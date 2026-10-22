using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Serilog;

namespace IpxListenerService
{
    public class XmlInstanceGenerator
    {
        private readonly GlobalSettingsRoot _settings;
        private readonly Serilog.ILogger _logger;
        private readonly Dictionary<int, int> _gpuPortMapping;

        public XmlInstanceGenerator(GlobalSettingsRoot settings, Serilog.ILogger logger)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Initialize GPU port mapping from settings
            _gpuPortMapping = new Dictionary<int, int>
            {
                { 0, _settings.GlobalSettings.TcpPorts.GetValueOrDefault("gpu_0", 8150) },
                { 1, _settings.GlobalSettings.TcpPorts.GetValueOrDefault("gpu_1", 8151) },
                { 2, _settings.GlobalSettings.TcpPorts.GetValueOrDefault("gpu_2", 8152) },
                { 3, _settings.GlobalSettings.TcpPorts.GetValueOrDefault("gpu_3", 8153) }
            };
        }

        public bool GenerateInstancesFromAllXmlFiles(string outputPath)
        {
            try
            {
                _logger.Information("Starting XML instance generation from path: {XmlPath}", _settings.GlobalSettings.XmlAnalyticsPath);

                if (!Directory.Exists(_settings.GlobalSettings.XmlAnalyticsPath))
                {
                    _logger.Error("XML analytics path does not exist: {XmlPath}", _settings.GlobalSettings.XmlAnalyticsPath);
                    return false;
                }

                // Load existing instances to preserve auto_start and reconnect_interval settings
                var existingInstances = LoadExistingInstances(outputPath);

                var allCameras = new List<CameraConfig>();
                var processedFiles = 0;
                
                // Process XML files from GPU-specific folders
                foreach (var gpuEntry in _settings.GlobalSettings.XmlFiles)
                {
                    var gpuNumber = gpuEntry.Key.Replace("gpu_", "");
                    var xmlFileName = gpuEntry.Value;
                    var gpuFolderName = $"NVidiaGPU-{gpuNumber}";
                    var gpuFolderPath = Path.Combine(_settings.GlobalSettings.XmlAnalyticsPath, gpuFolderName);
                    var xmlFilePath = Path.Combine(gpuFolderPath, xmlFileName);
                    
                    _logger.Information("Checking GPU {GpuNumber} folder: {GpuFolder}", gpuNumber, gpuFolderPath);
                    
                    if (!Directory.Exists(gpuFolderPath))
                    {
                        _logger.Warning("GPU folder does not exist: {GpuFolder}", gpuFolderPath);
                        continue;
                    }
                    
                    if (!File.Exists(xmlFilePath))
                    {
                        _logger.Warning("XML file not found: {XmlFile}", xmlFilePath);
                        continue;
                    }
                    
                    try
                    {
                        var cameras = ParseXmlCameras(xmlFilePath);
                        allCameras.AddRange(cameras);
                        processedFiles++;
                        _logger.Information("Processed {CameraCount} cameras from {FileName} in GPU {GpuNumber} folder", 
                            cameras.Count, xmlFileName, gpuNumber);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error processing XML file: {XmlFile}", xmlFilePath);
                    }
                }
                
                if (processedFiles == 0)
                {
                    _logger.Warning("No XML files were successfully processed from GPU folders");
                    return false;
                }
                
                _logger.Information("Successfully processed {ProcessedFiles} XML files from GPU folders", processedFiles);

                if (allCameras.Count == 0)
                {
                    _logger.Warning("No cameras found in any XML files");
                    return false;
                }

                // Get settings for filtering
                var activeOnly = GetAutoGenerationSetting("active_cameras_only", true);
                var defaultHost = GetAutoGenerationSetting("default_host", "127.0.0.1");

                // Generate instances
                var instancesConfig = GenerateInstances(allCameras, defaultHost, activeOnly, existingInstances);

                // Create backup if auto_backup is enabled
                var autoBackup = GetAutoGenerationSetting("auto_backup", true);
                if (autoBackup && File.Exists(outputPath))
                {
                    CreateBackup(outputPath);
                }

                // Save instances configuration
                var success = SaveInstancesJson(instancesConfig, outputPath);
                
                if (success)
                {
                    _logger.Information("Successfully generated {InstanceCount} instances to {OutputPath}", 
                        instancesConfig.Instances.Count, outputPath);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error generating instances from XML files");
                return false;
            }
        }

        private List<CameraConfig> ParseXmlCameras(string xmlFilePath)
        {
            var cameras = new List<CameraConfig>();
            
            try
            {
                var doc = XDocument.Load(xmlFilePath);
                var cameraElements = doc.Descendants("camera");
                
                foreach (var cameraElement in cameraElements)
                {
                    var nameElement = cameraElement.Element("name");
                    var gpuElement = cameraElement.Element("gpu");
                    var channelElement = cameraElement.Element("channel");
                    var activeElement = cameraElement.Element("active");
                    var descriptionElement = cameraElement.Element("description");
                    
                    // Skip if essential elements are missing
                    if (nameElement == null || gpuElement == null || channelElement == null || activeElement == null)
                    {
                        continue;
                    }
                    
                    var camera = new CameraConfig
                    {
                        Name = nameElement.Value ?? string.Empty,
                        Gpu = int.TryParse(gpuElement.Value, out var gpu) ? gpu : 0,
                        Channel = channelElement.Value ?? string.Empty,
                        Active = string.Equals(activeElement.Value, "true", StringComparison.OrdinalIgnoreCase),
                        Description = descriptionElement?.Value ?? string.Empty,
                        XmlSourceFile = Path.GetFileName(xmlFilePath),
                        XmlLastModified = File.GetLastWriteTime(xmlFilePath)
                    };
                    
                    cameras.Add(camera);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error parsing XML file: {FileName}", Path.GetFileName(xmlFilePath));
            }
            
            return cameras;
        }

        private InstanceRoot GenerateInstances(List<CameraConfig> cameras, string defaultHost, bool activeOnly, InstanceRoot existingInstances)
        {
            var instances = new Dictionary<string, InstanceConfig>();
            var existingInstancesDict = existingInstances?.Instances ?? new Dictionary<string, InstanceConfig>();
            
            // Filter cameras
            var filteredCameras = cameras.Where(c => !activeOnly || c.Active).ToList();
            
            _logger.Information("Generating instances for {FilteredCount} cameras (Active only: {ActiveOnly})", 
                filteredCameras.Count, activeOnly);
            
            foreach (var camera in filteredCameras)
            {
                // Get port based on GPU number
                var port = _gpuPortMapping.GetValueOrDefault(camera.Gpu, 8150);
                
                // Use camera name as instance key (matching Python script behavior)
                var instanceKey = camera.Name;
                
                // Get existing instance settings if available
                var existingInstance = existingInstancesDict.Values.FirstOrDefault(i => i.Name == camera.Name);
                
                var instanceConfig = new InstanceConfig
                {
                    Name = camera.Name,
                    Host = defaultHost,
                    Port = port,
                    Channel = int.TryParse(camera.Channel, out var channelInt) ? channelInt : 0,
                    LogLevel = GetInstanceDefault("log_level", "INFO"),
                    AutoStart = existingInstance?.AutoStart ?? GetInstanceDefault("auto_start", false),
                    ReconnectInterval = existingInstance?.ReconnectInterval ?? GetInstanceDefault("reconnect_interval", 5),
                    Description = $"Auto-generated from {camera.XmlSourceFile} on {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    Status = "Stopped",
                    XmlLastModified = camera.XmlLastModified.ToString("yyyy-MM-dd HH:mm:ss"),
                    XmlSourceFile = camera.XmlSourceFile,
                    Gpu = camera.Gpu
                };
                
                instances[instanceKey] = instanceConfig;
            }
            
            return new InstanceRoot { Instances = instances };
        }

        private InstanceRoot LoadExistingInstances(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    return JsonSerializer.Deserialize<InstanceRoot>(json);
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Could not load existing instances from {FilePath}", filePath);
            }
            
            return new InstanceRoot { Instances = new Dictionary<string, InstanceConfig>() };
        }

        private bool SaveInstancesJson(InstanceRoot instancesConfig, string outputPath)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                var json = JsonSerializer.Serialize(instancesConfig, options);
                File.WriteAllText(outputPath, json);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error saving instances file: {OutputPath}", outputPath);
                return false;
            }
        }

        private void CreateBackup(string filePath)
        {
            try
            {
                var backupPath = $"{filePath}.backup";
                File.Copy(filePath, backupPath, true);
                _logger.Information("Backup created: {BackupPath}", backupPath);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Could not create backup of {FilePath}", filePath);
            }
        }

        private T GetAutoGenerationSetting<T>(string key, T defaultValue)
        {
            try
            {
                if (_settings.GlobalSettings.AutoGeneration?.TryGetValue(key, out var value) == true)
                {
                    if (value is JsonElement jsonElement)
                    {
                        return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
                    }
                    return (T)Convert.ChangeType(value, typeof(T));
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Could not parse auto generation setting {Key}, using default: {Default}", key, defaultValue);
            }
            
            return defaultValue;
        }

        private T GetInstanceDefault<T>(string key, T defaultValue)
        {
            try
            {
                if (_settings.GlobalSettings.InstanceDefaults?.TryGetValue(key, out var value) == true)
                {
                    if (value is JsonElement jsonElement)
                    {
                        return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
                    }
                    return (T)Convert.ChangeType(value, typeof(T));
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Could not parse instance default {Key}, using default: {Default}", key, defaultValue);
            }
            
            return defaultValue;
        }
    }

    // Helper class for camera configuration
    public class CameraConfig
    {
        public string Name { get; set; }
        public int Gpu { get; set; }
        public string Channel { get; set; }
        public bool Active { get; set; }
        public string Description { get; set; }
        public string XmlSourceFile { get; set; }
        public DateTime XmlLastModified { get; set; }
    }
}
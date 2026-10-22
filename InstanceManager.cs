using System;
using System.IO;
using System.Threading.Tasks;
using Serilog;

namespace IpxListenerService
{
    /// <summary>
    /// Manages instance configuration and XML-based generation
    /// </summary>
    public class InstanceManager
    {
        private readonly GlobalSettingsRoot _globalSettings;
        private readonly Serilog.ILogger _logger;
        private readonly string _instancesFilePath;
        private readonly XmlInstanceGenerator _xmlGenerator;

        public InstanceManager(GlobalSettingsRoot globalSettings, string instancesFilePath = "instances.json", Serilog.ILogger logger = null)
        {
            _globalSettings = globalSettings ?? throw new ArgumentNullException(nameof(globalSettings));
            _instancesFilePath = instancesFilePath;
            _logger = logger ?? Log.Logger;
            _xmlGenerator = new XmlInstanceGenerator(_globalSettings, _logger);
        }

        /// <summary>
        /// Regenerate instances.json from XML configuration files
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        public bool RegenerateInstancesFromXml()
        {
            try
            {
                _logger?.Information("Starting instance regeneration from XML files...");
                
                var success = _xmlGenerator.GenerateInstancesFromAllXmlFiles(_instancesFilePath);
                
                if (success)
                {
                    _logger?.Information("Successfully regenerated instances from XML files");
                }
                else
                {
                    _logger?.Error("Failed to regenerate instances from XML files");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Error during instance regeneration");
                return false;
            }
        }

        /// <summary>
        /// Regenerate instances asynchronously
        /// </summary>
        /// <returns>Task with success result</returns>
        public Task<bool> RegenerateInstancesFromXmlAsync()
        {
            return Task.Run(() => RegenerateInstancesFromXml());
        }

        /// <summary>
        /// Check if XML files have been modified since last generation
        /// </summary>
        /// <returns>True if any XML file is newer than instances.json</returns>
        public bool AreXmlFilesNewer()
        {
            try
            {
                if (!File.Exists(_instancesFilePath))
                    return true; // instances.json doesn't exist, so XML is "newer"

                var instancesModTime = File.GetLastWriteTime(_instancesFilePath);

                if (_globalSettings.GlobalSettings.XmlFiles != null)
                {
                    foreach (var xmlFileEntry in _globalSettings.GlobalSettings.XmlFiles)
                    {
                        var xmlFileName = xmlFileEntry.Value;
                        var xmlFilePath = Path.IsPathRooted(xmlFileName)
                            ? xmlFileName
                            : Path.Combine(_globalSettings.GlobalSettings.XmlAnalyticsPath ?? AppDomain.CurrentDomain.BaseDirectory, xmlFileName);

                        if (File.Exists(xmlFilePath))
                        {
                            var xmlModTime = File.GetLastWriteTime(xmlFilePath);
                            if (xmlModTime > instancesModTime)
                            {
                                _logger?.Information("XML file {XmlFile} is newer than instances.json", xmlFilePath);
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger?.Warning(ex, "Error checking XML file modification times");
                return false;
            }
        }

        /// <summary>
        /// Check if settings.json is newer than instances.json
        /// </summary>
        /// <returns>True if settings.json is newer than instances.json</returns>
        public bool AreSettingsNewer()
        {
            try
            {
                if (!File.Exists(_instancesFilePath))
                    return true; // instances.json doesn't exist, so settings is "newer"

                var instancesModTime = File.GetLastWriteTime(_instancesFilePath);
                
                // Find settings.json in the same directory as instances.json
                var instancesDir = Path.GetDirectoryName(_instancesFilePath) ?? AppDomain.CurrentDomain.BaseDirectory;
                var settingsPath = Path.Combine(instancesDir, "settings.json");
                
                if (File.Exists(settingsPath))
                {
                    var settingsModTime = File.GetLastWriteTime(settingsPath);
                    if (settingsModTime > instancesModTime)
                    {
                        _logger?.Information("Settings file {SettingsFile} is newer than instances.json", settingsPath);
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger?.Warning(ex, "Error checking settings file modification time");
                return false;
            }
        }

        /// <summary>
        /// Auto-regenerate instances if XML files are newer and auto-generation is enabled
        /// </summary>
        /// <returns>True if regeneration occurred and was successful, false otherwise</returns>
        public bool AutoRegenerateIfNeeded()
        {
            try
            {
                // Check if auto-generation is enabled
                var autoGenEnabled = _globalSettings.GlobalSettings.AutoGeneration?.ContainsKey("enabled") == true &&
                    bool.TryParse(_globalSettings.GlobalSettings.AutoGeneration["enabled"].ToString(), out var enabled) && enabled;

                if (!autoGenEnabled)
                {
                    _logger?.Debug("Auto-generation is disabled");
                    return false;
                }

                // Check if scan on startup is enabled
                var scanOnStartup = _globalSettings.GlobalSettings.AutoGeneration?.ContainsKey("scan_on_startup") == true &&
                    bool.TryParse(_globalSettings.GlobalSettings.AutoGeneration["scan_on_startup"].ToString(), out var scan) && scan;

                if (!scanOnStartup)
                {
                    _logger?.Debug("Scan on startup is disabled");
                    return false;
                }

                // Check if XML files or settings are newer
                bool xmlNewer = AreXmlFilesNewer();
                bool settingsNewer = AreSettingsNewer();
                
                if (!xmlNewer && !settingsNewer)
                {
                    _logger?.Debug("XML files and settings are not newer than instances.json, skipping auto-regeneration");
                    return false;
                }
                
                if (xmlNewer)
                {
                    _logger?.Information("XML files are newer than instances.json, auto-regenerating...");
                }
                if (settingsNewer)
                {
                    _logger?.Information("Settings are newer than instances.json, auto-regenerating...");
                }

                return RegenerateInstancesFromXml();
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Error during auto-regeneration check");
                return false;
            }
        }
    }
}
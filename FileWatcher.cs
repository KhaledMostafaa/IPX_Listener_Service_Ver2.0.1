using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace IpxListenerService
{
    public class FileWatcher : IDisposable
    {
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private readonly GlobalSettings _settings;
        private readonly List<FileSystemWatcher> _watchers;
        private readonly Dictionary<string, DateTime> _lastChangeTime;
        private readonly Timer _debounceTimer;
        private readonly Timer _pollingTimer;
        private readonly object _lockObject = new object();
        private bool _disposed = false;
        private readonly HashSet<string> _pendingChanges;
        private DateTime _lastSettingsModTime;
        private readonly string _settingsFilePath;

        public event EventHandler<FileSystemEventArgs> SettingsFileChanged;
        public event EventHandler<FileSystemEventArgs> XmlFileChanged;
        public event EventHandler<FileSystemEventArgs> InstancesFileChanged;

        private readonly string _projectRootDirectory;

        public FileWatcher(GlobalSettings settings, Microsoft.Extensions.Logging.ILogger logger, string projectRootDirectory)
        {
            Console.WriteLine("*** FileWatcher constructor called ***");
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _projectRootDirectory = projectRootDirectory ?? throw new ArgumentNullException(nameof(projectRootDirectory));
            _watchers = new List<FileSystemWatcher>();
            _lastChangeTime = new Dictionary<string, DateTime>();
            _pendingChanges = new HashSet<string>();
            _debounceTimer = new Timer(ProcessPendingChanges, null, Timeout.Infinite, Timeout.Infinite);
            _settingsFilePath = Path.Combine(_projectRootDirectory, "settings.json");
            
            // Initialize last modification time
            if (File.Exists(_settingsFilePath))
            {
                _lastSettingsModTime = File.GetLastWriteTime(_settingsFilePath);
            }
            
            // Setup polling timer as fallback (check every 5 seconds)
            _pollingTimer = new Timer(CheckSettingsFilePolling, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

            Console.WriteLine("*** About to call InitializeWatchers ***");
            InitializeWatchers();
            Console.WriteLine("*** FileWatcher constructor completed ***");
        }

        private void InitializeWatchers()
        {
            try
            {
                Console.WriteLine($"*** InitializeWatchers: File watching enabled = {_settings.FileWatching.Enabled} ***");
                if (!_settings.FileWatching.Enabled)
                {
                    _logger.LogInformation("File watching is disabled in settings");
                    Console.WriteLine("*** File watching is disabled in settings ***");
                    return;
                }

                // Watch settings.json file
                if (_settings.FileWatching.WatchSettingsFile)
                {
                    Console.WriteLine("*** Setting up settings file watcher ***");
                    SetupSettingsFileWatcher();
                }

                // Watch XML files in GPU directories
                if (_settings.FileWatching.WatchXmlFiles)
                {
                    Console.WriteLine("*** Setting up XML file watchers ***");
                    SetupXmlFileWatchers();
                }

                // Watch instances.json file
                Console.WriteLine("*** Setting up instances file watcher ***");
                SetupInstancesFileWatcher();

                _logger.LogInformation("File watchers initialized successfully");
                Console.WriteLine("*** File watchers initialized successfully ***");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize file watchers");
                Console.WriteLine($"*** EXCEPTION in InitializeWatchers: {ex.Message} ***");
            }
        }

        private void SetupSettingsFileWatcher()
        {
            try
            {
                var settingsPath = _projectRootDirectory;
                var fullSettingsPath = Path.Combine(_projectRootDirectory, "settings.json");
                Console.WriteLine($"*** Settings file watcher path: {settingsPath} ***");
                Console.WriteLine($"*** Full settings.json path: {fullSettingsPath} ***");
                Console.WriteLine($"*** Project root directory: {_projectRootDirectory} ***");
                
                var settingsWatcher = new FileSystemWatcher(settingsPath, "settings.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };

                settingsWatcher.Changed += OnSettingsFileChanged;
                settingsWatcher.Created += OnSettingsFileChanged;
                settingsWatcher.Error += OnWatcherError;
                _watchers.Add(settingsWatcher);

                Console.WriteLine($"*** Settings file watcher created and enabled ***");
                _logger.LogInformation("Settings file watcher setup for: {Path}", settingsPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to setup settings file watcher");
                Console.WriteLine($"*** EXCEPTION in SetupSettingsFileWatcher: {ex.Message} ***");
            }
        }

        private void SetupXmlFileWatchers()
        {
            try
            {
                var xmlPath = _settings.XmlAnalyticsPath;
                if (!Directory.Exists(xmlPath))
                {
                    _logger.LogWarning("XML analytics path does not exist: {Path}", xmlPath);
                    return;
                }

                // Watch each GPU directory
                for (int gpuIndex = 0; gpuIndex < 4; gpuIndex++)
                {
                    var gpuDirName = $"NVidiaGPU-{gpuIndex}";
                    var gpuDirPath = Path.Combine(xmlPath, gpuDirName);

                    if (Directory.Exists(gpuDirPath))
                    {
                        var xmlWatcher = new FileSystemWatcher(gpuDirPath, "*.xml")
                        {
                            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                            EnableRaisingEvents = true
                        };

                        xmlWatcher.Changed += OnXmlFileChanged;
                        xmlWatcher.Created += OnXmlFileChanged;
                        xmlWatcher.Deleted += OnXmlFileChanged;
                        xmlWatcher.Error += OnWatcherError;
                        _watchers.Add(xmlWatcher);

                        _logger.LogInformation("XML file watcher setup for GPU directory: {Path}", gpuDirPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to setup XML file watchers");
            }
        }

        private void SetupInstancesFileWatcher()
        {
            try
            {
                var instancesPath = _projectRootDirectory;
                var fullInstancesPath = Path.Combine(_projectRootDirectory, "instances.json");
                Console.WriteLine($"*** Instances file watcher path: {instancesPath} ***");
                Console.WriteLine($"*** Full instances.json path: {fullInstancesPath} ***");
                
                var instancesWatcher = new FileSystemWatcher(instancesPath, "instances.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                instancesWatcher.Changed += OnInstancesFileChanged;
                instancesWatcher.Error += OnWatcherError;
                _watchers.Add(instancesWatcher);

                _logger.LogInformation("Instances file watcher setup for: {Path}", instancesPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to setup instances file watcher");
            }
        }

        private void OnSettingsFileChanged(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine($"*** RAW SETTINGS FILE CHANGE DETECTED: {e.FullPath} at {DateTime.Now} ***");
            _logger.LogInformation($"Raw settings file change detected: {e.FullPath}");
            HandleFileChange(e.FullPath, "settings");
        }

        private void OnXmlFileChanged(object sender, FileSystemEventArgs e)
        {
            HandleFileChange(e.FullPath, "xml");
        }

        private void OnInstancesFileChanged(object sender, FileSystemEventArgs e)
        {
            HandleFileChange(e.FullPath, "instances");
        }

        private void HandleFileChange(string filePath, string fileType)
        {
            try
            {
                Console.WriteLine($"*** HandleFileChange called: {fileType} - {filePath} ***");
                
                // Skip temporary files if configured
                if (_settings.FileWatching.ExcludeTempFiles && IsTemporaryFile(filePath))
                {
                    Console.WriteLine($"*** Skipping temporary file: {filePath} ***");
                    return;
                }

                // Check if file extension is monitored
                var extension = Path.GetExtension(filePath);
                Console.WriteLine($"*** File extension: {extension} ***");
                Console.WriteLine($"*** Monitored extensions: {string.Join(", ", _settings.FileWatching.MonitoredExtensions)} ***");
                
                if (!_settings.FileWatching.MonitoredExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"*** Extension {extension} not monitored, skipping ***");
                    return;
                }

                Console.WriteLine($"*** Processing file change for {fileType} ***");
                
                lock (_lockObject)
                {
                    var now = DateTime.UtcNow;
                    var key = $"{fileType}:{filePath}";

                    // Update last change time
                    _lastChangeTime[key] = now;
                    _pendingChanges.Add(key);

                    // Reset debounce timer
                    _debounceTimer.Change(_settings.FileWatching.DebounceDelayMs, Timeout.Infinite);
                    Console.WriteLine($"*** Debounce timer set for {_settings.FileWatching.DebounceDelayMs}ms ***");
                }

                _logger.LogDebug("File change detected: {FileType} - {FilePath}", fileType, filePath);
                Console.WriteLine($"*** File change logged: {fileType} - {filePath} ***");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling file change for {FilePath}", filePath);
                Console.WriteLine($"*** EXCEPTION in HandleFileChange: {ex.Message} ***");
            }
        }

        private void ProcessPendingChanges(object state)
        {
            try
            {
                Console.WriteLine("*** ProcessPendingChanges called ***");
                
                HashSet<string> changesToProcess;
                lock (_lockObject)
                {
                    changesToProcess = new HashSet<string>(_pendingChanges);
                    _pendingChanges.Clear();
                    Console.WriteLine($"*** Found {changesToProcess.Count} pending changes ***");
                }

                foreach (var change in changesToProcess)
                {
                    Console.WriteLine($"*** Processing change: {change} ***");
                    
                    var parts = change.Split(':', 2);
                    if (parts.Length != 2) 
                    {
                        Console.WriteLine($"*** Invalid change format: {change} ***");
                        continue;
                    }

                    var fileType = parts[0];
                    var filePath = parts[1];

                    Console.WriteLine($"*** Raising event for {fileType}: {filePath} ***");
                    
                    var eventArgs = new FileSystemEventArgs(WatcherChangeTypes.Changed, Path.GetDirectoryName(filePath), Path.GetFileName(filePath));

                    // Raise appropriate event
                    switch (fileType)
                    {
                        case "settings":
                            Console.WriteLine("*** Invoking SettingsFileChanged event ***");
                            SettingsFileChanged?.Invoke(this, eventArgs);
                            break;
                        case "xml":
                            Console.WriteLine("*** Invoking XmlFileChanged event ***");
                            XmlFileChanged?.Invoke(this, eventArgs);
                            break;
                        case "instances":
                            Console.WriteLine("*** Invoking InstancesFileChanged event ***");
                            InstancesFileChanged?.Invoke(this, eventArgs);
                            break;
                    }

                    _logger.LogInformation("Processed file change: {FileType} - {FilePath}", fileType, filePath);
                    Console.WriteLine($"*** Completed processing: {fileType} - {filePath} ***");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing pending file changes");
                Console.WriteLine($"*** EXCEPTION in ProcessPendingChanges: {ex.Message} ***");
            }
        }

        private void CheckSettingsFilePolling(object state)
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                    return;

                var currentModTime = File.GetLastWriteTime(_settingsFilePath);
                
                if (currentModTime > _lastSettingsModTime)
                {
                    Console.WriteLine($"*** POLLING DETECTED SETTINGS CHANGE: {_settingsFilePath} at {DateTime.Now} ***");
                    _logger.LogInformation($"Polling detected settings file change: {_settingsFilePath}");
                    
                    _lastSettingsModTime = currentModTime;
                    
                    // Trigger the same handling as FileSystemWatcher
                    HandleFileChange(_settingsFilePath, "settings");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in settings file polling check");
                Console.WriteLine($"*** EXCEPTION in CheckSettingsFilePolling: {ex.Message} ***");
            }
        }

        private bool IsTemporaryFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            return fileName.StartsWith("~") || 
                   fileName.StartsWith(".") || 
                   fileName.EndsWith(".tmp") || 
                   fileName.EndsWith(".temp") ||
                   fileName.Contains(".tmp.");
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            _logger.LogError(e.GetException(), "File watcher error occurred");
        }

        public void StopWatching()
        {
            try
            {
                foreach (var watcher in _watchers)
                {
                    watcher.EnableRaisingEvents = false;
                }
                _logger.LogInformation("File watching stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping file watchers");
            }
        }

        public void StartWatching()
        {
            try
            {
                if (!_settings.FileWatching.Enabled)
                {
                    _logger.LogInformation("File watching is disabled in settings");
                    return;
                }

                foreach (var watcher in _watchers)
                {
                    watcher.EnableRaisingEvents = true;
                }
                _logger.LogInformation("File watching started");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting file watchers");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    _debounceTimer?.Dispose();
                    _pollingTimer?.Dispose();
                    
                    foreach (var watcher in _watchers)
                    {
                        watcher.EnableRaisingEvents = false;
                        watcher.Dispose();
                    }
                    _watchers.Clear();
                    
                    _logger.LogInformation("FileWatcher disposed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing FileWatcher");
                }
                finally
                {
                    _disposed = true;
                }
            }
        }
    }


}
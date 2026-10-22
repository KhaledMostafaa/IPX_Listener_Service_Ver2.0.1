// using LicenseChkManager;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace IpxListenerService
{
    public class IpxListenerService : ServiceBase
    {
        private readonly ILogger _logger;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly List<InstanceWorker> _workers = new List<InstanceWorker>();
        private FileWatcher _fileWatcher;
        private GlobalSettingsRoot _currentSettings;
        private string _baseDir;
        private string _settingsPath;
        private string _instancesPath;
        private int _restartAttempts = 0;
        // private System.Timers.Timer _licenseCheckTimer;
        // private System.Timers.Timer _backgroundLicenseTimer;


        public bool IsRunning { get; private set; }
        
        // Event for console mode restart
        public event EventHandler RestartRequested;

        public IpxListenerService()
        {
            ServiceName = "IPXSListenerService";

            // Initialize the timer
            // _licenseCheckTimer = new System.Timers.Timer();

            _logger = LoggerFactory.Create(builder =>
            {
                builder.AddSimpleConsole(o =>
                {
                    o.IncludeScopes = false;
                    o.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ";
                });
            }).CreateLogger<IpxListenerService>();
        }

        private string GetExecutableDirectory()
        {
            // Use the directory where the executable is located
            var executableDir = AppDomain.CurrentDomain.BaseDirectory;
            _logger.LogInformation("Using executable directory: {ExecutableDir}", executableDir);
            return executableDir;
        }

        public void DebugRun()
        {
            OnStart(null);
            _logger.LogInformation("Running... Press Enter to stop");
            Console.WriteLine("Running... Press Enter to stop");
            Console.ReadLine();
            OnStop();
        }

        public void StopService()
        {
            OnStop();
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                _logger.LogInformation("Service is starting...");

                if (!Environment.UserInteractive)
                    base.RequestAdditionalTime(60000);

                // ✅ لا حاجة لفحص الترخيص هنا لأن Program.cs تأكد بالفعل أنه صالح
                // لكن سنضيف فحص دوري في الخلفية لاحقًا

                IsRunning = true;
                _cancellationTokenSource = new CancellationTokenSource();

                _baseDir = GetExecutableDirectory();
                _settingsPath = Path.Combine(_baseDir, "settings.json");
                _instancesPath = Path.Combine(_baseDir, "instances.json");

                _logger.LogInformation($"Using project root directory: {_baseDir}");
                _logger.LogInformation($"Settings file path: {_settingsPath}");
                _logger.LogInformation($"Instances file path: {_instancesPath}");

                _ = Task.Run(() => RunServiceAsync(), _cancellationTokenSource.Token);

                // --- Background license check commented out to run app without license restrictions ---
                // _backgroundLicenseTimer = new System.Timers.Timer(43200000); // 12 ساعة = 12 * 60 * 60 * 1000
                // _backgroundLicenseTimer.Elapsed += BackgroundLicenseCheck;
                // _backgroundLicenseTimer.AutoReset = true;
                // _backgroundLicenseTimer.Enabled = true;

                // _logger.LogInformation("Background license monitoring started successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during service start.");
                Stop();
            }
        }

        /*
        private void BackgroundLicenseCheck(object sender, ElapsedEventArgs e)
        {
            try
            {
                _logger.LogInformation("Performing background license check...");

                const string productName = "IPXSListenerService";
                string activeLicenseKey = Program.GetLicenseKeyFromSources(productName);

                if (string.IsNullOrWhiteSpace(activeLicenseKey))
                {
                    _logger.LogWarning("No license key found during background check.");
                    return;
                }

                var licenseManager = new LicenseManager();
                LicenseData licenseData = licenseManager.ValidateAndDecryptLicenseKey(activeLicenseKey);

                if (licenseData == null || !string.Equals(licenseData.ApplicationName, productName, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError("License validation failed (invalid application name). Stopping service.");
                    Stop();
                    return;
                }

                if (licenseData.ExpirationDate < DateTime.UtcNow)
                {
                    _logger.LogError($"License expired on {licenseData.ExpirationDate}. Stopping service.");
                    Stop();
                    return;
                }

                _logger.LogInformation($"License check passed. Valid until {licenseData.ExpirationDate:yyyy-MM-dd}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during background license check.");
            }
        }
        */




        //protected override void OnStart(string[] args)
        //{
        //    _logger.LogInformation("Service is starting...");

        //    // Request additional time to prevent timeout during initialization
        //    RequestAdditionalTime(60000); // Request 60 seconds additional time

        //    //==========================================
        //    var isLicensed = EncryptionHelper.CheckLicenseValedity();

        //    // Check license on start
        //    if (!isLicensed.StartsWith("[1001]"))
        //    {
        //        _logger.LogError("License is not active. Stopping service.");
        //        Stop();
        //        return;
        //    }

        //    // Configure and start a timer to check the license every 12 hours (43200000 milliseconds)
        //    _licenseCheckTimer = new System.Timers.Timer(2000);
        //    _licenseCheckTimer.Elapsed += OnLicenseCheckElapsed;
        //    _licenseCheckTimer.AutoReset = true;
        //    _licenseCheckTimer.Enabled = true;

        //    //==========================================

        //    IsRunning = true;
        //    _cancellationTokenSource = new CancellationTokenSource();

        //    // Initialize paths - use executable directory
        //    _baseDir = GetExecutableDirectory();
        //    _settingsPath = Path.Combine(_baseDir, "settings.json");
        //    _instancesPath = Path.Combine(_baseDir, "instances.json");

        //    _logger.LogInformation($"Using project root directory: {_baseDir}");
        //    _logger.LogInformation($"Settings file path: {_settingsPath}");
        //    _logger.LogInformation($"Instances file path: {_instancesPath}");

        //    // Immediately signal that the service has started
        //    // and perform initialization in the background
        //    _ = Task.Run(() => RunServiceAsync(), _cancellationTokenSource.Token);

        //    _logger.LogInformation("Service start signal sent to SCM, continuing initialization in background...");
        //}

        // Add this method to the IpxListenerService class
        //private void RequestAdditionalTime(int milliseconds)
        //{
        //    try
        //    {
        //        // This method informs SCM that we need more time to start
        //        var service = this as ServiceBase;
        //        service?.RequestAdditionalTime(milliseconds);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogWarning(ex, "Could not request additional time from SCM");
        //    }
        //}

        //protected override void OnStart(string[] args)
        //{
        //    _logger.LogInformation("Service is starting...");

        //    //==========================================
        //    var isLicensed = EncryptionHelper.CheckLicenseValedity();

        //    // Check license on start

        //    if (!isLicensed.StartsWith("[1001]"))
        //    {
        //        _logger.LogError("License is not active. Stopping service.");
        //        Stop();
        //        return;
        //    }

        //    // Configure and start a timer to check the license every 12 hours (43200000 milliseconds)
        //    _licenseCheckTimer = new System.Timers.Timer(2000);
        //    _licenseCheckTimer.Elapsed += OnLicenseCheckElapsed;
        //    _licenseCheckTimer.AutoReset = true;
        //    _licenseCheckTimer.Enabled = true;

        //    //==========================================

        //    IsRunning = true;
        //    _cancellationTokenSource = new CancellationTokenSource();

        //    // Initialize paths - use executable directory
        //    _baseDir = GetExecutableDirectory();
        //    _settingsPath = Path.Combine(_baseDir, "settings.json");
        //    _instancesPath = Path.Combine(_baseDir, "instances.json");

        //    _logger.LogInformation($"Using project root directory: {_baseDir}");
        //    _logger.LogInformation($"Settings file path: {_settingsPath}");
        //    _logger.LogInformation($"Instances file path: {_instancesPath}");

        //    // Kick off background
        //    _ = Task.Run(RunServiceAsync, _cancellationTokenSource.Token);
        //}

        /*
        private void OnLicenseCheckElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                _logger.LogInformation("Performing periodic license check...");
                var isLicensed = EncryptionHelper.CheckLicenseValedity();

                if (!isLicensed.StartsWith("[1001]"))
                {
                    _logger.LogWarning("License check failed. Will retry in next cycle.");
                }
                else
                {
                    _logger.LogInformation("License verified successfully.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during periodic license check.");
            }
        }
        */


        //private void OnLicenseCheckElapsed(object sender, ElapsedEventArgs e)
        //{
        //    _logger.LogInformation("Performing periodic license check.");
        //    var isLicensed = EncryptionHelper.CheckLicenseValedity();

        //    if (!isLicensed.StartsWith("[1001]"))
        //    {
        //        _logger.LogError("License is no longer active. Stopping service.");
        //        _licenseCheckTimer.Enabled = false; // Disable the timer to prevent re-entry
        //        Stop();
        //    }
        //}

        protected override void OnStop()
        {
            _logger.LogInformation("Service is stopping...");

            _cancellationTokenSource?.Cancel();
            IsRunning = false;
            // _licenseCheckTimer?.Dispose();
            _logger.LogInformation("Service is stopped.");





            IsRunning = false;

            // Dispose file watcher first
            try
            {
                _fileWatcher?.Dispose();
                _fileWatcher = null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing file watcher.");
            }

            try
            {
                _cancellationTokenSource?.Cancel();
            }
            catch { /* ignore */ }

            // Ask each worker to stop and wait up to 10s total
            var stopTasks = _workers.Select(w => w.StopAsync()).ToArray();
            if (stopTasks.Length > 0)
            {
                try
                {
                    Task.WaitAll(stopTasks, TimeSpan.FromSeconds(10));
                }
                catch (AggregateException aex)
                {
                    foreach (var ex in aex.InnerExceptions)
                        _logger.LogWarning(ex, "Worker stop threw.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Timed out waiting for workers to stop.");
                }
            }

            _logger.LogInformation("Service stopped.");
        }

        private async Task RunServiceAsync()
        {
            try
            {
                _logger.LogInformation("Initializing database...");
                Program.EnsureDatabaseCreated();
                _logger.LogInformation("Database initialization complete. Loading configs...");

                _currentSettings = JsonLoader.LoadGlobalSettings(_settingsPath);
                var settings = _currentSettings;

                // Initialize FileWatcher if enabled
                if (settings.GlobalSettings.FileWatching.Enabled)
                {
                    InitializeFileWatcher();
                }
                
                // Start periodic settings check as a more reliable fallback
                _ = Task.Run(PeriodicSettingsCheckAsync, _cancellationTokenSource.Token);
                
                // Auto-generate instances.json if needed
                try
                {
                    var instanceManager = new InstanceManager(settings, _instancesPath, null);
                    
                    // Check if instances.json exists or if auto-generation is needed
                    if (!File.Exists(_instancesPath))
                    {
                        _logger.LogInformation("instances.json not found, generating from XML files...");
                        var generated = instanceManager.RegenerateInstancesFromXml();
                        if (!generated)
                        {
                            _logger.LogError("Failed to generate instances.json from XML files");
                            throw new FileNotFoundException($"Configuration file not found and could not be generated: {_instancesPath}");
                        }
                    }
                    else
                    {
                        // Check if auto-regeneration is needed
                        var autoRegenerated = instanceManager.AutoRegenerateIfNeeded();
                        if (autoRegenerated)
                        {
                            _logger.LogInformation("instances.json auto-regenerated from XML files");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during instances.json auto-generation");
                    // Continue with existing file if auto-generation fails and file exists
                    if (!File.Exists(_instancesPath))
                    {
                        throw;
                    }
                }
                
                var instances = JsonLoader.LoadInstances(_instancesPath);

                var connStr = !string.IsNullOrWhiteSpace(settings.GlobalSettings.Database.Url)
                    ? Program.ParsePostgresConnectionStringFromUri(settings.GlobalSettings.Database.Url)
                    : Program.BuildConnectionStringFromParts(settings.GlobalSettings.Database);

                var dbFactory = new DatabaseContextFactory(connStr);

                _logger.LogInformation($"Loaded {instances.Instances.Count} listener instances.");

                foreach (var kv in instances.Instances)

                {
                    var instanceId = kv.Key;
                    var cfg = kv.Value;

                    try
                    {
                        _logger.LogInformation($"Starting worker for instance '{instanceId}'...");
                        var worker = new InstanceWorker(instanceId, cfg, settings.GlobalSettings, dbFactory, _cancellationTokenSource.Token);
                        _workers.Add(worker);
                        worker.Start();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to start worker for instance '{instanceId}'.");
                    }
                }

                // Keep the task alive until cancellation
                try
                {
                    await Task.Delay(Timeout.Infinite, _cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    // expected
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in RunServiceAsync.");
            }
        }

        private void InitializeFileWatcher()
        {
            try
            {
                Console.WriteLine("*** InitializeFileWatcher method called ***");
                _logger.LogInformation("=== INITIALIZING FILE WATCHER ===");
                _logger.LogInformation("File watching enabled: {Enabled}", _currentSettings.GlobalSettings.FileWatching.Enabled);
                _logger.LogInformation("Watch settings file: {WatchSettings}", _currentSettings.GlobalSettings.FileWatching.WatchSettingsFile);
                _logger.LogInformation("Watch XML files: {WatchXml}", _currentSettings.GlobalSettings.FileWatching.WatchXmlFiles);
                
                Console.WriteLine("*** About to create FileWatcher instance ***");
                _fileWatcher = new FileWatcher(_currentSettings.GlobalSettings, _logger, _baseDir);
                Console.WriteLine("*** FileWatcher instance created successfully ***");
                
                // Subscribe to file change events
                _fileWatcher.SettingsFileChanged += OnSettingsChanged;
                _fileWatcher.XmlFileChanged += OnXmlFilesChanged;
                _fileWatcher.InstancesFileChanged += OnInstancesChanged;
                
                _logger.LogInformation("=== FILE WATCHER INITIALIZED AND STARTED ===");
                Console.WriteLine("*** FILE WATCHER INITIALIZED AND STARTED ***");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== FAILED TO INITIALIZE FILE WATCHER ===");
                Console.WriteLine($"*** EXCEPTION in InitializeFileWatcher: {ex.Message} ***");
                Console.WriteLine($"*** STACK TRACE: {ex.StackTrace} ***");
            }
        }

        private async void OnSettingsChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                _logger.LogInformation($"*** SETTINGS FILE CHANGED EVENT TRIGGERED: {e.FullPath} ***");
                Console.WriteLine($"*** SETTINGS FILE CHANGED EVENT TRIGGERED: {e.FullPath} ***");
                _logger.LogInformation($"Settings file changed: {e.FullPath}. Initiating service restart...");
                
                if (_currentSettings.GlobalSettings.FileWatching.RestartOnSettingsChange)
                {
                    _logger.LogInformation("*** CALLING RestartServiceAsync ***");
                    Console.WriteLine("*** CALLING RestartServiceAsync ***");
                    await RestartServiceAsync();
                }
                else
                {
                    _logger.LogInformation("*** RESTART ON SETTINGS CHANGE IS DISABLED - REGENERATING INSTANCES ONLY ***");
                    Console.WriteLine("*** RESTART ON SETTINGS CHANGE IS DISABLED - REGENERATING INSTANCES ONLY ***");
                    
                    // Even if restart is disabled, we should still regenerate instances with new settings
                    await RegenerateInstancesWithNewSettingsAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling settings file change.");
                Console.WriteLine($"*** EXCEPTION in OnSettingsChanged: {ex.Message} ***");
            }
        }

        private async void OnXmlFilesChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                _logger.LogInformation($"XML file changed: {e.FullPath}. Regenerating instances.json...");
                
                if (_currentSettings.GlobalSettings.FileWatching.RegenerateOnXmlChange)
                {
                    await RegenerateInstancesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling XML file change.");
            }
        }

        private async void OnInstancesChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                _logger.LogInformation($"Instances file changed: {e.FullPath}. Reloading workers...");
                
                // Always reload workers when instances.json changes
                await ReloadWorkersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling instances file change.");
            }
        }

        private async Task RestartServiceAsync()
        {
            try
            {
                _logger.LogInformation("*** ENTERED RestartServiceAsync ***");
                Console.WriteLine("*** ENTERED RestartServiceAsync ***");
                
                var maxAttempts = _currentSettings.GlobalSettings.FileWatching.MaxRestartAttempts;
                var restartDelay = _currentSettings.GlobalSettings.FileWatching.RestartDelayMs;
                
                if (_restartAttempts >= maxAttempts)
                {
                    _logger.LogWarning($"Maximum restart attempts ({maxAttempts}) reached. Skipping restart.");
                    Console.WriteLine($"*** Maximum restart attempts ({maxAttempts}) reached. Skipping restart. ***");
                    return;
                }
                
                _restartAttempts++;
                _logger.LogInformation($"Restarting service (attempt {_restartAttempts}/{maxAttempts})...");
                Console.WriteLine($"*** Restarting service (attempt {_restartAttempts}/{maxAttempts})... ***");
                
                // Wait for restart delay
                await Task.Delay(restartDelay);
                
                // Check if running in console mode (interactive)
                if (Environment.UserInteractive)
                {
                    _logger.LogInformation("Console mode detected - triggering application restart...");
                    Console.WriteLine("*** Console mode detected - triggering application restart ***");
                    
                    // Trigger application restart for console mode
                    RestartRequested?.Invoke(this, EventArgs.Empty);
                    return;
                }
                
                // Service mode - restart workers only
                _logger.LogInformation("Service mode detected - restarting workers...");
                
                // Stop current workers
                await StopWorkersAsync();
                
                // Reload settings first
                _logger.LogInformation("Reloading settings from settings.json...");
                _currentSettings = JsonLoader.LoadGlobalSettings(_settingsPath);
                
                // Regenerate instances.json with new settings
                _logger.LogInformation("Regenerating instances.json with updated settings...");
                var instanceManager = new InstanceManager(_currentSettings, _instancesPath, null);
                var regenerated = instanceManager.RegenerateInstancesFromXml();
                
                if (!regenerated)
                {
                    _logger.LogWarning("Failed to regenerate instances.json. Service restart may use outdated configuration.");
                }
                else
                {
                    _logger.LogInformation("instances.json regenerated successfully with new settings.");
                }
                
                // Reinitialize file watcher if settings changed
                _fileWatcher?.Dispose();
                if (_currentSettings.GlobalSettings.FileWatching.Enabled)
                {
                    InitializeFileWatcher();
                }
                
                // Start workers with new configuration
                await StartWorkersAsync();
                
                _logger.LogInformation("Service restart completed successfully.");
                
                // Reset restart attempts on successful restart
                _restartAttempts = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during service restart.");
            }
        }

        private async Task RegenerateInstancesAsync()
        {
            try
            {
                _logger.LogInformation("Regenerating instances.json from XML files...");
                
                var instanceManager = new InstanceManager(_currentSettings, _instancesPath, null);
                var regenerated = instanceManager.RegenerateInstancesFromXml();
                
                if (regenerated)
                {
                    _logger.LogInformation("instances.json regenerated successfully.");
                    
                    // Always reload workers when instances.json is regenerated
                    await ReloadWorkersAsync();
                }
                else
                {
                    _logger.LogWarning("Failed to regenerate instances.json from XML files.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error regenerating instances.json.");
            }
        }

        private DateTime _lastSettingsModTime = DateTime.MinValue;
        
        private async Task PeriodicSettingsCheckAsync()
        {
            try
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        if (File.Exists(_settingsPath))
                        {
                            var currentModTime = File.GetLastWriteTime(_settingsPath);
                            
                            if (currentModTime > _lastSettingsModTime && _lastSettingsModTime != DateTime.MinValue)
                            {
                                _logger.LogInformation("*** PERIODIC CHECK DETECTED SETTINGS CHANGE ***");
                                Console.WriteLine("*** PERIODIC CHECK DETECTED SETTINGS CHANGE ***");
                                
                                await RegenerateInstancesWithNewSettingsAsync();
                            }
                            
                            _lastSettingsModTime = currentModTime;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in periodic settings check");
                    }
                    
                    // Check every 3 seconds
                    await Task.Delay(3000, _cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when service is stopping
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in periodic settings check loop");
            }
        }

        private async Task RegenerateInstancesWithNewSettingsAsync()
        {
            try
            {
                _logger.LogInformation("*** REGENERATING INSTANCES WITH NEW SETTINGS ***");
                Console.WriteLine("*** REGENERATING INSTANCES WITH NEW SETTINGS ***");
                
                // Reload settings first
                _logger.LogInformation("Reloading settings from settings.json...");
                _currentSettings = JsonLoader.LoadGlobalSettings(_settingsPath);
                
                // Regenerate instances.json with new settings
                _logger.LogInformation("Regenerating instances.json with updated settings...");
                var instanceManager = new InstanceManager(_currentSettings, _instancesPath, null);
                var regenerated = instanceManager.RegenerateInstancesFromXml();
                
                if (regenerated)
                {
                    _logger.LogInformation("instances.json regenerated successfully with new settings.");
                    Console.WriteLine("*** instances.json regenerated successfully with new settings ***");
                    
                    // Reload workers with new configuration
                    await ReloadWorkersAsync();
                }
                else
                {
                    _logger.LogWarning("Failed to regenerate instances.json with new settings.");
                    Console.WriteLine("*** Failed to regenerate instances.json with new settings ***");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error regenerating instances.json with new settings.");
                Console.WriteLine($"*** EXCEPTION in RegenerateInstancesWithNewSettingsAsync: {ex.Message} ***");
            }
        }

        private async Task ReloadWorkersAsync()
        {
            try
            {
                _logger.LogInformation("Reloading workers with updated configuration...");
                
                // Stop current workers
                await StopWorkersAsync();
                
                // Start workers with new configuration
                await StartWorkersAsync();
                
                _logger.LogInformation("Workers reloaded successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reloading workers.");
            }
        }

        private async Task StopWorkersAsync()
        {
            try
            {
                _logger.LogInformation("Stopping all workers...");
                
                var stopTasks = _workers.Select(w => w.StopAsync()).ToArray();
                if (stopTasks.Length > 0)
                {
                    await Task.WhenAll(stopTasks);
                }
                
                _workers.Clear();
                _logger.LogInformation("All workers stopped.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping workers.");
            }
        }

        private async Task StartWorkersAsync()
        {
            try
            {
                _logger.LogInformation("Starting workers...");
                
                var instances = JsonLoader.LoadInstances(_instancesPath);
                
                var connStr = !string.IsNullOrWhiteSpace(_currentSettings.GlobalSettings.Database.Url)
                    ? Program.ParsePostgresConnectionStringFromUri(_currentSettings.GlobalSettings.Database.Url)
                    : Program.BuildConnectionStringFromParts(_currentSettings.GlobalSettings.Database);
                
                var dbFactory = new DatabaseContextFactory(connStr);
                
                _logger.LogInformation($"Starting {instances.Instances.Count} worker instances...");
                
                foreach (var kv in instances.Instances)
                {
                    var instanceId = kv.Key;
                    var cfg = kv.Value;
                    
                    try
                    {
                        _logger.LogInformation($"Starting worker for instance '{instanceId}'...");
                        var worker = new InstanceWorker(instanceId, cfg, _currentSettings.GlobalSettings, dbFactory, _cancellationTokenSource.Token);
                        _workers.Add(worker);
                        worker.Start();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to start worker for instance '{instanceId}'.");
                    }
                }
                
                _logger.LogInformation("Workers started successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting workers.");
            }
        }
    }

    public class InstanceWorker
    {
        private readonly string _instanceId;
        private readonly CancellationToken _cancellationToken;
        private readonly ILogger _logger;
        private Task _workerTask;
        private IpxTcpClient _client;
        private readonly IpxFilter _filter;
        private readonly DatabaseContextFactory _dbFactory;

        public InstanceWorker(string instanceId, InstanceConfig config, GlobalSettings globalSettings,
                              DatabaseContextFactory dbContextFactory, CancellationToken cancellationToken)
        {
            _instanceId = instanceId;
            _cancellationToken = cancellationToken;
            _logger = LoggerFactory.Create(builder => builder.AddSimpleConsole()).CreateLogger<InstanceWorker>();

            _dbFactory = dbContextFactory;
            _filter = new IpxFilter();

            // 🔹 Map EventPayload → EventModel before saving
            _client = new IpxTcpClient(
                config.Host,
                config.Port,
                config.Channel,
                _instanceId,
                _logger,
                async payload =>
                {
                    var model = MapPayloadToModel(payload, config, _instanceId);
                    await SaveEventAsync(model);
                });
        }

        public void Start()
        {
            _workerTask = Task.Run(() => _client.StartAsync(_cancellationToken), _cancellationToken);
        }

        public async Task StopAsync()
        {
            _client?.Disconnect();
            if (_workerTask != null)
            {
                try { await _workerTask; } catch { }
            }
        }

        private async Task SaveEventAsync(EventModel model)
        {
            var (allowed, increment) = _filter.ShouldAllowWithIncrement(model);
            
            if (!allowed)
            {
                _logger.LogInformation("Event {EventId} skipped by filter.", model.EventId);
                return;
            }

            try
            {
                // If increment > 1, create multiple events (one per person/object that crossed simultaneously)
                // For negative increments (count decrease) or increment = 1, create only 1 event
                int eventsToCreate = increment > 1 ? increment : 1;
                
                if (eventsToCreate > 1)
                {
                    _logger.LogInformation("Creating {Count} events for simultaneous crossings (increment: {Increment})", eventsToCreate, increment);
                }

                using var db = _dbFactory.CreateDbContext();
                
                // Create and save multiple events if increment > 1
                for (int i = 0; i < eventsToCreate; i++)
                {
                    EventModel eventToSave;
                    
                    if (i == 0)
                    {
                        // Use the original model for the first event
                        eventToSave = model;
                    }
                    else
                    {
                        // Create a copy for subsequent events with unique EventId and slightly offset timestamp
                        eventToSave = new EventModel
                        {
                            EventId = Guid.NewGuid().ToString(),
                            ServerIp = model.ServerIp,
                            ServerPort = model.ServerPort,
                            Timestamp = model.Timestamp.AddMilliseconds(i * 10), // Small offset to distinguish events
                            Provider = model.Provider,
                            CameraName = model.CameraName,
                            AnalyticType = model.AnalyticType,
                            ObjectType = model.ObjectType,
                            ObjectName = model.ObjectName,
                            AccumulatedCount = model.AccumulatedCount,
                            Quantity = model.Quantity,
                            SnapshotUrl = model.SnapshotUrl,
                            SnapshotUrl2 = model.SnapshotUrl2,
                            AreaName = model.AreaName,
                            Alarmed = model.Alarmed,
                            Channel = model.Channel,
                            Gpu = model.Gpu
                        };
                    }
                    
                    db.Events.Add(eventToSave);
                    _logger.LogInformation("Event {EventId} added to save queue (event {Index}/{Total})", eventToSave.EventId, i + 1, eventsToCreate);
                }
                
                await db.SaveChangesAsync();
                _logger.LogInformation("Successfully saved {Count} event(s) (increment: {Increment})", eventsToCreate, increment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving event {EventId}", model.EventId);
            }
        }

        private static EventModel MapPayloadToModel(EventPayload payload, InstanceConfig config, string instanceId)
        {
            return new EventModel
            {
                EventId = Guid.NewGuid().ToString(),
                ServerIp = config.Host,
                ServerPort = config.Port.ToString(),
                Timestamp = DateTime.UtcNow,
                Provider = "IPX",
                CameraName = payload.CameraName,
                AnalyticType = payload.AnalyticType,
                ObjectType = payload.ObjectType,
                ObjectName = payload.ObjectName,
                AccumulatedCount = payload.AccumulatedCount,
                Quantity = payload.Quantity,
                SnapshotUrl = payload.SnapshotUrl,
                SnapshotUrl2 = payload.SnapshotUrl2,
                AreaName = payload.AreaName,
                Alarmed = payload.Alarmed,
                Channel = payload.Channel,
                Gpu = payload.Gpu
            };
        }
    }


    public class IpxTcpClient
    {
        private readonly string _host;
        private readonly int _port;
        private readonly int _channel;
        private readonly string _instanceId;
        private readonly ILogger _logger;
        private readonly IpxDecoder _decoder;
        private readonly Func<EventPayload, Task> _onEventReceived;

        private TcpClient _client;
        private NetworkStream _stream;

        public IpxTcpClient(string host, int port, int channel, string instanceId, ILogger logger, Func<EventPayload, Task> eventHandler)
        {
            _host = host;
            _port = port;
            _channel = channel;
            _instanceId = instanceId;
            _logger = logger;
            _onEventReceived = eventHandler;
            _decoder = new IpxDecoder(logger);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Client ConnectAsync..");
                    await ConnectAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("Client ReadLoopAsync..");
                    await ReadLoopAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Client shutdown requested.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Connection error. Retrying in 5 seconds...");
                    try { Disconnect(); } catch { }
                    try { await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                }
            }

            // Ensure socket closed
            try { Disconnect(); } catch { }
        }

        private async Task ConnectAsync(CancellationToken ct)
        {
            _logger.LogInformation($"Connecting to {_host}:{_port} (channel {_channel})...");
            _client = new TcpClient();
            // TcpClient.ConnectAsync doesn't take CT in .NET Framework; we rely on ct-triggered Read loop to close after connect.
            await _client.ConnectAsync(_host, _port).ConfigureAwait(false);

            _stream = _client.GetStream();
            var hello = Encoding.UTF8.GetBytes($"ID={_channel}\r\n");
            await _stream.WriteAsync(hello, 0, hello.Length, ct).ConfigureAwait(false);
            _logger.LogInformation("Connected and channel ID sent.");
        }

        public void Disconnect()
        {
            try { _stream?.Close(); } catch { }
            try { _stream?.Dispose(); } catch { }
            try { _client?.Close(); } catch { }
            try { _client?.Dispose(); } catch { }
            _stream = null;
            _client = null;
        }

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            _logger.LogInformation($"Starting read loop {_client}..{_stream}.");

            if (_client == null || _stream == null)
                return;

            var buffer = new byte[8192];
            var memoryStream = new MemoryStream();
            var frameEndPattern = Encoding.UTF8.GetBytes("---IPX-END---");

            _logger.LogInformation("Starting read loop...");

            while (!ct.IsCancellationRequested)
            {
                int bytesRead=0;
                try
                {
                    //Osama
                    // TODO
                    try
                    {
                        bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false);
                        _logger.LogError($"bytesRead...{bytesRead}");

                    }
                    catch(Exception ex)
                    {
                        _logger.LogError($"bytesRead...{ex.Message}");
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogError($"bytesRead...OperationCanceledException");
                    break;
                }
                catch (ObjectDisposedException)
                {
                    // Stream closed from Disconnect()
                    break;
                }

                if (bytesRead == 0)
                {
                    _logger.LogInformation("Remote closed connection.");
                    break;
                }

                await memoryStream.WriteAsync(buffer, 0, bytesRead, ct).ConfigureAwait(false);

                // Extract frames
                while (true)
                {
                    var data = memoryStream.ToArray();
                    var endOfFrameIndex = FindPattern(data, frameEndPattern);

                    if (endOfFrameIndex == -1) break;

                    var frameData = new byte[endOfFrameIndex + frameEndPattern.Length];
                    Array.Copy(data, frameData, frameData.Length);

                    var events = _decoder.DecodeFrame(frameData);
                    foreach (var evt in events)
                    {
                        if (ct.IsCancellationRequested) break;
                        await _onEventReceived(evt).ConfigureAwait(false);
                    }

                    var remaining = new byte[data.Length - frameData.Length];
                    Array.Copy(data, frameData.Length, remaining, 0, remaining.Length);
                    memoryStream = new MemoryStream();
                    await memoryStream.WriteAsync(remaining, 0, remaining.Length, ct).ConfigureAwait(false);
                }
            }
        }

        private static int FindPattern(byte[] source, byte[] pattern)
        {
            if (source == null || pattern == null || pattern.Length == 0 || source.Length < pattern.Length)
                return -1;

            for (int i = 0; i <= source.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (source[i + j] != pattern[j]) { match = false; break; }
                }
                if (match) return i;
            }
            return -1;
        }
    }

    public class IpxDecoder
    {
        private readonly ILogger _logger;

        public IpxDecoder(ILogger logger)
        {
            _logger = logger;
        }

        public List<EventPayload> DecodeFrame(byte[] raw)
        {
            try
            {
                var startMarker = Encoding.UTF8.GetBytes("---IPX-BEGIN---");
                var endMarker = Encoding.UTF8.GetBytes("---IPX-END---");

                int startIndex = IndexOf(raw, startMarker);
                int endIndex = IndexOf(raw, endMarker);

                ReadOnlyMemory<byte> jsonBytes;
                if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
                {
                    jsonBytes = new ReadOnlyMemory<byte>(raw, startIndex + startMarker.Length, endIndex - (startIndex + startMarker.Length));
                }
                else
                {
                    jsonBytes = new ReadOnlyMemory<byte>(raw);
                }

                var options = new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                };

                using var doc = JsonDocument.Parse(jsonBytes, options);
                return ProcessJsonDocument(doc.RootElement);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse IPX JSON payload.");
                return new List<EventPayload>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during frame decoding.");
                return new List<EventPayload>();
            }
        }

        public static int IndexOf(byte[] array, byte[] pattern)
        {
            if (pattern.Length == 0) return 0;
            if (pattern.Length > array.Length) return -1;

            for (int i = 0; i <= array.Length - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (array[i + j] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                    return i; // ✅ return index where pattern starts
            }
            return -1; // ✅ not found
        }


        //public static int IndexOf(byte[] array, byte[] pattern)
        //{
        //    if (pattern.Length == 0) return 0;
        //    if (pattern.Length > array.Length) return -1;

        //    for (int i = 0; i <= array.Length - pattern.Length; i++)
        //    {
        //        bool found = true;
        //        for (int j = 0; j < pattern.Length; j++)
        //        {
        //            if (array[i + j] != pattern[j]) { found = false; break; }
        //        }
        //        if (found) return i;
        //    }
        //    return -1;
        //}

        private List<EventPayload> ProcessJsonDocument(JsonElement root)
        {
            var events = new List<EventPayload>();

            int? getInt(JsonElement e)
            {
                if (e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var n)) return n;
                if (e.ValueKind == JsonValueKind.String && int.TryParse(e.GetString(), out var s)) return s;
                return null;
            }

            var basePayload = new EventPayload
            {
                CameraName = root.TryGetProperty("CameraName", out var cam) ? cam.GetString() : string.Empty,
                Channel = root.TryGetProperty("Channel", out var ch) ? ch.GetString() : string.Empty,
                Gpu = root.TryGetProperty("Gpu", out var gpu) ? getInt(gpu) : null
            };

            if (root.TryGetProperty("Areas", out var areasArray) && areasArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var area in areasArray.EnumerateArray())
                {
                    var evt = new EventPayload
                    {
                        CameraName = basePayload.CameraName,
                        Channel = basePayload.Channel,
                        Gpu = basePayload.Gpu,
                        AreaName = area.TryGetProperty("AreaName", out var aName) ? aName.GetString() : string.Empty,
                        AnalyticType = area.TryGetProperty("AnalyticType", out var aType) ? aType.GetString() : string.Empty,
                        ObjectType = area.TryGetProperty("ObjectType", out var oType) ? oType.GetString() : string.Empty,
                        ObjectName = area.TryGetProperty("ObjectName", out var oName) ? oName.GetString() : string.Empty,
                        AccumulatedCount = area.TryGetProperty("AccumulatedCount", out var aCount) ? getInt(aCount) : null,
                        Alarmed = area.TryGetProperty("Alarmed", out var alarmed) ? alarmed.GetString() : string.Empty,
                        Quantity = area.TryGetProperty("Quantity", out var quantity) ? getInt(quantity) : null,
                        SnapshotUrl = area.TryGetProperty("Snapshot", out var snap1) ? snap1.GetString() : string.Empty,
                        SnapshotUrl2 = area.TryGetProperty("SnapshotUrl2", out var snap2) ? snap2.GetString() : string.Empty,
                    };
                    events.Add(evt);
                }
            }
            return events;
        }
    }

    public class IpxFilter
    {
        private readonly Dictionary<(string, string), int> _lastAccumulatedCount = new();
        private readonly Dictionary<(string, string, string), DateTime> _lastEventTime = new();
        private readonly ILogger _logger;
        private readonly TimeSpan _duplicateWindow = TimeSpan.FromSeconds(1);

        public IpxFilter()
        {
            _logger = LoggerFactory.Create(builder =>
            {
                builder.AddSimpleConsole(options =>
                {
                    options.IncludeScopes = false;
                    options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ";
                });
            }).CreateLogger<IpxFilter>();
        }

        /// <summary>
        /// Returns true if this event should be allowed (saved), false if filtered out.
        /// Backward-compatible method that calls ShouldAllowWithIncrement.
        /// </summary>
        public bool ShouldAllow(EventModel model)
        {
            var (allowed, _) = ShouldAllowWithIncrement(model);
            return allowed;
        }

        /// <summary>
        /// Returns (allowed, increment) where allowed indicates if event should be saved,
        /// and increment is the count increase (0 if same, negative if decreased, >1 if multiple simultaneous crossings).
        /// Implements the same filtering logic as the Python filter.py
        /// </summary>
        public (bool allowed, int increment) ShouldAllowWithIncrement(EventModel model)
        {
            var cam = model.CameraName;
            var area = model.AreaName;
            var atyp = model.AnalyticType?.Trim()?.ToLower();

            _logger.LogInformation("Filtering out  fields: cam={Cam}, area={Area}, type={Type}", cam, area, atyp);
            if (string.IsNullOrEmpty(cam) || string.IsNullOrEmpty(area) || string.IsNullOrEmpty(atyp))
            {
                _logger.LogDebug("Filtering out event with missing required fields: cam={Cam}, area={Area}, type={Type}", cam, area, atyp);
                return (false, 0);
            }

            // Exclude exclusion analytic type from being saved
            if (atyp == "exclusion")
            {
                _logger.LogDebug("Filtering out exclusion analytic type for {Cam}/{Area}", cam, area);
                return (false, 0);
            }

            var key = (cam, area);
            var timeKey = (cam, area, atyp);
            var currentTime = DateTime.UtcNow;
            _logger.LogInformation("Filtering out  fields: cam={Cam}, area={Area}, type={Type}", key, timeKey, currentTime);
            lock (_lastEventTime)
            {
                // Counting/Crossline analytics - handle count increases first to allow simultaneous crossings
                if (atyp == "crossline" || atyp == "counting" || atyp == "crossingline")
                {
                    if (!model.AccumulatedCount.HasValue)
                    {
                        _logger.LogWarning("Missing AccumulatedCount for counting event {Cam}/{Area}", cam, area);
                        return (false, 0);
                    }

                    var acc = model.AccumulatedCount.Value;
                    
                    // Skip if count is 0 (IPX restart scenario)
                    if (acc == 0)
                    {
                        _lastAccumulatedCount[key] = acc; // Update stored value but don't save
                        _logger.LogDebug("Skipping zero count for {Cam}/{Area} (IPX restart)", cam, area);
                        return (false, 0);
                    }

                    // Check count logic first (before duplicate window) to allow rapid sequential crossings
                    if (_lastAccumulatedCount.TryGetValue(key, out var lastAcc))
                    {
                        var increment = acc - lastAcc;
                        
                        // If count increased (by any amount), allow the event
                        // This handles both single crossings (acc = lastAcc + 1) and simultaneous crossings (acc > lastAcc + 1)
                        if (acc > lastAcc)
                        {
                            _lastAccumulatedCount[key] = acc;
                            _lastEventTime[timeKey] = currentTime;
                            if (increment > 1)
                            {
                                _logger.LogDebug("Count increase detected for {Cam}/{Area}: {LastAcc} -> {Acc} (increment: {Increment}, multiple simultaneous crossings)", 
                                    cam, area, lastAcc, acc, increment);
                            }
                            else
                            {
                                _logger.LogDebug("Count increase detected for {Cam}/{Area}: {LastAcc} -> {Acc} (single crossing)", cam, area, lastAcc, acc);
                            }
                            return (true, increment);
                        }
                        // If count decreased (unusual but possible - reset scenario), allow it
                        else if (acc < lastAcc)
                        {
                            _lastAccumulatedCount[key] = acc;
                            _lastEventTime[timeKey] = currentTime;
                            _logger.LogDebug("Count decrease detected for {Cam}/{Area}: {LastAcc} -> {Acc} (possible reset)", cam, area, lastAcc, acc);
                            return (true, increment);
                        }
                        // Same count - apply duplicate window check to prevent saving the same event multiple times
                        else
                        {
                            // Same count - check if we've seen this exact count recently
                            if (_lastEventTime.TryGetValue(timeKey, out var lastTime2))
                            {
                                var timeDiff = currentTime - lastTime2;
                                if (timeDiff < _duplicateWindow)
                                {
                                    _logger.LogDebug("Filtering out duplicate count event for {Cam}/{Area}/{Type} (same count {Acc}, too soon: {TimeDiff}ms)", 
                                        cam, area, atyp, acc, timeDiff.TotalMilliseconds);
                                    return (false, 0);
                                }
                            }
                            // Same count but outside duplicate window - might be a legitimate repeat or delayed event
                            // Update timestamp but don't save (to prevent duplicates)
                            _lastEventTime[timeKey] = currentTime;
                            _logger.LogDebug("Same count for {Cam}/{Area}: {Acc} (outside duplicate window, but filtering to prevent duplicates)", cam, area, acc);
                            return (false, 0); // Changed: don't save same count events to prevent duplicates
                        }
                    }
                    else
                    {
                        // First time seeing this camera/area
                        _lastAccumulatedCount[key] = acc;
                        _lastEventTime[timeKey] = currentTime;
                        _logger.LogDebug("First count for {Cam}/{Area}: {Acc}", cam, area, acc);
                        return (true, 1); // First count - treat as single event (we don't know the history)
                    }
                }

                // For non-counting events, apply duplicate window check first
                // Prevent functional duplicates - same camera/area/type within 1 second
                if (_lastEventTime.TryGetValue(timeKey, out var lastTime))
                {
                    var timeDiff = currentTime - lastTime;
                    if (timeDiff < _duplicateWindow)
                    {
                        _logger.LogDebug("Filtering out duplicate event for {Cam}/{Area}/{Type} (too soon: {TimeDiff}ms)", 
                            cam, area, atyp, timeDiff.TotalMilliseconds);
                        return (false, 0);
                    }
                }

                // Motion detection analytics - only store alarm events
                if (atyp == "moving" || atyp == "stopped/loitering" || atyp == "stopped" || atyp == "loitering")
                {
                    var alarmed = model.Alarmed?.Trim();
                    if (alarmed == "1")
                    {
                        _lastEventTime[timeKey] = currentTime;
                        _logger.LogDebug("Alarm triggered for {Cam}/{Area}: {Type}", cam, area, atyp);
                        return (true, 1); // Non-counting events have increment of 1
                    }
                    else
                    {
                        _logger.LogDebug("No alarm for {Cam}/{Area}: {Type} (Alarmed={Alarmed})", cam, area, atyp, alarmed);
                        return (false, 0);
                    }
                }
                // Presence detection analytics - only store when objects detected
                if (atyp == "presence" || atyp == "nonpresence" || atyp == "detection")
                {
                    if (!model.Quantity.HasValue)
                    {
                        _logger.LogWarning("Missing Quantity for presence event {Cam}/{Area}", cam, area);
                        return (false, 0);
                    }

                    var quantity = model.Quantity.Value;
                    if (quantity > 0)
                    {
                        _lastEventTime[timeKey] = currentTime;
                        _logger.LogDebug("Objects detected for {Cam}/{Area}: {Type} (Quantity={Quantity})", cam, area, atyp, quantity);
                        return (true, 1); // Non-counting events have increment of 1
                    }
                    else
                    {
                        _logger.LogDebug("No objects for {Cam}/{Area}: {Type}", cam, area, atyp);
                        return (false, 0);
                    }
                }
                // Unknown analytic type - log and store with duplicate prevention
                _logger.LogWarning("Unknown analytic type '{Type}' for {Cam}/{Area} - storing with duplicate prevention", atyp, cam, area);
                _lastEventTime[timeKey] = currentTime;
                return (true, 1); // Unknown types have increment of 1
            }
        }
    }

}

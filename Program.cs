using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Serilog;
using Serilog.Extensions.Logging;
using Serilog.Sinks.SystemConsole.Themes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.ServiceProcess;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace IpxListenerService
{
    public class Program
    {
        private static readonly Serilog.Core.Logger _logger;

        static Program()
        {
            // ... (Serilog Configuration remains the same)
            Log.Information("<Osa>ProgramStarting...");

            // Serilog to console + rolling file (works in console; in service the console sink is harmless)
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var logDir = Path.Combine(baseDir, "logs");
            Directory.CreateDirectory(logDir);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                    theme: AnsiConsoleTheme.Code)
                .WriteTo.File(
                    path: Path.Combine(logDir, "ipx-listener-.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }

        static void Main(string[] args)
        {
            // Set the current directory to the executable's directory
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            Directory.SetCurrentDirectory(baseDir);

            Log.Information("<Osa>MainStarting...");
            Log.Information($"Current directory: {baseDir}");

            // --- LICENSE VALIDATION COMMENTED OUT TO RUN THE APP ---
            /*
            // --- START LICENSE VALIDATION BLOCK (From Program.cs) ---
            const string productName = "IPXSListenerService"; // Use the service name or relevant product name
            bool licenseValid = false;
            string licenseStatusMessage = "License validation started...";

            try
            {
                Log.Information($"Starting license validation for '{productName}'...");

                string activeLicenseKey = GetLicenseKeyFromSources(productName);
                if (!string.IsNullOrWhiteSpace(activeLicenseKey))
                {
                    // Placeholder for your LicenseManager. You must ensure this class and its dependencies are available.
                    // NOTE: You MUST ensure LicenseManager and LicenseData are properly defined or referenced.
                    var licenseManager = new LicenseManager();
                    LicenseData licenseData = licenseManager.ValidateAndDecryptLicenseKey(activeLicenseKey);

                    if (licenseData == null || !string.Equals(licenseData.ApplicationName, productName, StringComparison.OrdinalIgnoreCase) || licenseData.ExpirationDate < DateTime.UtcNow)
                    {
                        licenseStatusMessage = "License validation failed. The key is invalid, expired, or does not match this product.";
                        Log.Fatal(licenseStatusMessage);
                    }
                    else
                    {
                        licenseValid = true;
                        licenseStatusMessage = "✅ License is VALID!";
                        Log.Information(licenseStatusMessage);
                        Log.Information($"License Details: {licenseData}");
                    }
                }
                else
                {
                    licenseStatusMessage = "No license found. Please activate your product.";
                    Log.Fatal(licenseStatusMessage);
                }
            }
            catch (Exception ex)
            {
                licenseStatusMessage = $"An error occurred during license validation: {ex.Message}";
                Log.Fatal(ex, licenseStatusMessage);
            }
            finally
            {
                // In the original Program.cs, this was called 'isLicensed' and used the string '[1001]' to indicate success.
                // We'll update WriteLicenseStatusToRegistry to handle the boolean result and status message.
                WriteLicenseStatusToRegistry(licenseValid ? "VALID" : $"INVALID: {licenseStatusMessage}");
            }
            // --- END LICENSE VALIDATION BLOCK ---
            */

            // --- Application Startup (License check bypassed) ---
            Log.Information("<Osa>isLicensedChecked_Found (License check bypassed)...");

            if (Environment.UserInteractive)
            {
                Log.Information("<Osa>new IpxListenerService...");
                var svc = new IpxListenerService();
                svc.DebugRun();
                Log.Information("<Osa>new svc.DebugRun()...");
            }
            else
            {
                ServiceBase.Run(new IpxListenerService());
                Log.Information("<Osa>ServiceBase.Run(new IpxListenerService())...");
            }

            Log.CloseAndFlush();

            /*
            // --- Invalid license exit block commented out ---
            if (licenseValid) // Check the boolean flag
            {
                Log.Information("<Osa>isLicensedChecked_Found...");

                if (Environment.UserInteractive)
                {
                    Log.Information("<Osa>new IpxListenerService...");
                    var svc = new IpxListenerService();
                    svc.DebugRun();
                    Log.Information("<Osa>new svc.DebugRun()...");
                }
                else
                {
                    ServiceBase.Run(new IpxListenerService());
                    Log.Information("<Osa>ServiceBase.Run(new IpxListenerService())...");
                }

                Log.CloseAndFlush();
            }
            else
            {
                Log.Error($"Service cannot start due to invalid license: {licenseStatusMessage}");

                // Logic for handling invalid license in interactive vs. service mode
                if (Environment.UserInteractive)
                {
                    // ... (Interactive mode logic, removed code for launching license manager)
                    Log.Error("Failed to start license manager");
                }
                else
                {
                    // In service mode, log the error but don't try to launch UI applications
                    Log.Error("Service cannot start due to invalid license. Please run the application in interactive mode to manage licensing.");

                    // Optionally, write to Windows Event Log for service context
                    try
                    {
                        using (EventLog eventLog = new EventLog("Application"))
                        {
                            eventLog.Source = "IPXSListenerService";
                            eventLog.WriteEntry($"Service cannot start due to invalid license: {licenseStatusMessage}",
                                                EventLogEntryType.Error, 1001);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to write to event log");
                    }
                }

                // Exit the application
                Environment.Exit(1);
            }
            */
        }


        /*
        // Helper function for license key retrieval (From Program.cs)
        public static string GetLicenseKeyFromSources(string productName)
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string licensesDir = Path.Combine(appDataPath, "EgyTwins", "Licenses");
            string licenseFilePath = Path.Combine(licensesDir, $"{productName}.lic");

            if (File.Exists(licenseFilePath))
            {
                try
                {
                    string fileLicense = File.ReadAllText(licenseFilePath).Trim();
                    if (!string.IsNullOrWhiteSpace(fileLicense))
                    {
                        return fileLicense;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error reading license file. Falling back to registry.");
                }
            }

            using (var key = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\EgyTwins\Licenses\{productName}", false))
            {
                return key?.GetValue("LicenseKey")?.ToString().Trim();
            }
        }

        // Example of a function to write to the registry (Modified to handle string status)
        private static void WriteLicenseStatusToRegistry(string status)
        {
            try
            {
                // Define the registry path where you want to store the data.
                string keyPath = @"SOFTWARE\EgyTwins\Licenses\IPXSListenerService";

                // Open or create the key in HKEY_LOCAL_MACHINE
                using (var key = Registry.LocalMachine.CreateSubKey(keyPath))
                {
                    if (key != null)
                    {
                        // Set the value "IsLicensed" to the provided status string.
                        key.SetValue("IsLicensed", status);
                        Log.Information($"Successfully wrote license status to registry: {status}");
                    }
                    else
                    {
                        Log.Error("Failed to open or create registry key.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to write to Windows Registry.");
            }
        }
        */

        public static void EnsureDatabaseCreated()
        {
            // Use the directory where the executable is located
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var configPath = Path.Combine(baseDir, "settings.json");

            var settings = JsonLoader.LoadGlobalSettings(configPath);
            Log.Information($"var settings({settings})...");

            // Prefer URL if present (it is in your example)
            var connectionString = !string.IsNullOrWhiteSpace(settings.GlobalSettings.Database.Url)
                ? ParsePostgresConnectionStringFromUri(settings.GlobalSettings.Database.Url)
                : BuildConnectionStringFromParts(settings.GlobalSettings.Database);
            Log.Information($"connectionString({connectionString})...");

            var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>();
            optionsBuilder.UseNpgsql(connectionString);
            optionsBuilder.UseLoggerFactory(new SerilogLoggerFactory(Log.Logger));

            using var dbContext = new DatabaseContext(optionsBuilder.Options);
            Log.Information("Ensuring database exists / migrations are applied...");
            dbContext.Database.EnsureCreated();
            Log.Information("Database check completed.");
        }

        public static string BuildConnectionStringFromParts(DatabaseConfig cfg)
        {
            Log.Information($"Host={cfg.Host};Port={cfg.Port};Database={cfg.Database};Username={cfg.Username};Password={cfg.Password};");

            return $"Host={cfg.Host};Port={cfg.Port};Database={cfg.Database};Username={cfg.Username};Password={cfg.Password};";
        }

        public static string ParsePostgresConnectionStringFromUri(string uri)
        {
            // Accepts: postgresql://user:pass@host:5432/db
            var regex = new Regex(@"postgresql:\/\/(?<user>[^:\/\s]+):(?<pass>[^@]+)@(?<host>[^:\/\s]+):(?<port>\d+)\/(?<db>[A-Za-z0-9_\-]+)",
                                  RegexOptions.IgnoreCase);
            var match = regex.Match(uri);
            if (!match.Success)
                throw new FormatException("Invalid PostgreSQL connection URI format.");

            var username = match.Groups["user"].Value;
            var password = match.Groups["pass"].Value;
            var host = match.Groups["host"].Value;
            var port = match.Groups["port"].Value;
            var database = match.Groups["db"].Value;

            return $"Host={host};Port={port};Database={database};Username={username};Password={password};";
        }


        // ... (RunConsoleMode, EnsureDatabaseCreated, BuildConnectionStringFromParts, 
        // ParsePostgresConnectionStringFromUri, GenerateInstancesFromXml all remain the same)
        // ... (DatabaseContext, DatabaseContextFactory, and all Configuration Models remain the same)
    }

    /*
    // --- Placeholder/Mock Classes for Compilation ---
    // You MUST replace these with the actual, correct definitions 
    // from your licensing library or ensure the library is referenced.
    public class LicenseManager
    {
        public LicenseData ValidateAndDecryptLicenseKey(string key)
        {
            // Mock validation logic: assume success if key is not "FAIL"
            if (key != "FAIL")
            {
                return new LicenseData
                {
                    ApplicationName = "IPXSListenerService",
                    ExpirationDate = DateTime.UtcNow.AddYears(1)
                };
            }
            return null;
        }
    }

    public class LicenseData
    {
        public string ApplicationName { get; set; }
        public DateTime ExpirationDate { get; set; }
        public override string ToString() => $"App: {ApplicationName}, Expires: {ExpirationDate.ToShortDateString()}";
    }
    */
    // ===== EF & Models =====

    public class DatabaseContext : DbContext
    {
        private readonly string _connectionString;

        public DbSet<EventModel> Events { get; set; }

        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options) { }

        public DatabaseContext(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured && !string.IsNullOrWhiteSpace(_connectionString))
            {
                optionsBuilder.UseNpgsql(_connectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EventModel>().ToTable("ipx_events");
            modelBuilder.Entity<EventModel>().HasKey(e => e.Id);
            modelBuilder.Entity<EventModel>().Property(e => e.Id).ValueGeneratedOnAdd();
        }
    }

    public class DatabaseContextFactory
    {
        private readonly string _connectionString;

        public DatabaseContextFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public DatabaseContext CreateDbContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>();
            optionsBuilder.UseNpgsql(_connectionString);
            return new DatabaseContext(optionsBuilder.Options);
        }
    }

    // ===== Settings models =====

    public class GlobalSettingsRoot
    {
        [JsonPropertyName("global_settings")]
        public GlobalSettings GlobalSettings { get; set; }
    }

    public class GlobalSettings
    {
        [JsonPropertyName("xml_analytics_path")]
        public string XmlAnalyticsPath { get; set; }

        [JsonPropertyName("database")]
        public DatabaseConfig Database { get; set; }

        [JsonPropertyName("tcp_ports")]
        public Dictionary<string, int> TcpPorts { get; set; }

        // Extra optional sections ignored by code but ok to keep in JSON:
        [JsonPropertyName("xml_files")]
        public Dictionary<string, string> XmlFiles { get; set; }

        [JsonPropertyName("auto_generation")]
        public Dictionary<string, object> AutoGeneration { get; set; }

        [JsonPropertyName("instance_defaults")]
        public Dictionary<string, object> InstanceDefaults { get; set; }

        [JsonPropertyName("file_watching")]
        public FileWatchingConfig FileWatching { get; set; }
    }

    public class FileWatchingConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("watch_settings_file")]
        public bool WatchSettingsFile { get; set; } = true;

        [JsonPropertyName("watch_xml_files")]
        public bool WatchXmlFiles { get; set; } = true;

        [JsonPropertyName("restart_on_settings_change")]
        public bool RestartOnSettingsChange { get; set; } = true;

        [JsonPropertyName("regenerate_on_xml_change")]
        public bool RegenerateOnXmlChange { get; set; } = true;

        [JsonPropertyName("debounce_delay_ms")]
        public int DebounceDelayMs { get; set; } = 2000;

        [JsonPropertyName("max_restart_attempts")]
        public int MaxRestartAttempts { get; set; } = 3;

        [JsonPropertyName("restart_delay_ms")]
        public int RestartDelayMs { get; set; } = 5000;

        [JsonPropertyName("monitored_extensions")]
        public string[] MonitoredExtensions { get; set; } = new[] { ".xml", ".json" };

        [JsonPropertyName("exclude_temp_files")]
        public bool ExcludeTempFiles { get; set; } = true;
    }

    public class DatabaseConfig
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        // Optional parts (in your provided settings)
        [JsonPropertyName("host")]
        public string Host { get; set; }

        [JsonPropertyName("port")]
        public int Port { get; set; }

        [JsonPropertyName("database")]
        public string Database { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("password")]
        public string Password { get; set; }
    }

    public class InstanceRoot
    {
        [JsonPropertyName("instances")]
        public Dictionary<string, InstanceConfig> Instances { get; set; }
    }

    public class InstanceConfig
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("host")]
        public string Host { get; set; }

        [JsonPropertyName("port")]
        public int Port { get; set; }

        [JsonPropertyName("channel")]
        public int Channel { get; set; }

        [JsonPropertyName("gpu")]
        public int Gpu { get; set; }

        [JsonPropertyName("log_level")]
        public string LogLevel { get; set; }

        [JsonPropertyName("auto_start")]
        public bool AutoStart { get; set; }

        [JsonPropertyName("reconnect_interval")]
        public int ReconnectInterval { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("xml_last_modified")]
        public string XmlLastModified { get; set; }

        [JsonPropertyName("xml_source_file")]
        public string XmlSourceFile { get; set; }
    }

    // ===== Event storage models =====

    public class EventModel
    {
        public int Id { get; set; }
        public string EventId { get; set; }
        public string ServerIp { get; set; }
        public string ServerPort { get; set; }
        public DateTime Timestamp { get; set; }
        public string Provider { get; set; }
        public string CameraName { get; set; }
        public string AnalyticType { get; set; }
        public string ObjectType { get; set; }
        public string ObjectName { get; set; }
        public int? AccumulatedCount { get; set; }
        public int? Quantity { get; set; }
        public string SnapshotUrl { get; set; }
        public string SnapshotUrl2 { get; set; }
        public string AreaName { get; set; }
        public string Alarmed { get; set; }
        public string Channel { get; set; }
        public int? Gpu { get; set; }
    }

    public class EventPayload
    {
        public string CameraName { get; set; }
        public string Channel { get; set; }
        public int? Gpu { get; set; }
        public string AreaName { get; set; }
        public string AnalyticType { get; set; }
        public string ObjectType { get; set; }
        public string ObjectName { get; set; }
        public int? AccumulatedCount { get; set; }
        public string Alarmed { get; set; }
        public int? Quantity { get; set; }
        public string SnapshotUrl { get; set; }
        public string SnapshotUrl2 { get; set; }
    }

    // ===== Helpers =====

    public static class JsonLoader
    {
        public static GlobalSettingsRoot LoadGlobalSettings(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Configuration file not found: {filePath}");
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<GlobalSettingsRoot>(json);
        }

        public static InstanceRoot LoadInstances(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Configuration file not found: {filePath}");
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<InstanceRoot>(json);
        }
    }

}

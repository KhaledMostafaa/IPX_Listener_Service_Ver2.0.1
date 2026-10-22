using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Sinks.SystemConsole.Themes;
using IpxListenerService.Common;
using IpxListenerService.Data;
using IpxListenerService.Models;
using IpxListenerService.Services;

namespace IpxListenerService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            Directory.SetCurrentDirectory(baseDir);

            var logDir = Path.Combine(baseDir, "logs");
            Directory.CreateDirectory(logDir);

            // Configure Serilog with structured Console and Rolling Log Files
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                    theme: AnsiConsoleTheme.Code)
                // General rolling log file (Daily, 50MB limit per file, 31 days retention)
                .WriteTo.File(
                    path: Path.Combine(logDir, "ipx-listener-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 31,
                    fileSizeLimitBytes: 52428800,
                    rollOnFileSizeLimit: true,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                // Dedicated error rolling log file for warnings and errors only
                .WriteTo.File(
                    path: Path.Combine(logDir, "ipx-errors-.log"),
                    restrictedToMinimumLevel: LogEventLevel.Warning,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 31,
                    fileSizeLimitBytes: 52428800,
                    rollOnFileSizeLimit: true,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            try
            {
                Log.Information("<Osa>Starting IPX Listener Web API host...");
                Log.Information($"Current directory: {baseDir}");

                var builder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    Args = args,
                    ContentRootPath = baseDir
                });

                // Support running as Windows Service when launched by Service Control Manager
                builder.Host.UseWindowsService();

                // Hook Serilog into ASP.NET Core logging
                builder.Host.UseSerilog();

                // Add Controllers
                builder.Services.AddControllers()
                    .AddJsonOptions(options =>
                    {
                        options.JsonSerializerOptions.WriteIndented = true;
                    });

                // Add Swagger/OpenAPI documentation
                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                    {
                        Title = "IPX Listener Service API",
                        Version = "v2.0.1",
                        Description = "IPX Salient Listener Service Web API - Health Monitoring, Instance Management & Log Files Explorer"
                    });
                });

                // Register IpxListenerService as Singleton and as IHostedService background worker
                builder.Services.AddSingleton<IpxListenerService>();
                builder.Services.AddHostedService(sp => sp.GetRequiredService<IpxListenerService>());

                // Register HealthCheckService & LogFileService
                builder.Services.AddSingleton<HealthCheckService>();
                builder.Services.AddSingleton<ILogFileService, LogFileService>();

                // Register DatabaseContext in DI
                builder.Services.AddDbContext<DatabaseContext>((sp, options) =>
                {
                    var connStr = GetPostgresConnectionString();
                    options.UseNpgsql(connStr);
                });

                var app = builder.Build();

                // Attempt to ensure database is created and all pending migrations are applied on startup
                // If PostgreSQL is not reachable, retry with backoff and allow Web API to stay online
                var dbReady = EnsureDatabaseCreated(maxRetries: 3, delaySeconds: 2);
                if (!dbReady)
                {
                    Log.Warning("PostgreSQL is not reachable on startup. The Web API will stay online so you can check logs and health via /swagger or /api/logs. Background worker will retry database initialization.");
                }

                // Enable Swagger UI
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "IPX Listener API v2.0.1");
                    c.RoutePrefix = "swagger";
                });

                // Redirect root to swagger
                app.MapGet("/", () => Results.Redirect("/swagger"));

                app.UseRouting();
                app.UseAuthorization();
                app.MapControllers();

                Log.Information("IPX Listener Web API started. Swagger UI available at /swagger");
                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static bool EnsureDatabaseCreated(int maxRetries = 3, int delaySeconds = 2)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var configPath = Path.Combine(baseDir, "settings.json");

            GlobalSettingsRoot? settings = null;
            if (File.Exists(configPath))
            {
                try
                {
                    settings = JsonLoader.LoadGlobalSettings(configPath);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to load settings.json for database config");
                }
            }

            var connectionString = GetPostgresConnectionString(settings?.GlobalSettings?.Database);
            Log.Information($"Database ConnectionString resolved: {MaskConnectionString(connectionString)}");

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>();
                    optionsBuilder.UseNpgsql(connectionString);
                    optionsBuilder.UseLoggerFactory(new SerilogLoggerFactory(Log.Logger));

                    using var dbContext = new DatabaseContext(optionsBuilder.Options);
                    Log.Information($"Checking database and applying migrations (attempt {attempt}/{maxRetries})...");

                    var pendingMigrations = dbContext.Database.GetPendingMigrations().ToList();
                    if (pendingMigrations.Count > 0)
                    {
                        Log.Information($"Found {pendingMigrations.Count} pending migration(s): {string.Join(", ", pendingMigrations)}");
                        Log.Information("Applying migrations...");
                    }
                    else
                    {
                        Log.Information("No pending migrations found. Ensuring database schema is up-to-date...");
                    }

                    dbContext.Database.Migrate();
                    Log.Information("Database check and migration completed successfully.");
                    return true;
                }
                catch (Exception ex)
                {
                    if (attempt < maxRetries)
                    {
                        Log.Warning($"Database connection attempt {attempt} failed: {ex.Message}. Retrying in {delaySeconds}s...");
                        Thread.Sleep(delaySeconds * 1000);
                    }
                    else
                    {
                        Log.Error(ex, $"Failed to connect to database or apply migrations after {maxRetries} attempt(s). Target: {MaskConnectionString(connectionString)}. Check if PostgreSQL service is running and accessible on 127.0.0.1:5432.");
                        return false;
                    }
                }
            }

            return false;
        }

        public static string MaskConnectionString(string connStr)
        {
            if (string.IsNullOrWhiteSpace(connStr))
                return string.Empty;

            var masked = Regex.Replace(connStr, @"(Password\s*=\s*)([^;]+)", "$1********", RegexOptions.IgnoreCase);
            masked = Regex.Replace(masked, @"://([^:]+):([^@]+)@", "://$1:********@");
            return masked;
        }

        public static string NormalizePostgresConnectionString(string connectionStringOrUri)
        {
            if (string.IsNullOrWhiteSpace(connectionStringOrUri))
                return string.Empty;

            var trimmed = connectionStringOrUri.Trim();
            if (trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
            {
                return ParsePostgresConnectionStringFromUri(trimmed);
            }

            return trimmed;
        }

        public static string GetPostgresConnectionString(DatabaseConfig? cfg = null, IConfiguration? appConfig = null)
        {
            // 1. Check settings.json connection_string
            if (cfg != null && !string.IsNullOrWhiteSpace(cfg.ConnectionString))
            {
                return NormalizePostgresConnectionString(cfg.ConnectionString);
            }

            // 2. Check appsettings.json DefaultConnection via appConfig or direct read
            var appSettingsConn = appConfig?.GetConnectionString("DefaultConnection") ?? GetAppsettingsConnectionString();
            if (!string.IsNullOrWhiteSpace(appSettingsConn))
            {
                return NormalizePostgresConnectionString(appSettingsConn);
            }

            // 3. Check settings.json url
            if (cfg != null && !string.IsNullOrWhiteSpace(cfg.Url))
            {
                return NormalizePostgresConnectionString(cfg.Url);
            }

            // 4. Check discrete parts
            if (cfg != null && !string.IsNullOrWhiteSpace(cfg.Host) && !string.IsNullOrWhiteSpace(cfg.Database))
            {
                return BuildConnectionStringFromParts(cfg);
            }

            // 5. Default fallback
            return "Host=127.0.0.1;Port=5432;Database=ipx_events_beta;Username=postgres;Password=admin;";
        }

        private static string? GetAppsettingsConnectionString()
        {
            try
            {
                var appsettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(appsettingsPath))
                {
                    var json = File.ReadAllText(appsettingsPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("ConnectionStrings", out var csSection) &&
                        csSection.TryGetProperty("DefaultConnection", out var defConn))
                    {
                        var str = defConn.GetString();
                        if (!string.IsNullOrWhiteSpace(str))
                            return str;
                    }
                }
            }
            catch
            {
                // Ignore fallback errors
            }
            return null;
        }

        public static string BuildConnectionStringFromParts(DatabaseConfig cfg)
        {
            var host = !string.IsNullOrWhiteSpace(cfg.Host) ? cfg.Host : "127.0.0.1";
            var port = cfg.Port > 0 ? cfg.Port : 5432;
            var database = !string.IsNullOrWhiteSpace(cfg.Database) ? cfg.Database : "ipx_events_beta";
            var username = !string.IsNullOrWhiteSpace(cfg.Username) ? cfg.Username : "postgres";
            var password = cfg.Password ?? "";

            Log.Information($"Host={host};Port={port};Database={database};Username={username};Password=********;");

            return $"Host={host};Port={port};Database={database};Username={username};Password={password};";
        }

        public static string ParsePostgresConnectionStringFromUri(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri))
                return string.Empty;

            var trimmed = uri.Trim();
            if (!trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
            {
                // Already in standard connection string format
                return trimmed;
            }

            // Accepts: postgresql://user:pass@host:5432/db
            var regex = new Regex(@"postgre(?:sql)?:\/\/(?<user>[^:\/\s]+):(?<pass>[^@]+)@(?<host>[^:\/\s]+):(?<port>\d+)\/(?<db>[A-Za-z0-9_\-]+)",
                                  RegexOptions.IgnoreCase);
            var match = regex.Match(trimmed);
            if (!match.Success)
                throw new FormatException($"Invalid PostgreSQL connection URI format: {uri}");

            var username = match.Groups["user"].Value;
            var password = match.Groups["pass"].Value;
            var host = match.Groups["host"].Value;
            var port = match.Groups["port"].Value;
            var database = match.Groups["db"].Value;

            return $"Host={host};Port={port};Database={database};Username={username};Password={password};";
        }
    }
}

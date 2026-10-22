using System.Text.Json.Serialization;

namespace IpxListenerService.Models
{
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
}

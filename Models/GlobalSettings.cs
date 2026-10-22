using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace IpxListenerService.Models
{
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

        [JsonPropertyName("xml_files")]
        public Dictionary<string, string> XmlFiles { get; set; }

        [JsonPropertyName("auto_generation")]
        public Dictionary<string, object> AutoGeneration { get; set; }

        [JsonPropertyName("instance_defaults")]
        public Dictionary<string, object> InstanceDefaults { get; set; }

        [JsonPropertyName("file_watching")]
        public FileWatchingConfig FileWatching { get; set; }
    }
}

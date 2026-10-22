using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace IpxListenerService.Models
{
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
}

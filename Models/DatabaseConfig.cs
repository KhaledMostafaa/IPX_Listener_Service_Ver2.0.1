using System.Text.Json.Serialization;

namespace IpxListenerService.Models
{
    public class DatabaseConfig
    {
        [JsonPropertyName("connection_string")]
        public string? ConnectionString { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("host")]
        public string? Host { get; set; }

        [JsonPropertyName("port")]
        public int Port { get; set; }

        [JsonPropertyName("database")]
        public string? Database { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("password")]
        public string? Password { get; set; }
    }
}

using System.IO;
using System.Text.Json;
using IpxListenerService.Models;

namespace IpxListenerService.Common
{
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

using System.IO;
using App.Core.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace App.Infrastructure.Configuration
{
    /// <summary>
    /// Loads/saves the portable config.json next to the exe. Creates a default file on first run.
    /// Migration-by-schemaVersion and automatic backups land in a later phase.
    /// </summary>
    public class ConfigService
    {
        private readonly string _configFilePath;

        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            Converters = { new StringEnumConverter() }
        };

        public ConfigService(string configFilePath = null)
        {
            _configFilePath = configFilePath ?? PortablePaths.ConfigFilePath;
        }

        public AppConfig LoadOrCreateDefault()
        {
            if (!File.Exists(_configFilePath))
            {
                AppConfig defaultConfig = AppConfig.CreateDefault();
                Save(defaultConfig);
                return defaultConfig;
            }

            string json = File.ReadAllText(_configFilePath);
            return JsonConvert.DeserializeObject<AppConfig>(json, SerializerSettings) ?? AppConfig.CreateDefault();
        }

        public void Save(AppConfig config)
        {
            string json = JsonConvert.SerializeObject(config, SerializerSettings);
            File.WriteAllText(_configFilePath, json);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using App.Core.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace App.Infrastructure.Configuration
{
    /// <summary>
    /// Loads/saves the portable config.json next to the exe. Creates a default file on first
    /// run, migrates older schemas via ConfigMigrator, and keeps a retained set of timestamped
    /// backups so a bad save (or a bad migration) is always recoverable.
    /// </summary>
    public class ConfigService
    {
        private const int BackupsToRetain = 10;

        private readonly string _configFilePath;

        internal static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
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
            JObject root = JObject.Parse(json);
            int originalVersion = (int?)root["SchemaVersion"] ?? 1;

            JObject migrated = ConfigMigrator.MigrateToCurrent(root);
            AppConfig config = migrated.ToObject<AppConfig>(JsonSerializer.Create(SerializerSettings)) ?? AppConfig.CreateDefault();

            if (originalVersion < SchemaVersions.Current)
            {
                // Migration changed the on-disk shape — persist it now (backing up the pre-migration file first).
                Save(config);
            }

            return config;
        }

        public void Save(AppConfig config)
        {
            BackupCurrentConfig();

            string json = JsonConvert.SerializeObject(config, SerializerSettings);
            File.WriteAllText(_configFilePath, json);
        }

        private void BackupCurrentConfig()
        {
            if (!File.Exists(_configFilePath))
            {
                return;
            }

            string directory = Path.GetDirectoryName(_configFilePath) ?? ".";
            string backupPath = Path.Combine(directory, $"config.backup.{DateTime.UtcNow:yyyyMMddHHmmssfff}.json");
            File.Copy(_configFilePath, backupPath, overwrite: true);

            PruneOldBackups(directory);
        }

        private static void PruneOldBackups(string directory)
        {
            var backups = Directory.GetFiles(directory, "config.backup.*.json")
                .OrderByDescending(path => path)
                .Skip(BackupsToRetain);

            foreach (string oldBackup in backups)
            {
                File.Delete(oldBackup);
            }
        }
    }
}

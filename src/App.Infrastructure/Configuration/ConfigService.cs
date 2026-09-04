using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using App.Core.Configuration;
using App.Core.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace App.Infrastructure.Configuration
{
    /// <summary>
    /// Loads/saves the portable configuration next to the exe — split across two files so
    /// Settings and Folders/Rules can be saved (and backed up) independently:
    /// general.config.json ("Settings") and folders.config.json ("Folders"). Both still migrate
    /// via ConfigMigrator and keep a retained set of timestamped backups, same as the single
    /// config.json did before this split.
    /// A pre-split config.json is migrated into the two new files on first load and then removed
    /// (a ".pre-split.bak" copy is left behind first, so it's still recoverable).
    /// </summary>
    public class ConfigService
    {
        private const int BackupsToRetain = 10;

        private readonly string _configDirectory;

        internal static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            Converters = { new StringEnumConverter() }
        };

        public ConfigService(string configDirectory = null)
        {
            _configDirectory = configDirectory ?? PortablePaths.BaseDirectory;
        }

        private string GeneralConfigPath => Path.Combine(_configDirectory, "general.config.json");
        private string FoldersConfigPath => Path.Combine(_configDirectory, "folders.config.json");
        private string LegacyConfigPath => Path.Combine(_configDirectory, "config.json");

        public AppConfig LoadOrCreateDefault()
        {
            bool generalExists = File.Exists(GeneralConfigPath);
            bool foldersExist = File.Exists(FoldersConfigPath);

            if (!generalExists && !foldersExist && File.Exists(LegacyConfigPath))
            {
                return MigrateLegacyConfigFile();
            }

            bool foldersMigrated = false;
            var config = new AppConfig
            {
                Settings = generalExists ? ReadSettings() : new AppSettings(),
                Folders = foldersExist ? ReadFolders(out foldersMigrated) : new ObservableCollection<WatchedFolder>()
            };

            if (!generalExists)
            {
                SaveSettings(config);
            }
            if (!foldersExist || foldersMigrated)
            {
                // Either brand new, or an older schema that ConfigMigrator just upgraded in memory —
                // persist the upgraded shape now (backing up the pre-migration file first) so this
                // migration only ever runs once.
                SaveFolders(config);
            }

            return config;
        }

        /// <summary>Saves both files. Use SaveSettings/SaveFolders instead when only one half
        /// actually changed, so the backup-on-settings-change toggle and the "always back up
        /// folders/rules" behavior each apply only to the file that was actually touched.</summary>
        public void Save(AppConfig config)
        {
            SaveSettings(config);
            SaveFolders(config);
        }

        public void SaveSettings(AppConfig config)
        {
            if (config.Settings.BackupOnSettingsChange)
            {
                BackupFile(GeneralConfigPath, "general.config");
            }

            WriteJson(new { SchemaVersion = SchemaVersions.Current, config.Settings }, GeneralConfigPath);
        }

        public void SaveFolders(AppConfig config)
        {
            BackupFile(FoldersConfigPath, "folders.config");
            WriteJson(new { SchemaVersion = SchemaVersions.Current, config.Folders }, FoldersConfigPath);
        }

        private AppSettings ReadSettings()
        {
            JObject root = ParseJObject(GeneralConfigPath);
            return root["Settings"]?.ToObject<AppSettings>(JsonSerializer.Create(SerializerSettings)) ?? new AppSettings();
        }

        private ObservableCollection<WatchedFolder> ReadFolders(out bool wasMigrated)
        {
            JObject root = ParseJObject(FoldersConfigPath);
            int originalVersion = (int?)root["SchemaVersion"] ?? 1;

            JObject migrated = ConfigMigrator.MigrateToCurrent(root);
            wasMigrated = originalVersion < SchemaVersions.Current;

            return migrated["Folders"]?.ToObject<ObservableCollection<WatchedFolder>>(JsonSerializer.Create(SerializerSettings))
                ?? new ObservableCollection<WatchedFolder>();
        }

        private AppConfig MigrateLegacyConfigFile()
        {
            JObject root = ParseJObject(LegacyConfigPath);
            JObject migrated = ConfigMigrator.MigrateToCurrent(root);
            AppConfig config = migrated.ToObject<AppConfig>(JsonSerializer.Create(SerializerSettings)) ?? AppConfig.CreateDefault();

            SaveSettings(config);
            SaveFolders(config);

            File.Copy(LegacyConfigPath, LegacyConfigPath + ".pre-split.bak", overwrite: true);
            File.Delete(LegacyConfigPath);

            return config;
        }

        /// <summary>
        /// Reads and parses a config file, turning a corrupt/truncated/hand-edited-wrong JSON
        /// file into a clear, actionable exception instead of letting an unhandled
        /// JsonReaderException crash the whole app at startup (confirmed to do exactly that
        /// before this existed). ConfigService always writes valid JSON itself — this only
        /// triggers for a file damaged after the fact (manual edit, disk issue, interrupted write).
        /// </summary>
        private static JObject ParseJObject(string filePath)
        {
            try
            {
                return JObject.Parse(File.ReadAllText(filePath));
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"FldrSrtr kan het configuratiebestand niet lezen — het is geen geldige JSON:\n{filePath}\n\n" +
                    "Herstel een werkende versie uit een backup in dezelfde map " +
                    "(bv. general.config.backup.*.json of folders.config.backup.*.json), of verwijder " +
                    "het bestand om opnieuw te beginnen met de standaardinstellingen.",
                    ex);
            }
        }

        private void BackupFile(string filePath, string backupPrefix)
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            string directory = Path.GetDirectoryName(filePath) ?? ".";
            string backupPath = Path.Combine(directory, $"{backupPrefix}.backup.{DateTime.UtcNow:yyyyMMddHHmmssfff}.json");
            File.Copy(filePath, backupPath, overwrite: true);

            PruneOldBackups(directory, backupPrefix);
        }

        private static void PruneOldBackups(string directory, string backupPrefix)
        {
            var backups = Directory.GetFiles(directory, $"{backupPrefix}.backup.*.json")
                .OrderByDescending(path => path)
                .Skip(BackupsToRetain);

            foreach (string oldBackup in backups)
            {
                File.Delete(oldBackup);
            }
        }

        private static void WriteJson<T>(T value, string filePath)
        {
            string json = JsonConvert.SerializeObject(value, SerializerSettings);
            File.WriteAllText(filePath, json);
        }
    }
}

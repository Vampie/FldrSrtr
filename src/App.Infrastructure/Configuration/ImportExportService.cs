using System;
using System.IO;
using App.Core.Configuration;
using App.Core.Model;
using Newtonsoft.Json;

namespace App.Infrastructure.Configuration
{
    /// <summary>Import/export for the whole config, a single folder, or a single rule (§3.15).</summary>
    public class ImportExportService
    {
        public void ExportConfig(AppConfig config, string filePath) => WriteJson(config, filePath);
        public AppConfig ImportConfig(string filePath) => ReadJson<AppConfig>(filePath);

        public void ExportFolder(WatchedFolder folder, string filePath) => WriteJson(folder, filePath);
        public WatchedFolder ImportFolder(string filePath) => ReadJson<WatchedFolder>(filePath);

        public void ExportRule(Rule rule, string filePath) => WriteJson(rule, filePath);
        public Rule ImportRule(string filePath) => ReadJson<Rule>(filePath);

        /// <summary>Deep-clones a rule (conditions/actions included) via a JSON round-trip, with a
        /// fresh Id so the copy is independent of the original.</summary>
        public Rule CloneRule(Rule rule)
        {
            Rule clone = CloneViaJson(rule);
            clone.Id = Guid.NewGuid().ToString("N");
            clone.Name = $"{rule.Name} (kopie)";
            return clone;
        }

        /// <summary>Deep-clones a folder entry including all its rules, with fresh Ids throughout
        /// so the copy is independent of the original (e.g. before pointing it at a different root).</summary>
        public WatchedFolder CloneFolder(WatchedFolder folder)
        {
            WatchedFolder clone = CloneViaJson(folder);
            clone.Id = Guid.NewGuid().ToString("N");
            foreach (Rule rule in clone.Rules)
            {
                rule.Id = Guid.NewGuid().ToString("N");
            }
            return clone;
        }

        private static T CloneViaJson<T>(T value)
        {
            string json = JsonConvert.SerializeObject(value, ConfigService.SerializerSettings);
            return JsonConvert.DeserializeObject<T>(json, ConfigService.SerializerSettings);
        }

        private static void WriteJson<T>(T value, string filePath)
        {
            string json = JsonConvert.SerializeObject(value, ConfigService.SerializerSettings);
            File.WriteAllText(filePath, json);
        }

        private static T ReadJson<T>(string filePath)
        {
            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<T>(json, ConfigService.SerializerSettings);
        }
    }
}

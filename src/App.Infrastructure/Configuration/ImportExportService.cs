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

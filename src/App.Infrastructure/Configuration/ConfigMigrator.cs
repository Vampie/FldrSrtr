using System.Linq;
using Newtonsoft.Json.Linq;

namespace App.Infrastructure.Configuration
{
    /// <summary>
    /// Migrates a raw config.json JObject to the current schema before it's deserialized into
    /// AppConfig. The one real migration so far: Fase 2 replaced each Rule's flat
    /// "Conditions"/"Logic" with a nested "RootCondition" tree. A config.json saved by a Fase 1
    /// build (schemaVersion 1, or the field missing entirely) still has the old flat shape —
    /// this rebuilds it into an equivalent single-level group so those rules keep matching
    /// exactly the same files after upgrading. Property names match Newtonsoft's default
    /// (unmodified) PascalCase output — ConfigService sets no naming-strategy contract resolver.
    /// </summary>
    public static class ConfigMigrator
    {
        public static JObject MigrateToCurrent(JObject root)
        {
            int version = (int?)root["SchemaVersion"] ?? 1;

            if (version < 2)
            {
                MigrateV1ToV2(root);
                version = 2;
            }

            root["SchemaVersion"] = version;
            return root;
        }

        private static void MigrateV1ToV2(JObject root)
        {
            JArray folders = root["Folders"] as JArray;
            if (folders == null)
            {
                return;
            }

            foreach (JObject folder in folders.OfType<JObject>())
            {
                JArray rules = folder["Rules"] as JArray;
                if (rules == null)
                {
                    continue;
                }

                foreach (JObject rule in rules.OfType<JObject>())
                {
                    if (rule["RootCondition"] != null)
                    {
                        continue; // already in the new shape
                    }

                    JArray oldConditions = rule["Conditions"] as JArray;
                    string oldLogic = (string)rule["Logic"] ?? "All";

                    var children = new JArray();
                    if (oldConditions != null)
                    {
                        foreach (JObject oldCondition in oldConditions.OfType<JObject>())
                        {
                            children.Add(new JObject
                            {
                                ["NodeType"] = "Leaf",
                                ["Field"] = oldCondition["Field"],
                                ["Operator"] = oldCondition["Operator"],
                                ["Value"] = oldCondition["Value"],
                                ["CaseSensitive"] = oldCondition["CaseSensitive"] ?? false
                            });
                        }
                    }

                    rule["RootCondition"] = new JObject
                    {
                        ["NodeType"] = "Group",
                        ["GroupLogic"] = oldLogic,
                        ["Children"] = children
                    };

                    rule.Remove("Conditions");
                    rule.Remove("Logic");
                }
            }
        }
    }
}

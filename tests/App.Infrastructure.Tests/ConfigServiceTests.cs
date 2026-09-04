using System;
using System.IO;
using System.Linq;
using App.Core.Configuration;
using App.Core.Evaluation;
using App.Core.Model;
using App.Infrastructure.Configuration;
using FluentAssertions;
using Xunit;

namespace App.Infrastructure.Tests
{
    public class ConfigServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _configPath;

        public ConfigServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "FldrSrtrTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _configPath = Path.Combine(_tempDir, "config.json");
        }

        [Fact]
        public void LoadOrCreateDefault_WhenFileMissing_CreatesFileWithCurrentSchemaVersion()
        {
            var sut = new ConfigService(_configPath);

            var config = sut.LoadOrCreateDefault();

            config.SchemaVersion.Should().Be(SchemaVersions.Current);
            File.Exists(_configPath).Should().BeTrue();
        }

        [Fact]
        public void LoadOrCreateDefault_WhenFileExists_ReadsBackSavedValues()
        {
            var sut = new ConfigService(_configPath);
            sut.Save(new AppConfig { SchemaVersion = SchemaVersions.Current });

            var reloaded = sut.LoadOrCreateDefault();

            reloaded.SchemaVersion.Should().Be(SchemaVersions.Current);
        }

        [Fact]
        public void Save_CreatesATimestampedBackupOfThePreviousFile()
        {
            var sut = new ConfigService(_configPath);
            sut.Save(new AppConfig());
            sut.Save(new AppConfig()); // second save should back up the first file

            Directory.GetFiles(_tempDir, "config.backup.*.json").Should().HaveCount(1);
        }

        [Fact]
        public void Save_RetainsOnlyTheTenMostRecentBackups()
        {
            var sut = new ConfigService(_configPath);
            for (int i = 0; i < 15; i++)
            {
                sut.Save(new AppConfig());
                System.Threading.Thread.Sleep(2); // backups are timestamp-named to the millisecond
            }

            Directory.GetFiles(_tempDir, "config.backup.*.json").Should().HaveCount(10);
        }

        [Fact]
        public void LoadOrCreateDefault_MigratesFase1FlatConditionsIntoRootCondition()
        {
            // Shape a genuine Fase 1 config.json would have had: Rule.Conditions (flat list) +
            // Rule.Logic, no RootCondition, no explicit SchemaVersion (defaults to 1).
            string oldFormatJson = @"{
                ""Folders"": [{
                    ""Id"": ""f1"",
                    ""Path"": ""C:\\Downloads"",
                    ""Enabled"": true,
                    ""Rules"": [{
                        ""Id"": ""r1"",
                        ""Name"": ""Old PDFs"",
                        ""Enabled"": true,
                        ""Logic"": ""All"",
                        ""Conditions"": [
                            { ""Field"": ""Extension"", ""Operator"": ""Equals"", ""Value"": ""pdf"", ""CaseSensitive"": false }
                        ],
                        ""Actions"": []
                    }]
                }]
            }";
            File.WriteAllText(_configPath, oldFormatJson);

            var sut = new ConfigService(_configPath);
            AppConfig config = sut.LoadOrCreateDefault();

            config.SchemaVersion.Should().Be(SchemaVersions.Current);
            Rule rule = config.Folders.Single().Rules.Single();
            rule.RootCondition.Should().NotBeNull();
            rule.RootCondition.NodeType.Should().Be(ConditionNodeType.Group);
            rule.RootCondition.GroupLogic.Should().Be(GroupLogic.All);
            rule.RootCondition.Children.Should().ContainSingle(c => c.Field == ConditionField.Extension && c.Value == "pdf");

            // The migrated rule must still evaluate exactly as the old flat shape would have.
            var pdfFile = new FileEntry { Name = "invoice.pdf", Extension = "pdf" };
            var txtFile = new FileEntry { Name = "notes.txt", Extension = "txt" };
            ConditionEvaluator.Matches(rule, pdfFile).Should().BeTrue();
            ConditionEvaluator.Matches(rule, txtFile).Should().BeFalse();

            // Migration must have persisted the upgraded shape so this only happens once.
            string savedJson = File.ReadAllText(_configPath);
            savedJson.Should().Contain("RootCondition");
            savedJson.Should().NotContain("\"Conditions\"");
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
    }
}

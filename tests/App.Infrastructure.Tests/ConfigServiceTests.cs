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
        private readonly string _generalPath;
        private readonly string _foldersPath;
        private readonly string _legacyPath;

        public ConfigServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "FldrSrtrTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _generalPath = Path.Combine(_tempDir, "general.config.json");
            _foldersPath = Path.Combine(_tempDir, "folders.config.json");
            _legacyPath = Path.Combine(_tempDir, "config.json");
        }

        [Fact]
        public void LoadOrCreateDefault_WhenFilesMissing_CreatesBothFilesWithCurrentSchemaVersion()
        {
            var sut = new ConfigService(_tempDir);

            var config = sut.LoadOrCreateDefault();

            config.SchemaVersion.Should().Be(SchemaVersions.Current);
            File.Exists(_generalPath).Should().BeTrue();
            File.Exists(_foldersPath).Should().BeTrue();
        }

        [Fact]
        public void LoadOrCreateDefault_WhenFilesExist_ReadsBackSavedValues()
        {
            var sut = new ConfigService(_tempDir);
            var saved = new AppConfig();
            saved.Folders.Add(new WatchedFolder { Path = @"C:\Downloads" });
            saved.Settings.MaxFilesPerRun = 42;
            sut.Save(saved);

            var reloaded = sut.LoadOrCreateDefault();

            reloaded.SchemaVersion.Should().Be(SchemaVersions.Current);
            reloaded.Folders.Should().ContainSingle(f => f.Path == @"C:\Downloads");
            reloaded.Settings.MaxFilesPerRun.Should().Be(42);
        }

        [Fact]
        public void LoadOrCreateDefault_RoundTripsIconOverrides()
        {
            var sut = new ConfigService(_tempDir);
            var saved = new AppConfig();
            saved.Settings.IconOverrides["icon-add.png"] = @"C:\MyIcons\add.png";
            sut.Save(saved);

            var reloaded = sut.LoadOrCreateDefault();

            reloaded.Settings.IconOverrides.Should().ContainKey("icon-add.png")
                .WhoseValue.Should().Be(@"C:\MyIcons\add.png");
        }

        [Fact]
        public void SaveSettings_OnlyTouchesGeneralConfigFile()
        {
            var sut = new ConfigService(_tempDir);
            sut.Save(new AppConfig());
            DateTime foldersWriteTimeBefore = File.GetLastWriteTimeUtc(_foldersPath);
            System.Threading.Thread.Sleep(10);

            sut.SaveSettings(new AppConfig());

            File.GetLastWriteTimeUtc(_foldersPath).Should().Be(foldersWriteTimeBefore);
        }

        [Fact]
        public void SaveFolders_OnlyTouchesFoldersConfigFile()
        {
            var sut = new ConfigService(_tempDir);
            sut.Save(new AppConfig());
            DateTime generalWriteTimeBefore = File.GetLastWriteTimeUtc(_generalPath);
            System.Threading.Thread.Sleep(10);

            sut.SaveFolders(new AppConfig());

            File.GetLastWriteTimeUtc(_generalPath).Should().Be(generalWriteTimeBefore);
        }

        [Fact]
        public void SaveFolders_AlwaysCreatesABackup_RegardlessOfBackupOnSettingsChange()
        {
            var sut = new ConfigService(_tempDir);
            var config = new AppConfig { Settings = { BackupOnSettingsChange = false } };
            sut.SaveFolders(config);
            sut.SaveFolders(config); // second save should back up the first file

            Directory.GetFiles(_tempDir, "folders.config.backup.*.json").Should().HaveCount(1);
        }

        [Fact]
        public void SaveSettings_WhenBackupOnSettingsChangeIsTrue_CreatesABackup()
        {
            var sut = new ConfigService(_tempDir);
            var config = new AppConfig { Settings = { BackupOnSettingsChange = true } };
            sut.SaveSettings(config);
            sut.SaveSettings(config);

            Directory.GetFiles(_tempDir, "general.config.backup.*.json").Should().HaveCount(1);
        }

        [Fact]
        public void SaveSettings_WhenBackupOnSettingsChangeIsFalse_CreatesNoBackup()
        {
            var sut = new ConfigService(_tempDir);
            var config = new AppConfig { Settings = { BackupOnSettingsChange = false } };
            sut.SaveSettings(config);
            sut.SaveSettings(config);

            Directory.GetFiles(_tempDir, "general.config.backup.*.json").Should().BeEmpty();
        }

        [Fact]
        public void Save_RetainsOnlyTheTenMostRecentBackupsPerFile()
        {
            var sut = new ConfigService(_tempDir);
            for (int i = 0; i < 15; i++)
            {
                sut.Save(new AppConfig());
                System.Threading.Thread.Sleep(2); // backups are timestamp-named to the millisecond
            }

            Directory.GetFiles(_tempDir, "general.config.backup.*.json").Should().HaveCount(10);
            Directory.GetFiles(_tempDir, "folders.config.backup.*.json").Should().HaveCount(10);
        }

        [Fact]
        public void LoadOrCreateDefault_MigratesFase1FlatConditionsIntoRootCondition()
        {
            // Shape a genuine Fase 1 folders.config.json would have had: Rule.Conditions (flat
            // list) + Rule.Logic, no RootCondition, no explicit SchemaVersion (defaults to 1).
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
            File.WriteAllText(_foldersPath, oldFormatJson);

            var sut = new ConfigService(_tempDir);
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
        }

        [Fact]
        public void LoadOrCreateDefault_MigratesAPreSplitLegacyConfigJsonIntoTheTwoNewFiles()
        {
            string legacyJson = @"{
                ""SchemaVersion"": 2,
                ""Folders"": [{ ""Id"": ""f1"", ""Path"": ""C:\\Downloads"", ""Enabled"": true, ""Rules"": [] }],
                ""Settings"": { ""MaxFilesPerRun"": 77 }
            }";
            File.WriteAllText(_legacyPath, legacyJson);

            var sut = new ConfigService(_tempDir);
            AppConfig config = sut.LoadOrCreateDefault();

            config.Folders.Should().ContainSingle(f => f.Path == @"C:\Downloads");
            config.Settings.MaxFilesPerRun.Should().Be(77);
            File.Exists(_generalPath).Should().BeTrue();
            File.Exists(_foldersPath).Should().BeTrue();
            File.Exists(_legacyPath).Should().BeFalse("the legacy file is removed once split");
            File.Exists(_legacyPath + ".pre-split.bak").Should().BeTrue("a safety copy of the legacy file is kept");
        }

        [Fact]
        public void LoadOrCreateDefault_WhenGeneralConfigIsCorruptJson_ThrowsAClearException()
        {
            File.WriteAllText(_generalPath, "{ not valid json");
            File.WriteAllText(_foldersPath, "{}");
            var sut = new ConfigService(_tempDir);

            Action act = () => sut.LoadOrCreateDefault();

            act.Should().Throw<InvalidOperationException>().WithMessage($"*{_generalPath}*");
        }

        [Fact]
        public void LoadOrCreateDefault_WhenLegacyConfigIsCorruptJson_ThrowsAClearException()
        {
            File.WriteAllText(_legacyPath, "{ not valid json");
            var sut = new ConfigService(_tempDir);

            Action act = () => sut.LoadOrCreateDefault();

            act.Should().Throw<InvalidOperationException>().WithMessage($"*{_legacyPath}*");
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

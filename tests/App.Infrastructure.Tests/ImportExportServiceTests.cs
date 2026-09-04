using System;
using System.IO;
using App.Core.Configuration;
using App.Core.Model;
using App.Infrastructure.Configuration;
using FluentAssertions;
using Xunit;

namespace App.Infrastructure.Tests
{
    public class ImportExportServiceTests : IDisposable
    {
        private readonly string _tempDir;

        public ImportExportServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "FldrSrtrTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [Fact]
        public void ExportThenImportConfig_RoundTrips()
        {
            var sut = new ImportExportService();
            var config = new AppConfig();
            config.Folders.Add(new WatchedFolder { Path = @"C:\Downloads" });
            string path = Path.Combine(_tempDir, "config-export.json");

            sut.ExportConfig(config, path);
            AppConfig imported = sut.ImportConfig(path);

            imported.Folders.Should().ContainSingle(f => f.Path == @"C:\Downloads");
        }

        [Fact]
        public void ExportThenImportFolder_RoundTrips()
        {
            var sut = new ImportExportService();
            var folder = new WatchedFolder { Path = @"C:\Downloads", Recursive = true };
            folder.Rules.Add(new Rule { Name = "Move old PDFs" });
            string path = Path.Combine(_tempDir, "folder-export.json");

            sut.ExportFolder(folder, path);
            WatchedFolder imported = sut.ImportFolder(path);

            imported.Path.Should().Be(@"C:\Downloads");
            imported.Recursive.Should().BeTrue();
            imported.Rules.Should().ContainSingle(r => r.Name == "Move old PDFs");
        }

        [Fact]
        public void ExportThenImportRule_RoundTrips()
        {
            var sut = new ImportExportService();
            var rule = new Rule { Name = "Archive invoices" };
            rule.RootCondition.Children.Add(ConditionNode.NewLeaf(ConditionField.Extension, ConditionOperator.Equals, "pdf"));
            rule.Actions.Add(new RuleAction { Type = ActionType.Move, Destination = @"D:\Archive" });
            string path = Path.Combine(_tempDir, "rule-export.json");

            sut.ExportRule(rule, path);
            Rule imported = sut.ImportRule(path);

            imported.Name.Should().Be("Archive invoices");
            imported.RootCondition.Children.Should().ContainSingle(c => c.Value == "pdf");
            imported.Actions.Should().ContainSingle(a => a.Destination == @"D:\Archive");
        }

        [Fact]
        public void CloneRule_CopiesContentButAssignsFreshIdAndName()
        {
            var sut = new ImportExportService();
            var rule = new Rule { Name = "Archive invoices" };
            rule.RootCondition.Children.Add(ConditionNode.NewLeaf(ConditionField.Extension, ConditionOperator.Equals, "pdf"));
            rule.Actions.Add(new RuleAction { Type = ActionType.Move, Destination = @"D:\Archive" });

            Rule clone = sut.CloneRule(rule);

            clone.Id.Should().NotBe(rule.Id);
            clone.Name.Should().Be("Archive invoices (kopie)");
            clone.RootCondition.Children.Should().ContainSingle(c => c.Value == "pdf");
            clone.Actions.Should().ContainSingle(a => a.Destination == @"D:\Archive");
        }

        [Fact]
        public void CloneFolder_CopiesRulesWithFreshIdsThroughout()
        {
            var sut = new ImportExportService();
            var folder = new WatchedFolder { Path = @"C:\Downloads", Recursive = true };
            var rule = new Rule { Name = "Move old PDFs" };
            folder.Rules.Add(rule);

            WatchedFolder clone = sut.CloneFolder(folder);

            clone.Id.Should().NotBe(folder.Id);
            clone.Path.Should().Be(@"C:\Downloads");
            clone.Rules.Should().ContainSingle(r => r.Name == "Move old PDFs" && r.Id != rule.Id);
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

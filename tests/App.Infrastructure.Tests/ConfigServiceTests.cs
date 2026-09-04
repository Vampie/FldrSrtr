using System;
using System.IO;
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
            _tempDir = Path.Combine(Path.GetTempPath(), "FoldrSortrTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _configPath = Path.Combine(_tempDir, "config.json");
        }

        [Fact]
        public void LoadOrCreateDefault_WhenFileMissing_CreatesFileWithSchemaVersion1()
        {
            var sut = new ConfigService(_configPath);

            var config = sut.LoadOrCreateDefault();

            config.SchemaVersion.Should().Be(1);
            File.Exists(_configPath).Should().BeTrue();
        }

        [Fact]
        public void LoadOrCreateDefault_WhenFileExists_ReadsBackSavedValues()
        {
            var sut = new ConfigService(_configPath);
            sut.Save(new App.Core.Configuration.AppConfig { SchemaVersion = 1 });

            var reloaded = sut.LoadOrCreateDefault();

            reloaded.SchemaVersion.Should().Be(1);
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

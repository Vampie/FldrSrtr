using System;
using System.IO;
using App.Infrastructure.Activity;
using FluentAssertions;
using Xunit;

namespace App.Infrastructure.Tests
{
    public class ActivityLoggerTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _logPath;

        public ActivityLoggerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "FldrSrtrTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _logPath = Path.Combine(_tempDir, "activity.jsonl");
        }

        [Fact]
        public void Append_ThenReadAll_RoundTripsEntries()
        {
            var logger = new ActivityLogger(_logPath);
            logger.Append(new ActivityLogEntry { RuleName = "Move old PDFs", Status = "SUCCESS", FileName = "invoice.pdf" });
            logger.Append(new ActivityLogEntry { RuleName = "Move old PDFs", Status = "ERROR", FileName = "locked.pdf" });

            var entries = logger.ReadAll();

            entries.Should().HaveCount(2);
            entries[0].Status.Should().Be("SUCCESS");
            entries[1].Status.Should().Be("ERROR");
        }

        [Fact]
        public void ReadAll_WhenFileMissing_ReturnsEmpty()
        {
            new ActivityLogger(_logPath).ReadAll().Should().BeEmpty();
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

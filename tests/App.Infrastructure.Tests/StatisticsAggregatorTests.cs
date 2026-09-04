using System;
using System.Collections.Generic;
using App.Infrastructure.Activity;
using FluentAssertions;
using Xunit;

namespace App.Infrastructure.Tests
{
    public class StatisticsAggregatorTests
    {
        private static ActivityLogEntry Entry(string action, string status, int daysAgo, string rule = "R1", string folder = "C:\\F", long size = 100) => new ActivityLogEntry
        {
            TimestampUtc = DateTime.UtcNow.AddDays(-daysAgo),
            Action = action,
            Status = status,
            RuleName = rule,
            FolderPath = folder,
            FileSizeBytes = size
        };

        [Fact]
        public void Aggregate_CountsSuccessfulActionsByType()
        {
            var entries = new List<ActivityLogEntry>
            {
                Entry("Move", "SUCCESS", 0),
                Entry("Move", "SUCCESS", 0),
                Entry("Copy", "SUCCESS", 0),
                Entry("DeleteToRecycleBin", "SUCCESS", 0),
                Entry("Move", "ERROR", 0),
            };

            var stats = StatisticsAggregator.Aggregate(entries, StatisticsPeriod.AllTime);

            stats.Moved.Should().Be(2);
            stats.Copied.Should().Be(1);
            stats.Deleted.Should().Be(1);
            stats.Errors.Should().Be(1);
            stats.FilesProcessed.Should().Be(4);
        }

        [Fact]
        public void Aggregate_Today_ExcludesOlderEntries()
        {
            var entries = new List<ActivityLogEntry>
            {
                Entry("Move", "SUCCESS", 0),
                Entry("Move", "SUCCESS", 10),
            };

            var stats = StatisticsAggregator.Aggregate(entries, StatisticsPeriod.Today);

            stats.Moved.Should().Be(1);
        }

        [Fact]
        public void Aggregate_ExcludesUndoEntries_FromProcessingCounts()
        {
            var undoEntry = Entry("Move", "SUCCESS", 0);
            undoEntry.UndoOfId = "some-other-id";

            var stats = StatisticsAggregator.Aggregate(new[] { undoEntry }, StatisticsPeriod.AllTime);

            stats.FilesProcessed.Should().Be(0);
        }

        [Fact]
        public void Aggregate_SumsDataMovedAndGroupsByRuleAndFolder()
        {
            var entries = new List<ActivityLogEntry>
            {
                Entry("Move", "SUCCESS", 0, rule: "Sort PDFs", folder: "C:\\Downloads", size: 1000),
                Entry("Move", "SUCCESS", 0, rule: "Sort PDFs", folder: "C:\\Downloads", size: 2000),
                Entry("Copy", "SUCCESS", 0, rule: "Backup", folder: "C:\\Photos", size: 500),
            };

            var stats = StatisticsAggregator.Aggregate(entries, StatisticsPeriod.AllTime);

            stats.DataMovedBytes.Should().Be(3500);
            stats.ActionsPerRule["Sort PDFs"].Should().Be(2);
            stats.ActionsPerRule["Backup"].Should().Be(1);
            stats.ActionsPerFolder["C:\\Downloads"].Should().Be(2);
            stats.ActionsPerFolder["C:\\Photos"].Should().Be(1);
        }
    }
}

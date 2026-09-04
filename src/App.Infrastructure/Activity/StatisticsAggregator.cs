using System;
using System.Collections.Generic;
using System.Linq;

namespace App.Infrastructure.Activity
{
    public enum StatisticsPeriod
    {
        Today,
        Last7Days,
        Last30Days,
        AllTime
    }

    /// <summary>Aggregates the activity log into dashboard numbers (§3.13) — read-only, no side effects.</summary>
    public static class StatisticsAggregator
    {
        public static ActivityStatistics Aggregate(IEnumerable<ActivityLogEntry> entries, StatisticsPeriod period)
        {
            DateTime? sinceUtc = ResolveSince(period);
            var relevant = entries
                .Where(e => e.UndoOfId == null) // undo entries aren't "processing", don't double count
                .Where(e => sinceUtc == null || e.TimestampUtc >= sinceUtc.Value)
                .ToList();

            var stats = new ActivityStatistics();

            foreach (ActivityLogEntry entry in relevant)
            {
                if (entry.Status == "ERROR")
                {
                    stats.Errors++;
                    continue;
                }

                if (entry.Status != "SUCCESS")
                {
                    continue;
                }

                stats.FilesProcessed++;
                stats.DataMovedBytes += entry.FileSizeBytes;

                switch (entry.Action)
                {
                    case "Move": stats.Moved++; break;
                    case "Copy": stats.Copied++; break;
                    case "Rename": stats.Renamed++; break;
                    case "DeleteToRecycleBin": stats.Deleted++; break;
                }

                Increment(stats.ActionsPerRule, entry.RuleName);
                Increment(stats.ActionsPerFolder, entry.FolderPath);
            }

            return stats;
        }

        private static void Increment(Dictionary<string, int> counts, string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }
            counts[key] = counts.TryGetValue(key, out int current) ? current + 1 : 1;
        }

        private static DateTime? ResolveSince(StatisticsPeriod period)
        {
            switch (period)
            {
                case StatisticsPeriod.Today: return DateTime.UtcNow.Date;
                case StatisticsPeriod.Last7Days: return DateTime.UtcNow.Date.AddDays(-7);
                case StatisticsPeriod.Last30Days: return DateTime.UtcNow.Date.AddDays(-30);
                default: return null;
            }
        }
    }
}

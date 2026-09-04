using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using App.Infrastructure.Configuration;
using Newtonsoft.Json;

namespace App.Infrastructure.Activity
{
    /// <summary>
    /// Append-only JSON Lines activity log next to the exe. Deliberately not SQLite — there's
    /// no 24/7 background volume to justify a native dependency (see CLAUDE.md architecture notes).
    /// </summary>
    public class ActivityLogger
    {
        private readonly string _logFilePath;

        public ActivityLogger(string logFilePath = null)
        {
            _logFilePath = logFilePath ?? Path.Combine(PortablePaths.BaseDirectory, "activity.jsonl");
        }

        public void Append(ActivityLogEntry entry)
        {
            File.AppendAllText(_logFilePath, JsonConvert.SerializeObject(entry) + Environment.NewLine);
        }

        public IReadOnlyList<ActivityLogEntry> ReadAll()
        {
            if (!File.Exists(_logFilePath))
            {
                return Array.Empty<ActivityLogEntry>();
            }

            return File.ReadAllLines(_logFilePath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => JsonConvert.DeserializeObject<ActivityLogEntry>(line))
                .ToList();
        }
    }
}

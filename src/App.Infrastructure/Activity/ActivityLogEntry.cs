using System;

namespace App.Infrastructure.Activity
{
    public class ActivityLogEntry
    {
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string FolderPath { get; set; }
        public string RuleName { get; set; }
        public string FileName { get; set; }
        public string Action { get; set; }

        /// <summary>INFO, SUCCESS, WARNING or ERROR — per §3.12.</summary>
        public string Status { get; set; }

        public string Message { get; set; }
        public string OriginalPath { get; set; }
        public string DestinationPath { get; set; }
    }
}

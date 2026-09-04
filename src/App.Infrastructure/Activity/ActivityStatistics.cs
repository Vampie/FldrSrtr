using System.Collections.Generic;

namespace App.Infrastructure.Activity
{
    public class ActivityStatistics
    {
        public int FilesProcessed { get; set; }
        public int Moved { get; set; }
        public int Copied { get; set; }
        public int Renamed { get; set; }
        public int Deleted { get; set; }
        public int Errors { get; set; }
        public long DataMovedBytes { get; set; }
        public Dictionary<string, int> ActionsPerRule { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ActionsPerFolder { get; set; } = new Dictionary<string, int>();
    }
}

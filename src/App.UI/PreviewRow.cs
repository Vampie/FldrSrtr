namespace FldrSrtr
{
    /// <summary>UI-only row shown in the dry-run / run results grid. Never crosses into Core.</summary>
    public class PreviewRow
    {
        public string FileName { get; set; }
        public string Action { get; set; }
        public string FromPath { get; set; }
        public string ToPath { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
    }
}

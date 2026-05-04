using System;

namespace FileSync.Models
{
    public enum LogAction
    {
        Copy,
        Delete,
        Skip,
        Conflict,
        Error
    }

    public class SyncLogEntry
    {
        public int Id { get; set; }
        public Guid TaskId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public LogAction Action { get; set; }
        public string Direction { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}

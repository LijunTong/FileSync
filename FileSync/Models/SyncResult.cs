using System;

namespace FileSync.Models
{
    public class SyncResult
    {
        public int Id { get; set; }
        public Guid TaskId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public SyncStatus Status { get; set; }
        public int FilesCopied { get; set; }
        public int FilesDeleted { get; set; }
        public int FilesSkipped { get; set; }
        public int Errors { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}

using System;
using System.IO;

namespace FileSync.Utils
{
    public static class FileComparer
    {
        public static bool IsSourceNewer(FileInfo source, FileInfo target)
        {
            if (!target.Exists) return true;
            return source.LastWriteTimeUtc > target.LastWriteTimeUtc;
        }

        public static bool IsDifferent(FileInfo source, FileInfo target)
        {
            if (!target.Exists) return true;
            if (source.LastWriteTimeUtc != target.LastWriteTimeUtc) return true;
            if (source.Length != target.Length) return true;
            return false;
        }

        public static bool IsConflict(FileInfo source, FileInfo target)
        {
            if (!source.Exists || !target.Exists) return false;
            TimeSpan diff = source.LastWriteTimeUtc - target.LastWriteTimeUtc;
            return Math.Abs(diff.TotalSeconds) < 2;
        }
    }
}

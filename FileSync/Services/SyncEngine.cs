using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileSync.Models;
using FileSync.Utils;

namespace FileSync.Services
{
    public class SyncProgress
    {
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public string CurrentFile { get; set; } = string.Empty;
        public string Phase { get; set; } = string.Empty;
    }

    public class SyncEngine
    {
        public event EventHandler<SyncProgress>? ProgressChanged;

        public async Task<SyncResult> ExecuteAsync(SyncTask task, CancellationToken cancellationToken = default)
        {
            var result = new SyncResult
            {
                TaskId = task.Id,
                StartTime = DateTime.Now,
                Status = SyncStatus.Running
            };

            var logs = new List<SyncLogEntry>();

            try
            {
                // 开始运行
                task.IsRunning = true;
                task.ProgressValue = 0;
                task.ProgressMax = 1;

                if (!Directory.Exists(task.SourcePath))
                    throw new DirectoryNotFoundException($"Source directory not found: {task.SourcePath}");

                if (!Directory.Exists(task.TargetPath))
                    Directory.CreateDirectory(task.TargetPath);

                var filters = task.GetFilters();

                // Phase 1: Count files
                ReportProgress("Scanning...", 0, 0);
                task.ProgressValue = 0;
                int totalFiles = await Task.Run(() => CountFiles(task.SourcePath, filters), cancellationToken);
                if (task.IsBidirectional)
                {
                    totalFiles += await Task.Run(() => CountFiles(task.TargetPath, filters), cancellationToken);
                }
                task.ProgressMax = totalFiles > 0 ? totalFiles : 1;

                int processed = 0;

                if (task.IsBidirectional)
                {
                    await Task.Run(() =>
                    {
                        SyncBidirectional(task, filters, totalFiles, ref processed, result, logs, cancellationToken);
                    }, cancellationToken);
                }
                else
                {
                    await Task.Run(() =>
                    {
                        SyncOneWay(task, filters, totalFiles, ref processed, result, logs, cancellationToken);
                    }, cancellationToken);
                }

                result.EndTime = DateTime.Now;
                result.Status = result.Errors > 0 ? SyncStatus.Failed : SyncStatus.Success;
            }
            catch (Exception ex)
            {
                result.EndTime = DateTime.Now;
                result.Status = SyncStatus.Failed;
                result.Errors++;
                result.ErrorMessage = ex.Message;
                logs.Add(new SyncLogEntry
                {
                    TaskId = task.Id,
                    FilePath = string.Empty,
                    Action = LogAction.Error,
                    Timestamp = DateTime.Now,
                    Message = ex.Message
                });
            }

            if (logs.Count > 0)
            {
                DatabaseService.Instance.InsertSyncLogs(logs);
            }
            DatabaseService.Instance.InsertSyncResult(result);

            // 结束运行
            task.IsRunning = false;

            return result;
        }

        private void SyncOneWay(SyncTask task, List<string> filters, int totalFiles, ref int processed,
            SyncResult result, List<SyncLogEntry> logs, CancellationToken cancellationToken)
        {
            string source = task.SourcePath;
            string target = task.TargetPath;

            var sourceFiles = GetAllFiles(source, filters);
            var targetFilesSet = new HashSet<string>(GetAllFiles(target, filters).Select(f => f.Substring(target.Length).TrimStart('\\', '/')));

            foreach (var file in sourceFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string relativePath = file.Substring(source.Length).TrimStart('\\', '/');
                string destPath = Path.Combine(target, relativePath);

                processed++;
                task.ProgressValue = processed;
                ReportProgress(relativePath, processed, totalFiles);

                // 添加延时方便测试进度条
                Thread.Sleep(100);

                var srcInfo = new FileInfo(file);
                var destInfo = new FileInfo(destPath);

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

                    if (FileComparer.IsDifferent(srcInfo, destInfo))
                    {
                        File.Copy(file, destPath, overwrite: true);
                        File.SetLastWriteTimeUtc(destPath, srcInfo.LastWriteTimeUtc);
                        result.FilesCopied++;
                        logs.Add(CreateLog(task.Id, relativePath, LogAction.Copy, "Source -> Target"));
                    }
                    else
                    {
                        result.FilesSkipped++;
                        logs.Add(CreateLog(task.Id, relativePath, LogAction.Skip, ""));
                    }

                    targetFilesSet.Remove(relativePath);
                }
                catch (Exception ex)
                {
                    result.Errors++;
                    logs.Add(CreateLog(task.Id, relativePath, LogAction.Error, ex.Message));
                }
            }

            if (task.MirrorMode)
            {
                foreach (var relativePath in targetFilesSet)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string destPath = Path.Combine(target, relativePath);
                    try
                    {
                        if (File.Exists(destPath))
                        {
                            File.Delete(destPath);
                            result.FilesDeleted++;
                            logs.Add(CreateLog(task.Id, relativePath, LogAction.Delete, "Removed from target"));
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors++;
                        logs.Add(CreateLog(task.Id, relativePath, LogAction.Error, ex.Message));
                    }
                }

                CleanupEmptyDirectories(target);
            }
        }

        private void SyncBidirectional(SyncTask task, List<string> filters, int totalFiles, ref int processed,
            SyncResult result, List<SyncLogEntry> logs, CancellationToken cancellationToken)
        {
            string left = task.SourcePath;
            string right = task.TargetPath;

            var leftFiles = GetAllFiles(left, filters);
            var rightFiles = GetAllFiles(right, filters);

            var rightDict = rightFiles.ToDictionary(
                f => f.Substring(right.Length).TrimStart('\\', '/'),
                StringComparer.OrdinalIgnoreCase);

            foreach (var leftFile in leftFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relativePath = leftFile.Substring(left.Length).TrimStart('\\', '/');
                processed++;
                task.ProgressValue = processed;
                ReportProgress(relativePath, processed, totalFiles);

                // 添加延时方便测试进度条
                Thread.Sleep(100);

                var leftInfo = new FileInfo(leftFile);
                string rightFile = Path.Combine(right, relativePath);
                bool rightExists = rightDict.ContainsKey(relativePath);

                try
                {
                    if (!rightExists)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(rightFile)!);
                        File.Copy(leftFile, rightFile, overwrite: true);
                        File.SetLastWriteTimeUtc(rightFile, leftInfo.LastWriteTimeUtc);
                        result.FilesCopied++;
                        logs.Add(CreateLog(task.Id, relativePath, LogAction.Copy, "Source -> Target"));
                    }
                    else
                    {
                        var rightInfo = new FileInfo(rightFile);
                        if (FileComparer.IsConflict(leftInfo, rightInfo))
                        {
                            // Conflict: backup the older one
                            string backup = rightFile + ".conflict-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                            File.Copy(rightFile, backup);
                            File.Copy(leftFile, rightFile, overwrite: true);
                            File.SetLastWriteTimeUtc(rightFile, leftInfo.LastWriteTimeUtc);
                            result.FilesCopied++;
                            logs.Add(CreateLog(task.Id, relativePath, LogAction.Conflict, $"Source -> Target, backup: {backup}"));
                        }
                        else if (FileComparer.IsSourceNewer(leftInfo, rightInfo))
                        {
                            File.Copy(leftFile, rightFile, overwrite: true);
                            File.SetLastWriteTimeUtc(rightFile, leftInfo.LastWriteTimeUtc);
                            result.FilesCopied++;
                            logs.Add(CreateLog(task.Id, relativePath, LogAction.Copy, "Source -> Target"));
                        }
                        else if (FileComparer.IsSourceNewer(rightInfo, leftInfo))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(leftFile)!);
                            File.Copy(rightFile, leftFile, overwrite: true);
                            File.SetLastWriteTimeUtc(leftFile, rightInfo.LastWriteTimeUtc);
                            result.FilesCopied++;
                            logs.Add(CreateLog(task.Id, relativePath, LogAction.Copy, "Target -> Source"));
                        }
                        else
                        {
                            result.FilesSkipped++;
                            logs.Add(CreateLog(task.Id, relativePath, LogAction.Skip, ""));
                        }
                        rightDict.Remove(relativePath);
                    }
                }
                catch (Exception ex)
                {
                    result.Errors++;
                    logs.Add(CreateLog(task.Id, relativePath, LogAction.Error, ex.Message));
                }
            }

            foreach (var remaining in rightDict)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relativePath = remaining.Key;
                string rightFile = remaining.Value;
                string leftFile = Path.Combine(left, relativePath);
                processed++;
                task.ProgressValue = processed;
                ReportProgress(relativePath, processed, totalFiles);

                // 添加延时方便测试进度条
                Thread.Sleep(100);

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(leftFile)!);
                    File.Copy(rightFile, leftFile, overwrite: true);
                    File.SetLastWriteTimeUtc(leftFile, new FileInfo(rightFile).LastWriteTimeUtc);
                    result.FilesCopied++;
                    logs.Add(CreateLog(task.Id, relativePath, LogAction.Copy, "Target -> Source"));
                }
                catch (Exception ex)
                {
                    result.Errors++;
                    logs.Add(CreateLog(task.Id, relativePath, LogAction.Error, ex.Message));
                }
            }
        }

        private List<string> GetAllFiles(string directory, List<string> filters)
        {
            var files = new List<string>();
            if (!Directory.Exists(directory)) return files;

            try
            {
                var allFiles = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
                
                foreach (var file in allFiles)
                {
                    var fileName = Path.GetFileName(file);
                    foreach (var filter in filters)
                    {
                        if (MatchesFilter(fileName, filter))
                        {
                            files.Add(file);
                            break;
                        }
                    }
                }
            }
            catch { }
            
            return files.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private int CountFiles(string directory, List<string> filters)
        {
            if (!Directory.Exists(directory)) return 0;
            
            try
            {
                var allFiles = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
                int count = 0;
                
                foreach (var file in allFiles)
                {
                    var fileName = Path.GetFileName(file);
                    foreach (var filter in filters)
                    {
                        if (MatchesFilter(fileName, filter))
                        {
                            count++;
                            break;
                        }
                    }
                }
                
                return count;
            }
            catch { }
            
            return 0;
        }

        private bool MatchesFilter(string fileName, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;
                
            // 简单的通配符匹配：* 匹配任意字符，? 匹配单个字符
            var pattern = "^" + System.Text.RegularExpressions.Regex.Escape(filter)
                                 .Replace("\\*", ".*")
                                 .Replace("\\?", ".") + "$";
            
            return System.Text.RegularExpressions.Regex.IsMatch(fileName, pattern, 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private void CleanupEmptyDirectories(string directory)
        {
            try
            {
                foreach (var subDir in Directory.GetDirectories(directory))
                {
                    CleanupEmptyDirectories(subDir);
                }

                if (!Directory.EnumerateFileSystemEntries(directory).Any() && directory != directory.TrimEnd('\\'))
                {
                    Directory.Delete(directory);
                }
            }
            catch { }
        }

        private SyncLogEntry CreateLog(Guid taskId, string filePath, LogAction action, string message)
        {
            return new SyncLogEntry
            {
                TaskId = taskId,
                FilePath = filePath,
                Action = action,
                Timestamp = DateTime.Now,
                Message = message
            };
        }

        private void ReportProgress(string currentFile, int processed, int total)
        {
            ProgressChanged?.Invoke(this, new SyncProgress
            {
                CurrentFile = currentFile,
                ProcessedFiles = processed,
                TotalFiles = total,
                Phase = $"{processed} / {total}"
            });
        }
    }
}

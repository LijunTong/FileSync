using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Dapper;
using FileSync.Models;

namespace FileSync.Services
{
    public class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
    {
        public override void SetValue(IDbDataParameter parameter, Guid value)
        {
            parameter.Value = value.ToString();
        }

        public override Guid Parse(object value)
        {
            if (value is string s) return Guid.Parse(s);
            if (value is byte[] b) return new Guid(b);
            return (Guid)value;
        }
    }

    public class DatabaseService
    {
        private static readonly Lazy<DatabaseService> _instance = new Lazy<DatabaseService>(() => new DatabaseService());
        public static DatabaseService Instance => _instance.Value;

        private readonly string _dbPath;

        static DatabaseService()
        {
            DefaultTypeMap.MatchNamesWithUnderscores = true;
            SqlMapper.AddTypeHandler(new GuidTypeHandler());
        }

        private DatabaseService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string configDir = Path.Combine(appData, "FileSync");
            Directory.CreateDirectory(configDir);
            _dbPath = Path.Combine(configDir, "history.db");
            InitializeDatabase();
        }

        private IDbConnection CreateConnection()
        {
            var connection = new System.Data.SQLite.SQLiteConnection($"Data Source={_dbPath};Version=3;");
            connection.Open();
            return connection;
        }

        private void InitializeDatabase()
        {
            using (var conn = CreateConnection())
            {
                conn.Execute(@"
                    CREATE TABLE IF NOT EXISTS sync_results (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        task_id TEXT NOT NULL,
                        start_time TEXT NOT NULL,
                        end_time TEXT NOT NULL,
                        status INTEGER NOT NULL,
                        files_copied INTEGER DEFAULT 0,
                        files_deleted INTEGER DEFAULT 0,
                        files_skipped INTEGER DEFAULT 0,
                        errors INTEGER DEFAULT 0,
                        error_message TEXT DEFAULT ''
                    )
                ");

                conn.Execute(@"
                    CREATE TABLE IF NOT EXISTS sync_logs (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        task_id TEXT NOT NULL,
                        file_path TEXT NOT NULL,
                        action INTEGER NOT NULL,
                        direction TEXT DEFAULT '',
                        timestamp TEXT NOT NULL,
                        message TEXT DEFAULT ''
                    )
                ");

                conn.Execute("CREATE INDEX IF NOT EXISTS idx_sync_logs_task_id ON sync_logs(task_id)");
                conn.Execute("CREATE INDEX IF NOT EXISTS idx_sync_results_task_id ON sync_results(task_id)");
            }
        }

        public int InsertSyncResult(SyncResult result)
        {
            using (var conn = CreateConnection())
            {
                var sql = @"
                    INSERT INTO sync_results (task_id, start_time, end_time, status, files_copied, files_deleted, files_skipped, errors, error_message)
                    VALUES (@TaskId, @StartTime, @EndTime, @Status, @FilesCopied, @FilesDeleted, @FilesSkipped, @Errors, @ErrorMessage);
                    SELECT last_insert_rowid();
                ";
                return conn.ExecuteScalar<int>(sql, new
                {
                    TaskId = result.TaskId.ToString(),
                    result.StartTime,
                    result.EndTime,
                    Status = (int)result.Status,
                    result.FilesCopied,
                    result.FilesDeleted,
                    result.FilesSkipped,
                    result.Errors,
                    result.ErrorMessage
                });
            }
        }

        public void InsertSyncLog(SyncLogEntry entry)
        {
            using (var conn = CreateConnection())
            {
                var sql = @"
                    INSERT INTO sync_logs (task_id, file_path, action, direction, timestamp, message)
                    VALUES (@TaskId, @FilePath, @Action, @Direction, @Timestamp, @Message)
                ";
                conn.Execute(sql, new
                {
                    TaskId = entry.TaskId.ToString(),
                    entry.FilePath,
                    Action = (int)entry.Action,
                    entry.Direction,
                    entry.Timestamp,
                    entry.Message
                });
            }
        }

        public void InsertSyncLogs(IEnumerable<SyncLogEntry> entries)
        {
            using (var conn = CreateConnection())
            {
                var sql = @"
                    INSERT INTO sync_logs (task_id, file_path, action, direction, timestamp, message)
                    VALUES (@TaskId, @FilePath, @Action, @Direction, @Timestamp, @Message)
                ";
                var list = entries.Select(e => new
                {
                    TaskId = e.TaskId.ToString(),
                    e.FilePath,
                    Action = (int)e.Action,
                    e.Direction,
                    e.Timestamp,
                    e.Message
                });
                conn.Execute(sql, list);
            }
        }

        public List<SyncResult> GetSyncResults(Guid taskId, int limit = 100)
        {
            using (var conn = CreateConnection())
            {
                var sql = "SELECT * FROM sync_results WHERE task_id = @TaskId ORDER BY start_time DESC LIMIT @Limit";
                return conn.Query<SyncResult>(sql, new { TaskId = taskId.ToString(), Limit = limit }).ToList();
            }
        }

        public List<SyncLogEntry> GetSyncLogs(Guid taskId, DateTime? from = null, DateTime? to = null)
        {
            using (var conn = CreateConnection())
            {
                var sql = "SELECT * FROM sync_logs WHERE task_id = @TaskId";
                if (from.HasValue)
                    sql += " AND timestamp >= @From";
                if (to.HasValue)
                    sql += " AND timestamp <= @To";
                sql += " ORDER BY timestamp DESC";

                return conn.Query<SyncLogEntry>(sql, new { TaskId = taskId.ToString(), From = from, To = to }).ToList();
            }
        }

        public void CleanupOldLogs(TimeSpan retention)
        {
            DateTime cutoff = DateTime.Now - retention;
            using (var conn = CreateConnection())
            {
                conn.Execute("DELETE FROM sync_logs WHERE timestamp < @Cutoff", new { Cutoff = cutoff });
                conn.Execute("DELETE FROM sync_results WHERE start_time < @Cutoff", new { Cutoff = cutoff });
            }
        }
    }
}

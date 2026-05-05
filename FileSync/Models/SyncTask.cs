using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using FileSync.Utils;

namespace FileSync.Models
{
    public enum ScheduleType
    {
        None,
        Minutely,
        Hourly,
        Daily,
        Weekly,
        Cron
    }

    public enum SyncStatus
    {
        Idle,
        Running,
        Success,
        Failed
    }

    public class SyncTask : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
        public bool IsBidirectional { get; set; } = false;
        public bool MirrorMode { get; set; } = true;
        public string FilterPattern { get; set; } = "*.*";
        public ScheduleType Schedule { get; set; } = ScheduleType.None;
        public TimeSpan? ScheduleTime { get; set; }
        public DayOfWeek? ScheduleDay { get; set; }
        public string CronExpression { get; set; } = string.Empty;
        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (SetField(ref _isEnabled, value))
                {
                    OnPropertyChanged(nameof(NextRunDisplay));
                }
            }
        }

        public DateTime? LastRunTime { get; set; }
        public SyncStatus LastStatus { get; set; } = SyncStatus.Idle;
        public string LastErrorMessage { get; set; } = string.Empty;

        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                if (SetField(ref _isRunning, value))
                {
                    if (!value)
                    {
                        ProgressValue = 0;
                        ProgressMax = 1;
                    }
                }
            }
        }

        private int _progressValue;
        public int ProgressValue
        {
            get => _progressValue;
            set => SetField(ref _progressValue, value);
        }

        private int _progressMax = 1;
        public int ProgressMax
        {
            get => _progressMax;
            set => SetField(ref _progressMax, value);
        }

        public string ScheduleDisplay
        {
            get
            {
                switch (Schedule)
                {
                    case ScheduleType.None: return "手动";
                    case ScheduleType.Minutely:
                        var interval = Math.Max(1, (int)(ScheduleTime?.TotalMinutes ?? 1));
                        return $"每{interval}分钟";
                    case ScheduleType.Hourly:
                        return $"每小时第{ScheduleTime?.Minutes ?? 0}分";
                    case ScheduleType.Daily:
                        return ScheduleTime.HasValue ? $"每天{ScheduleTime.Value:hh\\:mm}" : "每天";
                    case ScheduleType.Weekly:
                        var d = ScheduleDay.HasValue ? GetDayName(ScheduleDay.Value) : "";
                        var t = ScheduleTime.HasValue ? ScheduleTime.Value.ToString("hh\\:mm") : "";
                        return $"每周{d} {t}".Trim();
                    case ScheduleType.Cron:
                        return string.IsNullOrEmpty(CronExpression) ? "Cron" : CronExpression;
                    default: return "";
                }
            }
        }

        private static string GetDayName(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Monday => "一",
                DayOfWeek.Tuesday => "二",
                DayOfWeek.Wednesday => "三",
                DayOfWeek.Thursday => "四",
                DayOfWeek.Friday => "五",
                DayOfWeek.Saturday => "六",
                DayOfWeek.Sunday => "日",
                _ => ""
            };
        }

        public string NextRunDisplay
        {
            get
            {
                if (!IsEnabled || Schedule == ScheduleType.None)
                    return "";

                var now = DateTime.Now;
                DateTime? next = null;

                switch (Schedule)
                {
                    case ScheduleType.Minutely:
                        var interval = Math.Max(1, (int)(ScheduleTime?.TotalMinutes ?? 1));
                        next = now.AddMinutes(interval);
                        break;
                    case ScheduleType.Hourly:
                        var targetMin = ScheduleTime?.Minutes ?? 0;
                        next = new DateTime(now.Year, now.Month, now.Day, now.Hour, targetMin, 0);
                        if (next <= now) next = next.Value.AddHours(1);
                        break;
                    case ScheduleType.Daily:
                        if (ScheduleTime.HasValue)
                        {
                            var st = ScheduleTime.Value;
                            next = new DateTime(now.Year, now.Month, now.Day, st.Hours, st.Minutes, 0);
                            if (next <= now) next = next.Value.AddDays(1);
                        }
                        break;
                    case ScheduleType.Weekly:
                        if (ScheduleTime.HasValue && ScheduleDay.HasValue)
                        {
                            var st = ScheduleTime.Value;
                            next = new DateTime(now.Year, now.Month, now.Day, st.Hours, st.Minutes, 0);
                            int daysUntil = ((int)ScheduleDay.Value - (int)now.DayOfWeek + 7) % 7;
                            if (daysUntil == 0 && next <= now) daysUntil = 7;
                            next = next.Value.AddDays(daysUntil);
                        }
                        break;
                    case ScheduleType.Cron:
                        if (!string.IsNullOrEmpty(CronExpression))
                            next = CronEvaluator.GetNextOccurrence(CronExpression, now);
                        break;
                }

                return next.HasValue ? next.Value.ToString("MM-dd HH:mm") : "";
            }
        }

        public List<string> GetFilters()
        {
            if (string.IsNullOrWhiteSpace(FilterPattern))
                return new List<string> { "*.*" };
            return FilterPattern.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                               .Select(f => f.Trim())
                               .Where(f => !string.IsNullOrWhiteSpace(f))
                               .ToList();
        }
    }
}

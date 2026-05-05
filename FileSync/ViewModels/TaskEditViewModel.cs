using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Win32;
using FileSync.Models;
using FileSync.Utils;

namespace FileSync.ViewModels
{
    public class ComboItem<T>
    {
        public string Display { get; set; } = string.Empty;
        public T Value { get; set; } = default!;
    }

    public class TaskEditViewModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _sourcePath = string.Empty;
        private string _targetPath = string.Empty;
        private bool _isBidirectional;
        private bool _mirrorMode = true;
        private string _filterPattern = "*.*";
        private ScheduleType _schedule = ScheduleType.None;
        private TimeSpan? _scheduleTime;
        private DayOfWeek? _scheduleDay;
        private string _cronExpression = string.Empty;
        private bool _isEnabled = true;
        private string _scheduleError = string.Empty;
        private string _nextRunTime = "";
        private List<string> _nextRunTimes = new List<string>();

        public List<ComboItem<ScheduleType>> ScheduleOptions { get; } = new List<ComboItem<ScheduleType>>
        {
            new ComboItem<ScheduleType> { Display = "手动执行", Value = ScheduleType.None },
            new ComboItem<ScheduleType> { Display = "每隔N分钟", Value = ScheduleType.Minutely },
            new ComboItem<ScheduleType> { Display = "每小时", Value = ScheduleType.Hourly },
            new ComboItem<ScheduleType> { Display = "每天", Value = ScheduleType.Daily },
            new ComboItem<ScheduleType> { Display = "每周", Value = ScheduleType.Weekly },
            new ComboItem<ScheduleType> { Display = "Cron 表达式", Value = ScheduleType.Cron },
        };

        public List<ComboItem<DayOfWeek>> DayOfWeekOptions { get; } = new List<ComboItem<DayOfWeek>>
        {
            new ComboItem<DayOfWeek> { Display = "星期一", Value = DayOfWeek.Monday },
            new ComboItem<DayOfWeek> { Display = "星期二", Value = DayOfWeek.Tuesday },
            new ComboItem<DayOfWeek> { Display = "星期三", Value = DayOfWeek.Wednesday },
            new ComboItem<DayOfWeek> { Display = "星期四", Value = DayOfWeek.Thursday },
            new ComboItem<DayOfWeek> { Display = "星期五", Value = DayOfWeek.Friday },
            new ComboItem<DayOfWeek> { Display = "星期六", Value = DayOfWeek.Saturday },
            new ComboItem<DayOfWeek> { Display = "星期日", Value = DayOfWeek.Sunday },
        };

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string SourcePath
        {
            get => _sourcePath;
            set { _sourcePath = value; OnPropertyChanged(); }
        }

        public string TargetPath
        {
            get => _targetPath;
            set { _targetPath = value; OnPropertyChanged(); }
        }

        public bool IsBidirectional
        {
            get => _isBidirectional;
            set { _isBidirectional = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsMirrorModeVisible)); }
        }

        public bool MirrorMode
        {
            get => _mirrorMode;
            set { _mirrorMode = value; OnPropertyChanged(); }
        }

        public bool IsMirrorModeVisible => !IsBidirectional;

        public string FilterPattern
        {
            get => _filterPattern;
            set { _filterPattern = value; OnPropertyChanged(); }
        }

        public ScheduleType Schedule
        {
            get => _schedule;
            set
            {
                _schedule = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsScheduleTimeVisible));
                OnPropertyChanged(nameof(IsScheduleDayVisible));
                OnPropertyChanged(nameof(IsHourlyMinuteVisible));
                OnPropertyChanged(nameof(IsMinuteIntervalVisible));
                OnPropertyChanged(nameof(IsCronVisible));
                ValidateSchedule();
                CalculateNextRun();
            }
        }

        public TimeSpan? ScheduleTime
        {
            get => _scheduleTime;
            set { _scheduleTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(MinuteInterval)); ValidateSchedule(); CalculateNextRun(); }
        }

        public DayOfWeek? ScheduleDay
        {
            get => _scheduleDay;
            set { _scheduleDay = value; OnPropertyChanged(); ValidateSchedule(); CalculateNextRun(); }
        }

        public string CronExpression
        {
            get => _cronExpression;
            set { _cronExpression = value; OnPropertyChanged(); ValidateSchedule(); CalculateNextRun(); }
        }

        public int MinuteInterval
        {
            get => Math.Max(1, (int)(ScheduleTime?.TotalMinutes ?? 5));
            set { ScheduleTime = TimeSpan.FromMinutes(Math.Max(1, value)); OnPropertyChanged(); ValidateSchedule(); CalculateNextRun(); }
        }

        public int HourlyMinute
        {
            get => ScheduleTime?.Minutes ?? 0;
            set { ScheduleTime = TimeSpan.FromMinutes(Math.Max(0, Math.Min(59, value))); OnPropertyChanged(); ValidateSchedule(); CalculateNextRun(); }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        public bool IsScheduleTimeVisible => Schedule == ScheduleType.Daily || Schedule == ScheduleType.Weekly;
        public bool IsScheduleDayVisible => Schedule == ScheduleType.Weekly;
        public bool IsHourlyMinuteVisible => Schedule == ScheduleType.Hourly;
        public bool IsMinuteIntervalVisible => Schedule == ScheduleType.Minutely;
        public bool IsCronVisible => Schedule == ScheduleType.Cron;
        public bool IsNextRunVisible => Schedule != ScheduleType.None && string.IsNullOrEmpty(ScheduleError);

        public string ScheduleError
        {
            get => _scheduleError;
            set { _scheduleError = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNextRunVisible)); OnPropertyChanged(nameof(HasScheduleError)); }
        }

        public bool HasScheduleError => !string.IsNullOrEmpty(ScheduleError);

        public string NextRunTime
        {
            get => _nextRunTime;
            set { _nextRunTime = value; OnPropertyChanged(); }
        }

        public List<string> NextRunTimes
        {
            get => _nextRunTimes;
            set { _nextRunTimes = value; OnPropertyChanged(); }
        }

        public SyncTask? Task { get; private set; }

        public RelayCommand BrowseSourceCommand { get; }
        public RelayCommand BrowseTargetCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }

        public TaskEditViewModel(SyncTask task)
        {
            if (task.Id != Guid.Empty)
            {
                Task = task;
                Name = task.Name;
                SourcePath = task.SourcePath;
                TargetPath = task.TargetPath;
                IsBidirectional = task.IsBidirectional;
                MirrorMode = task.MirrorMode;
                FilterPattern = task.FilterPattern;
                Schedule = task.Schedule;
                ScheduleTime = task.ScheduleTime;
                ScheduleDay = task.ScheduleDay;
                CronExpression = task.CronExpression;
                IsEnabled = task.IsEnabled;
            }
            else
            {
                Task = task;
                ScheduleTime = TimeSpan.FromMinutes(5);
            }

            BrowseSourceCommand = new RelayCommand(BrowseSource);
            BrowseTargetCommand = new RelayCommand(BrowseTarget);
            SaveCommand = new RelayCommand(Save, CanSave);
            CancelCommand = new RelayCommand(() => { });

            ValidateSchedule();
            CalculateNextRun();
        }

        private void ValidateSchedule()
        {
            switch (Schedule)
            {
                case ScheduleType.None:
                    ScheduleError = "";
                    break;
                case ScheduleType.Minutely:
                    var interval = MinuteInterval;
                    if (interval < 1)
                        ScheduleError = "间隔分钟数必须大于 0";
                    else
                        ScheduleError = "";
                    break;
                case ScheduleType.Hourly:
                    var min = HourlyMinute;
                    if (min < 0 || min > 59)
                        ScheduleError = "分钟数必须在 0-59 之间";
                    else
                        ScheduleError = "";
                    break;
                case ScheduleType.Daily:
                    if (!ScheduleTime.HasValue)
                        ScheduleError = "请设置执行时间";
                    else
                        ScheduleError = "";
                    break;
                case ScheduleType.Weekly:
                    if (!ScheduleTime.HasValue)
                        ScheduleError = "请设置执行时间";
                    else if (!ScheduleDay.HasValue)
                        ScheduleError = "请选择执行日期";
                    else
                        ScheduleError = "";
                    break;
                case ScheduleType.Cron:
                    if (string.IsNullOrWhiteSpace(CronExpression))
                        ScheduleError = "请输入 Cron 表达式";
                    else if (!CronEvaluator.IsValid(CronExpression))
                        ScheduleError = "Cron 表达式格式无效";
                    else
                        ScheduleError = "";
                    break;
            }
        }

        private void CalculateNextRun()
        {
            var times = new List<string>();
            if (Schedule == ScheduleType.None || !string.IsNullOrEmpty(ScheduleError))
            {
                NextRunTime = "";
                NextRunTimes = times;
                return;
            }

            var now = DateTime.Now;
            DateTime? current = now;

            for (int i = 0; i < 5; i++)
            {
                DateTime? next = null;

                switch (Schedule)
                {
                    case ScheduleType.Minutely:
                        next = current.Value.AddMinutes(MinuteInterval);
                        break;
                    case ScheduleType.Hourly:
                        var targetMin = HourlyMinute;
                        next = new DateTime(current.Value.Year, current.Value.Month, current.Value.Day, current.Value.Hour, targetMin, 0);
                        if (next <= current)
                            next = next.Value.AddHours(1);
                        break;
                    case ScheduleType.Daily:
                        if (ScheduleTime.HasValue)
                        {
                            var st = ScheduleTime.Value;
                            next = new DateTime(current.Value.Year, current.Value.Month, current.Value.Day, st.Hours, st.Minutes, 0);
                            if (next <= current)
                                next = next.Value.AddDays(1);
                        }
                        break;
                    case ScheduleType.Weekly:
                        if (ScheduleTime.HasValue && ScheduleDay.HasValue)
                        {
                            var st = ScheduleTime.Value;
                            next = new DateTime(current.Value.Year, current.Value.Month, current.Value.Day, st.Hours, st.Minutes, 0);
                            int daysUntil = ((int)ScheduleDay.Value - (int)current.Value.DayOfWeek + 7) % 7;
                            if (daysUntil == 0 && next <= current)
                                daysUntil = 7;
                            next = next.Value.AddDays(daysUntil);
                        }
                        break;
                    case ScheduleType.Cron:
                        next = CronEvaluator.GetNextOccurrence(CronExpression, current.Value);
                        break;
                }

                if (next.HasValue)
                {
                    times.Add($"{i + 1}. {next.Value:yyyy-MM-dd HH:mm:ss}");
                    current = next.Value;
                }
                else
                {
                    break;
                }
            }

            NextRunTimes = times;
            NextRunTime = times.Count > 0
                ? $"下次执行：{times[0].Substring(3)}"
                : "无法计算下次执行时间";
        }

        private void BrowseSource()
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择源目录",
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "选择此文件夹"
            };
            if (dialog.ShowDialog() == true)
                SourcePath = Path.GetDirectoryName(dialog.FileName);
        }

        private void BrowseTarget()
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择目标目录",
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "选择此文件夹"
            };
            if (dialog.ShowDialog() == true)
                TargetPath = Path.GetDirectoryName(dialog.FileName);
        }

        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(Name)
                && !string.IsNullOrWhiteSpace(SourcePath)
                && !string.IsNullOrWhiteSpace(TargetPath)
                && string.IsNullOrEmpty(ScheduleError);
        }

        private void Save()
        {
            if (Task == null) return;
            Task.Name = Name;
            Task.SourcePath = SourcePath;
            Task.TargetPath = TargetPath;
            Task.IsBidirectional = IsBidirectional;
            Task.MirrorMode = MirrorMode;
            Task.FilterPattern = FilterPattern;
            Task.Schedule = Schedule;
            Task.ScheduleTime = ScheduleTime;
            Task.ScheduleDay = ScheduleDay;
            Task.CronExpression = CronExpression;
            Task.IsEnabled = IsEnabled;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

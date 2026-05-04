using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using FileSync.Models;
using FileSync.Utils;

namespace FileSync.Services
{
    public class SchedulerService
    {
        private readonly Dictionary<Guid, bool> _runningTasks = new Dictionary<Guid, bool>();
        private readonly Timer _timer;
        private readonly Dispatcher _dispatcher;

        public event EventHandler<SyncTask>? TaskDue;

        public SchedulerService()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _timer = new Timer(OnTimerTick, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        private void OnTimerTick(object? state)
        {
            _dispatcher.Invoke(() => CheckSchedules());
        }

        private void CheckSchedules()
        {
            foreach (var task in ConfigService.Instance.Tasks)
            {
                if (!task.IsEnabled || task.Schedule == ScheduleType.None)
                    continue;

                if (_runningTasks.ContainsKey(task.Id) && _runningTasks[task.Id])
                    continue;

                if (ShouldRunNow(task))
                {
                    _runningTasks[task.Id] = true;
                    TaskDue?.Invoke(this, task);
                }
            }
        }

        private bool ShouldRunNow(SyncTask task)
        {
            var now = DateTime.Now;

            // Minutely: at least configured interval since last run
            if (task.Schedule == ScheduleType.Minutely)
            {
                var intervalMinutes = Math.Max(1, (int)(task.ScheduleTime?.TotalMinutes ?? 1));
                if (task.LastRunTime.HasValue)
                    return (now - task.LastRunTime.Value).TotalMinutes >= intervalMinutes;
                return true;
            }

            // Hourly: at least 60 minutes since last run, at specified minute of hour
            if (task.Schedule == ScheduleType.Hourly)
            {
                var targetMinute = task.ScheduleTime?.Minutes ?? 0;
                if (now.Minute != targetMinute)
                    return false;
                if (task.LastRunTime.HasValue && (now - task.LastRunTime.Value).TotalMinutes < 60)
                    return false;
                return true;
            }

            // Cron expression
            if (task.Schedule == ScheduleType.Cron)
            {
                if (string.IsNullOrWhiteSpace(task.CronExpression))
                    return false;
                if (!CronEvaluator.IsMatch(task.CronExpression, now))
                    return false;
                if (task.LastRunTime.HasValue && (now - task.LastRunTime.Value).TotalSeconds < 60)
                    return false;
                return true;
            }

            // Daily / Weekly: time-based
            var st = task.ScheduleTime ?? TimeSpan.Zero;
            var scheduledToday = new DateTime(now.Year, now.Month, now.Day, st.Hours, st.Minutes, 0);

            if (task.LastRunTime.HasValue)
            {
                if (task.LastRunTime.Value.Date == now.Date && task.LastRunTime.Value >= scheduledToday)
                    return false;
            }

            if (now < scheduledToday)
                return false;

            if (task.Schedule == ScheduleType.Weekly && task.ScheduleDay.HasValue)
            {
                if (now.DayOfWeek != task.ScheduleDay.Value)
                    return false;
            }

            return true;
        }

        public void MarkTaskComplete(Guid taskId)
        {
            _runningTasks[taskId] = false;
        }

        public void Stop()
        {
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }
}

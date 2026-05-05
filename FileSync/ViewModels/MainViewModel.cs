using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using FileSync.Models;
using FileSync.Services;
using FileSync.Utils;
using FileSync.Views;

namespace FileSync.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly SyncEngine _syncEngine;
        private readonly SchedulerService _scheduler;

        private SyncTask? _selectedTask;
        private string _statusMessage = "就绪";
        private int _progressValue;
        private int _progressMaximum = 100;
        private bool _isBusy;
        private bool _autoStart;

        public ObservableCollection<SyncTask> Tasks { get; } = new ObservableCollection<SyncTask>();

        public SyncTask? SelectedTask
        {
            get => _selectedTask;
            set { _selectedTask = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelection)); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public int ProgressValue
        {
            get => _progressValue;
            set { _progressValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsProgressVisible)); }
        }

        public int ProgressMaximum
        {
            get => _progressMaximum;
            set { _progressMaximum = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotBusy)); }
        }

        public bool IsNotBusy => !IsBusy;
        public bool HasSelection => SelectedTask != null;
        public bool IsProgressVisible => ProgressValue > 0;
        public bool HasTasks => Tasks.Count > 0;

        public bool AutoStart
        {
            get => _autoStart;
            set
            {
                _autoStart = value;
                OnPropertyChanged();
                ConfigService.Instance.SetAutoStart(value);
            }
        }

        public RelayCommand AddTaskCommand { get; }
        public RelayCommand EditTaskCommand { get; }
        public RelayCommand DeleteTaskCommand { get; }
        public RelayCommand ExecuteTaskCommand { get; }
        public RelayCommand ViewLogsCommand { get; }
        public RelayCommand ExitCommand { get; }

        public MainViewModel()
        {
            _syncEngine = new SyncEngine();
            _syncEngine.ProgressChanged += OnSyncProgress;

            _scheduler = new SchedulerService();
            _scheduler.TaskDue += async (s, task) => await ExecuteTaskAsync(task);

            _autoStart = ConfigService.Instance.Settings.AutoStart;

            AddTaskCommand = new RelayCommand(AddTask, () => !IsBusy);
            EditTaskCommand = new RelayCommand(EditTask, () => SelectedTask != null && !IsBusy);
            DeleteTaskCommand = new RelayCommand(DeleteTask, () => SelectedTask != null && !IsBusy);
            ExecuteTaskCommand = new RelayCommand(ExecuteTask, () => SelectedTask != null && !IsBusy);
            ViewLogsCommand = new RelayCommand(ViewLogs, () => SelectedTask != null);
            ExitCommand = new RelayCommand(() =>
            {
                if (App.Current is App app)
                    app.HideMainWindow();
            });

            LoadTasks();
        }

        private void LoadTasks()
        {
            Tasks.Clear();
            foreach (var task in ConfigService.Instance.Tasks.OrderBy(t => t.Name))
            {
                Tasks.Add(task);
            }
            OnPropertyChanged(nameof(HasTasks));
        }

        private void AddTask()
        {
            var vm = new TaskEditViewModel(new SyncTask());
            var window = new TaskEditWindow
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow
            };
            if (window.ShowDialog() == true && vm.Task != null)
            {
                ConfigService.Instance.AddTask(vm.Task);
                Tasks.Add(vm.Task);
                OnPropertyChanged(nameof(HasTasks));
            }
        }

        public void EditTask()
        {
            if (SelectedTask == null) return;
            var vm = new TaskEditViewModel(SelectedTask);
            var window = new TaskEditWindow
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow
            };
            if (window.ShowDialog() == true && vm.Task != null)
            {
                ConfigService.Instance.UpdateTask(vm.Task);
                RefreshTasks();
            }
        }

        private void DeleteTask()
        {
            if (SelectedTask == null) return;
            var window = new Views.ConfirmWindow(
                "确认删除",
                $"确定要删除任务 \"{SelectedTask.Name}\" 吗？\n\n此操作不可撤销。",
                "删除",
                "取消");
            window.Owner = Application.Current.MainWindow;
            if (window.ShowDialog() == true)
            {
                ConfigService.Instance.DeleteTask(SelectedTask.Id);
                Tasks.Remove(SelectedTask);
                SelectedTask = null;
                OnPropertyChanged(nameof(HasTasks));
            }
        }

        private void ExecuteTask()
        {
            if (SelectedTask == null) return;
            var window = new Views.ConfirmWindow(
                "确认执行",
                $"确定要立即执行任务 \"{SelectedTask.Name}\" 吗？",
                "执行",
                "取消");
            window.Owner = Application.Current.MainWindow;
            if (window.ShowDialog() == true)
            {
                _ = ExecuteTaskAsync(SelectedTask);
            }
        }

        private async Task ExecuteTaskAsync(SyncTask? task)
        {
            if (task == null) return;
            IsBusy = true;
            ProgressValue = 0;
            StatusMessage = $"⏳ 正在同步: {task.Name}...";

            try
            {
                using var cts = new CancellationTokenSource();
                var result = await _syncEngine.ExecuteAsync(task, cts.Token);

                task.LastRunTime = DateTime.Now;
                task.LastStatus = result.Status;
                task.LastErrorMessage = result.ErrorMessage;
                ConfigService.Instance.UpdateTask(task);

                if (result.Status == SyncStatus.Success)
                {
                    StatusMessage = $"✅ 同步完成: {task.Name} — 复制 {result.FilesCopied} | 删除 {result.FilesDeleted} | 跳过 {result.FilesSkipped}";
                }
                else
                {
                    StatusMessage = $"❌ 同步失败: {task.Name} — {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ 同步异常: {task.Name} — {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                ProgressValue = 0;
                _scheduler.MarkTaskComplete(task.Id);
                RefreshTasks();
            }
        }

        private void ViewLogs()
        {
            if (SelectedTask == null) return;
            var vm = new LogViewModel(SelectedTask);
            var window = new LogWindow
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow
            };
            window.ShowDialog();
        }

        public void RefreshTasks()
        {
            var selectedId = SelectedTask?.Id;
            Tasks.Clear();
            foreach (var task in ConfigService.Instance.Tasks.OrderBy(t => t.Name))
            {
                Tasks.Add(task);
            }
            OnPropertyChanged(nameof(HasTasks));
            if (selectedId.HasValue)
                SelectedTask = Tasks.FirstOrDefault(t => t.Id == selectedId.Value);
        }

        public static string GetStatusIcon(SyncStatus status)
        {
            return status switch
            {
                SyncStatus.Success => "✅",
                SyncStatus.Failed  => "❌",
                SyncStatus.Running => "⏳",
                _                  => "—"
            };
        }

        private void OnSyncProgress(object? sender, SyncProgress e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ProgressMaximum = Math.Max(e.TotalFiles, 1);
                ProgressValue = e.ProcessedFiles;
                StatusMessage = $"⏳ {e.Phase} - {e.CurrentFile}";
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

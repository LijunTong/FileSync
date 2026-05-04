using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FileSync.Models;
using FileSync.Services;

namespace FileSync.ViewModels
{
    public class LogViewModel : INotifyPropertyChanged
    {
        private SyncResult? _selectedResult;

        public SyncTask Task { get; }
        public ObservableCollection<SyncResult> Results { get; } = new ObservableCollection<SyncResult>();
        public ObservableCollection<SyncLogEntry> DetailLogs { get; } = new ObservableCollection<SyncLogEntry>();

        public SyncResult? SelectedResult
        {
            get => _selectedResult;
            set
            {
                _selectedResult = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelection));
                LoadDetailLogs();
            }
        }

        public bool HasSelection => SelectedResult != null;

        public LogViewModel(SyncTask task)
        {
            Task = task;
            LoadResults();
        }

        public void LoadResults()
        {
            Results.Clear();
            var results = DatabaseService.Instance.GetSyncResults(Task.Id, 100);
            foreach (var r in results)
                Results.Add(r);
        }

        private void LoadDetailLogs()
        {
            DetailLogs.Clear();
            if (SelectedResult == null) return;

            var logs = DatabaseService.Instance.GetSyncLogs(
                Task.Id,
                SelectedResult.StartTime,
                SelectedResult.EndTime);
            foreach (var log in logs)
                DetailLogs.Add(log);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

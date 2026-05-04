using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FileSync.Models;
using FileSync.Services;
using FileSync.ViewModels;

namespace FileSync
{
    public partial class MainWindow : Window
    {
        private MainViewModel? ViewModel => DataContext as MainViewModel;
        private bool _forceClose;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!_forceClose && App.Current is App app && !app.IsExiting)
            {
                e.Cancel = true;
                app.HideMainWindow();
            }
        }

        public void ForceClose()
        {
            _forceClose = true;
            Close();
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            var row = FindParent<DataGridRow>(e.OriginalSource as DependencyObject);
            if (row?.DataContext is SyncTask task)
            {
                ViewModel!.SelectedTask = task;
                ViewModel.EditTask();
            }
        }

        private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                    return parent;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        private void EnableCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is SyncTask task)
            {
                task.IsEnabled = cb.IsChecked == true;
                ConfigService.Instance.UpdateTask(task);
            }
        }

        private void RowEdit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is SyncTask task)
            {
                ViewModel!.SelectedTask = task;
                ViewModel.EditTask();
            }
        }

        private void RowDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is SyncTask task)
            {
                ViewModel!.SelectedTask = task;
                ViewModel.DeleteTaskCommand.Execute(null);
            }
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.EditTask();
        }

        private void Execute_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.ExecuteTaskCommand.Execute(null);
        }

        private void Logs_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.ViewLogsCommand.Execute(null);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.DeleteTaskCommand.Execute(null);
        }
    }
}

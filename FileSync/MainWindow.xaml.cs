using System;
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
        private bool _isDarkTheme;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            LoadTheme();
            StateChanged += MainWindow_StateChanged;
            MouseLeftButtonDown += MainWindow_MouseLeftButtonDown;
            Deactivated += MainWindow_Deactivated;
        }

        private void MainWindow_Deactivated(object sender, EventArgs e)
        {
            TaskContextMenu.Hide();
        }

        private void MainWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            TaskContextMenu.Hide();
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            UpdateWindowState();
        }

        private void UpdateWindowState()
        {
            if (WindowState == WindowState.Maximized)
            {
                MainBorder.CornerRadius = new CornerRadius(0);
                MainBorder.BorderThickness = new Thickness(0);
            }
            else
            {
                MainBorder.CornerRadius = new CornerRadius(8);
                MainBorder.BorderThickness = new Thickness(1);
            }
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

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
            }
            else
            {
                DragMove();
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            ToggleTheme();
        }

        private void LoadTheme()
        {
            _isDarkTheme = ConfigService.Instance.Settings.DarkTheme;
            ApplyTheme();
        }

        private void ToggleTheme()
        {
            _isDarkTheme = !_isDarkTheme;
            ConfigService.Instance.Settings.DarkTheme = _isDarkTheme;
            ConfigService.Instance.SaveSettings();
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            var mergedDictionaries = Application.Current.Resources.MergedDictionaries;

            for (int i = mergedDictionaries.Count - 1; i >= 0; i--)
            {
                var dict = mergedDictionaries[i];
                if (dict.Source?.OriginalString?.Contains("LightTheme") == true ||
                    dict.Source?.OriginalString?.Contains("DarkTheme") == true)
                {
                    mergedDictionaries.RemoveAt(i);
                }
            }

            var themeUri = _isDarkTheme
                ? new Uri("Themes/DarkTheme.xaml", UriKind.Relative)
                : new Uri("Themes/LightTheme.xaml", UriKind.Relative);

            mergedDictionaries.Add(new ResourceDictionary { Source = themeUri });

            ThemeIcon.Data = _isDarkTheme
                ? (Geometry)FindResource("SunIcon")
                : (Geometry)FindResource("MoonIcon");
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

        private void DataGrid_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var row = FindParent<DataGridRow>(e.OriginalSource as DependencyObject);
            if (row?.DataContext is SyncTask task)
            {
                ViewModel!.SelectedTask = task;

                TaskContextMenu.Hide();
                TaskContextMenu.AddItem("立即执行", (Geometry)FindResource("PlayIcon"), () => ViewModel.ExecuteTaskCommand.Execute(null));
                TaskContextMenu.AddItem("编辑", (Geometry)FindResource("EditIcon"), () => ViewModel.EditTask());
                TaskContextMenu.AddSeparator();
                TaskContextMenu.AddItem(task.IsEnabled ? "禁用任务" : "启用任务", (Geometry)FindResource("SettingsIcon"), () => ViewModel.ToggleTaskEnabledCommand.Execute(null));
                TaskContextMenu.AddSeparator();
                TaskContextMenu.AddItem("打开源目录", (Geometry)FindResource("FolderIcon"), () => OpenSourceDirectory_Click(null, null));
                TaskContextMenu.AddItem("打开目标目录", (Geometry)FindResource("FolderIcon"), () => OpenTargetDirectory_Click(null, null));
                TaskContextMenu.AddSeparator();
                TaskContextMenu.AddItem("查看日志", (Geometry)FindResource("ClipboardIcon"), () => ViewModel.ViewLogsCommand.Execute(null));
                TaskContextMenu.AddSeparator();
                TaskContextMenu.AddItem("删除", (Geometry)FindResource("TrashIcon"), () => ViewModel.DeleteTaskCommand.Execute(null), isDanger: true);

                TaskContextMenu.Show();
            }
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

        private void OpenSourceDirectory_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.SelectedTask == null) return;

            var path = ViewModel.SelectedTask.SourcePath;
            if (System.IO.Directory.Exists(path))
            {
                System.Diagnostics.Process.Start("explorer.exe", path);
            }
            else
            {
                MessageBox.Show($"源目录不存在：\n{path}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenTargetDirectory_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.SelectedTask == null) return;

            var path = ViewModel.SelectedTask.TargetPath;
            if (System.IO.Directory.Exists(path))
            {
                System.Diagnostics.Process.Start("explorer.exe", path);
            }
            else
            {
                MessageBox.Show($"目标目录不存在：\n{path}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
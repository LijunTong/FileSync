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
            UpdateMaximizeButton();
            StateChanged += MainWindow_StateChanged;
        }

        private void MainWindow_StateChanged(object sender, System.EventArgs e)
        {
            UpdateMaximizeButton();
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
            UpdateMaximizeButton();
        }

        private void UpdateMaximizeButton()
        {
            if (WindowState == WindowState.Maximized)
            {
                MaximizeButton.Content = "◱";
                MainBorder.CornerRadius = new CornerRadius(0);
                MainBorder.BorderThickness = new Thickness(0);
                MainShadow.Opacity = 0;
            }
            else
            {
                MaximizeButton.Content = "□";
                MainBorder.CornerRadius = new CornerRadius(16);
                MainBorder.BorderThickness = new Thickness(1);
                MainShadow.Opacity = 0.15;
            }
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
            
            // 移除旧的主题字典
            for (int i = mergedDictionaries.Count - 1; i >= 0; i--)
            {
                var dict = mergedDictionaries[i];
                if (dict.Source?.OriginalString?.Contains("LightTheme") == true ||
                    dict.Source?.OriginalString?.Contains("DarkTheme") == true)
                {
                    mergedDictionaries.RemoveAt(i);
                }
            }
            
            // 添加新的主题字典
            var themeUri = _isDarkTheme 
                ? new Uri("Themes/DarkTheme.xaml", UriKind.Relative)
                : new Uri("Themes/LightTheme.xaml", UriKind.Relative);
            
            mergedDictionaries.Add(new ResourceDictionary { Source = themeUri });

            ThemeToggleButton.Content = _isDarkTheme ? "☀️" : "🌙";
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

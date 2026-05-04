using System;
using System.Windows;
using System.Windows.Input;

namespace FileSync.Views
{
    public partial class LogWindow : Window
    {
        public LogWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;
            StateChanged += OnStateChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateMaximizeButtonContent();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleWindowState();
            }
            else
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleWindowState();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ToggleWindowState()
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowState = WindowState.Maximized;
            }
        }

        private void OnStateChanged(object sender, EventArgs e)
        {
            UpdateWindowStateChanged();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Ensure borders change isn't needed for this window
        }

        private void UpdateWindowStateChanged()
        {
            if (WindowState == WindowState.Maximized)
            {
                MainBorder.CornerRadius = new CornerRadius(0);
                MainShadow.Opacity = 0;
                ResizeMode = ResizeMode.CanMinimize;
                this.MaxWidth = double.PositiveInfinity;
                this.MaxHeight = double.PositiveInfinity;
                this.MinWidth = 980;
                this.MinHeight = 680;
            }
            else
            {
                MainBorder.CornerRadius = new CornerRadius(16);
                MainShadow.Opacity = 0.15;
                ResizeMode = ResizeMode.CanResize;
            }
            UpdateMaximizeButtonContent();
        }

        private void UpdateMaximizeButtonContent()
        {
            if (WindowState == WindowState.Maximized)
            {
                MaximizeButton.Content = "◱";
            }
            else
            {
                MaximizeButton.Content = "□";
            }
        }
    }
}

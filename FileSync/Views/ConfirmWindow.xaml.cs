using System.Windows;
using System.Windows.Input;

namespace FileSync.Views
{
    public partial class ConfirmWindow : Window
    {
        public bool Result { get; private set; }

        public ConfirmWindow(string title, string message, string confirmText = "确定", string cancelText = "取消")
        {
            InitializeComponent();
            TitleText.Text = title;
            MessageText.Text = message;
            ConfirmButton.Content = confirmText;
            CancelButton.Content = cancelText;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // 禁用双击最大化
                return;
            }
            DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            DialogResult = false;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            DialogResult = false;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            Result = true;
            DialogResult = true;
        }
    }
}

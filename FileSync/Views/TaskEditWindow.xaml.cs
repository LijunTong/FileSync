using System.Windows;
using FileSync.ViewModels;

namespace FileSync.Views
{
    public partial class TaskEditWindow : Window
    {
        public TaskEditWindow()
        {
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is TaskEditViewModel vm)
            {
                vm.SaveCommand.Execute(null);
                DialogResult = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

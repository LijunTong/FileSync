using System;
using System.Globalization;
using System.Windows.Data;
using FileSync.Models;

namespace FileSync.Converters
{
    public class StatusIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SyncStatus status)
            {
                return status switch
                {
                    SyncStatus.Success => "✅",
                    SyncStatus.Failed  => "❌",
                    SyncStatus.Running => "⏳",
                    _                  => "—"
                };
            }
            return "—";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

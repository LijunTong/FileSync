using System;
using System.Globalization;
using System.Windows.Data;
using FileSync.Models;

namespace FileSync.Converters
{
    public class LogActionTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogAction action)
            {
                return action switch
                {
                    LogAction.Copy     => "📥 复制",
                    LogAction.Delete   => "🗑 删除",
                    LogAction.Skip     => "⏭ 跳过",
                    LogAction.Conflict => "⚠️ 冲突",
                    LogAction.Error    => "❌ 错误",
                    _                  => action.ToString()
                };
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

using System;
using System.Globalization;
using System.Windows.Data;

namespace FileSync.Converters
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;
            
            string? valueStr = value.ToString();
            string? paramStr = parameter.ToString();
            
            return valueStr?.Equals(paramStr, StringComparison.OrdinalIgnoreCase) == true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter != null)
        {
            try
            {
                Type enumType = targetType;
                if (enumType.IsGenericType && enumType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    enumType = Nullable.GetUnderlyingType(enumType)!;
                }
                return Enum.Parse(enumType, parameter.ToString()!, true);
            }
            catch
            {
                // Do nothing
            }
        }
        return Binding.DoNothing;
    }
    }
}

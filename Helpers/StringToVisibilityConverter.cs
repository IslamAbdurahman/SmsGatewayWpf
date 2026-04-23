using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SmsGatewayApp.Helpers
{
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return Visibility.Collapsed;
            
            bool reverse = false;
            string param = parameter.ToString()!;
            if (param.StartsWith("!"))
            {
                reverse = true;
                param = param.Substring(1);
            }

            bool match = value.ToString() == param;
            if (reverse) match = !match;

            return match ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

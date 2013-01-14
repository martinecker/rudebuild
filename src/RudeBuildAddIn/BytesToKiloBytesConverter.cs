using System;
using System.Windows;
using System.Windows.Data;
using System.Globalization;

namespace RudeBuildAddIn
{
    public class BytesToKiloBytesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                long valueInBytes = (long)value;
                return valueInBytes / 1024;
            }
            catch
            {
                return DependencyProperty.UnsetValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return DependencyProperty.UnsetValue;

            try
            {
                long valueInKiloBytes = Int64.Parse((string)value);
                return valueInKiloBytes * 1024;
            }
            catch
            {
                return DependencyProperty.UnsetValue;
            }
        }
    }
}

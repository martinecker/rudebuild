using System;
using System.Windows.Data;
using System.Globalization;

namespace RudeBuildAddIn
{
    public class BytesToKiloBytesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            long valueInBytes = (long)value;
            return valueInBytes / 1024;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            long valueInKiloBytes = (long)value;
            return valueInKiloBytes * 1024;
        }
    }
}

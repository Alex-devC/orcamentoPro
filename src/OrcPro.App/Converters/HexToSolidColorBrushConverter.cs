using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace OrcPro.App.Converters;

/// <summary>
/// Converte uma string hexadecimal de cor (ex: "#3B82F6") em um SolidColorBrush.
/// </summary>
public sealed class HexToSolidColorBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(color);
            }
            catch
            {
                // ignored
            }
        }
        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SolidColorBrush brush)
        {
            return brush.Color.ToString();
        }
        return string.Empty;
    }
}

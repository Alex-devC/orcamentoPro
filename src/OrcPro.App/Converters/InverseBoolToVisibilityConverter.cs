using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OrcPro.App.Converters;

/// <summary>
/// Inverte um valor booleano para visibilidade. True = Collapsed, False = Visible.
/// </summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isTrue = value is bool b && b;
        return isTrue ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v != Visibility.Visible;
    }
}

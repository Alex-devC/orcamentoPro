using System;
using System.Globalization;
using System.Windows.Data;
using OrcPro.Domain.Common;

namespace OrcPro.App.Converters;

/// <summary>
/// Formata CPF/CNPJ para exibição no grid (a base guarda apenas dígitos).
/// </summary>
public class DocumentoFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var texto = value as string;
        return string.IsNullOrWhiteSpace(texto) ? string.Empty : CpfCnpjValidator.Formatar(texto);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value;
}
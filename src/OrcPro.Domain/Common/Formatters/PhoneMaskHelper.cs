namespace OrcPro.Domain.Common.Formatters;

/// <summary>
/// Helper para formatação e validação de telefone/celular brasileiro.
/// Suporta 10 dígitos (fixo) e 11 dígitos (celular com 9º dígito).
/// Persistência: apenas dígitos.
/// </summary>
public static class PhoneMaskHelper
{
    /// <summary>
    /// Remove tudo que não é dígito.
    /// </summary>
    public static string Normalizar(string? valor)
        => new string((valor ?? string.Empty).Where(char.IsDigit).ToArray());

    /// <summary>
    /// Formata para exibição:
    /// - 10 dígitos: (00) 0000-0000
    /// - 11 dígitos: (00) 00000-0000
    /// </summary>
    public static string Formatar(string? valor)
    {
        var digitos = Normalizar(valor);

        return digitos.Length switch
        {
            10 => $"({digitos[..2]}) {digitos[2..6]}-{digitos[6..10]}",
            11 => $"({digitos[..2]}) {digitos[2..7]}-{digitos[7..11]}",
            _ => valor?.Trim() ?? string.Empty
        };
    }

    /// <summary>
    /// Aplica máscara durante a digitação (para uso em TextBox com formatação progressiva).
    /// </summary>
    public static string AplicarMascaraDigitacao(string? valor)
    {
        var digitos = Normalizar(valor);

        if (digitos.Length == 0)
            return string.Empty;

        if (digitos.Length <= 2)
            return $"({digitos}";

        if (digitos.Length <= 6)
            return $"({digitos[..2]}) {digitos[2..]}";

        if (digitos.Length <= 10)
            return $"({digitos[..2]}) {digitos[2..6]}-{digitos[6..]}";

        // 11 dígitos (celular)
        return $"({digitos[..2]}) {digitos[2..7]}-{digitos[7..11]}";
    }

    /// <summary>
    /// Indica se o telefone tem formato válido (10 ou 11 dígitos).
    /// </summary>
    public static bool EhValido(string? valor)
    {
        var digitos = Normalizar(valor);
        return digitos.Length == 10 || digitos.Length == 11;
    }

    /// <summary>
    /// Indica se é celular (11 dígitos com 9º dígito = 9).
    /// </summary>
    public static bool EhCelular(string? valor)
    {
        var digitos = Normalizar(valor);
        return digitos.Length == 11 && digitos[2] == '9';
    }

    /// <summary>
    /// Indica se é telefone fixo (10 dígitos ou 11 dígitos sem 9º dígito).
    /// </summary>
    public static bool EhFixo(string? valor)
    {
        var digitos = Normalizar(valor);
        return digitos.Length == 10 || (digitos.Length == 11 && digitos[2] != '9');
    }
}
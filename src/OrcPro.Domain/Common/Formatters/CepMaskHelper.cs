namespace OrcPro.Domain.Common.Formatters;

/// <summary>
/// Helper para formatação e validação de CEP brasileiro.
/// Máscara visual: 00000-000
/// Persistência: apenas dígitos (8 dígitos).
/// </summary>
public static class CepMaskHelper
{
    /// <summary>
    /// Remove tudo que não é dígito.
    /// </summary>
    public static string Normalizar(string? valor)
        => new string((valor ?? string.Empty).Where(char.IsDigit).ToArray());

    /// <summary>
    /// Formata para exibição: 00000-000
    /// </summary>
    public static string Formatar(string? valor)
    {
        var digitos = Normalizar(valor);

        if (digitos.Length == 8)
            return $"{digitos[..5]}-{digitos[5..8]}";

        return valor?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Aplica máscara durante a digitação.
    /// </summary>
    public static string AplicarMascaraDigitacao(string? valor)
    {
        var digitos = Normalizar(valor);

        if (digitos.Length == 0)
            return string.Empty;

        if (digitos.Length <= 5)
            return digitos;

        return $"{digitos[..5]}-{digitos[5..]}";
    }

    /// <summary>
    /// Indica se o CEP tem formato válido (8 dígitos).
    /// </summary>
    public static bool EhValido(string? valor)
    {
        var digitos = Normalizar(valor);
        return digitos.Length == 8;
    }

    /// <summary>
    /// Verifica se o CEP está completo para consulta (8 dígitos).
    /// </summary>
    public static bool EstaCompletoParaConsulta(string? valor)
        => EhValido(valor);
}
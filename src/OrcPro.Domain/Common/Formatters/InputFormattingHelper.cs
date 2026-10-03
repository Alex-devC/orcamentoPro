namespace OrcPro.Domain.Common.Formatters;

/// <summary>
/// Helper centralizado para formatação e normalização de entrada de texto.
/// Reutilizável por todos os módulos (Domain, Application, App).
/// </summary>
public static class InputFormattingHelper
{
    /// <summary>
    /// Converte texto para MAIÚSCULO (padrão do sistema para campos de texto).
    /// </summary>
    public static string ToUpperCase(string? value)
        => (value ?? string.Empty).ToUpperInvariant();

    /// <summary>
    /// Converte e-mail para minúsculo (única exceção à regra de maiúsculas).
    /// </summary>
    public static string ToLowerCaseEmail(string? value)
        => (value ?? string.Empty).ToLowerInvariant().Trim();

    /// <summary>
    /// Normaliza texto genérico: trim + upper case.
    /// </summary>
    public static string? NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return value.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Normaliza e-mail: trim + lower case.
    /// </summary>
    public static string? NormalizeEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return value.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Aplica formatação condicional: upper case para texto, lower case para e-mail.
    /// </summary>
    public static string FormatField(string? value, bool isEmail = false)
        => isEmail ? ToLowerCaseEmail(value) : ToUpperCase(value);
}

/// <summary>
/// Cache simples para evitar consultas de CEP duplicadas ao mesmo valor.
/// Thread-safe e reutilizável por ViewModels e TextFormattingBehavior.
/// </summary>
public sealed class CepQueryCache
{
    private string? _ultimoCep;

    /// <summary>
    /// Verifica se o CEP já foi consultado nesta instância.
    /// Se sim, retorna true (não precisa consultar novamente).
    /// Se não, registra e retorna false.
    /// </summary>
    public bool ShouldQuery(string cepNormalizado)
    {
        if (string.Equals(_ultimoCep, cepNormalizado, StringComparison.Ordinal))
            return false;

        _ultimoCep = cepNormalizado;
        return true;
    }

    /// <summary>
    /// Reseta o cache (chamar ao abrir novo formulário ou limpar o campo CEP).
    /// </summary>
    public void Reset()
    {
        _ultimoCep = null;
    }
}
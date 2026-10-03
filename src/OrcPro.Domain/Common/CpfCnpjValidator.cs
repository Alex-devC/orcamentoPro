using OrcPro.Domain.Common.Formatters;

namespace OrcPro.Domain.Common;

/// <summary>
/// Regras de validação de documentos brasileiros usadas pelos cadastros.
/// Regra de negócio pura (sem dependências), compartilhada entre serviços.
/// Aceita o valor com ou sem máscara: a comparação é feita apenas sobre os dígitos.
/// Mantém compatibilidade com API anterior (apenas CPF e CNPJ numérico).
/// </summary>
public static class CpfCnpjValidator
{
    /// <summary>Remove tudo que não é dígito (compatibilidade legada: CPF/CNPJ numérico).
    /// Para CNPJ alfanumérico, use OrcPro.Domain.Common.Formatters.CpfCnpjValidator.NormalizarGenerico.</summary>
    public static string Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return string.Empty;
        return new string(valor.Where(char.IsDigit).ToArray());
    }

    /// <summary>
    /// Indica se o valor informado é um CPF (11 dígitos) ou CNPJ (14 dígitos) válido,
    /// incluindo a conferência dos dígitos verificadores.
    /// Mantém compatibilidade: valida apenas CPF e CNPJ numérico tradicional.
    /// </summary>
    public static bool EhValido(string? valor)
    {
        var result = Formatters.CpfCnpjValidator.Validar(valor);
        return result.IsValid && (result.Tipo == DocumentoTipo.Cpf || result.Tipo == DocumentoTipo.CnpjNumerico);
    }

    /// <summary>Formata para exibição (CPF 000.000.000-00 / CNPJ 00.000.000/0000-00).</summary>
    public static string Formatar(string? valor)
        => Formatters.CpfCnpjValidator.Formatar(valor);

    // Métodos privados mantidos para compatibilidade com código que os use via reflexão
    // (não são públicos na nova API)
    private static bool EhCpf(string digitos) => Formatters.CpfCnpjValidator.Validar(digitos).IsValid;
    private static bool EhCnpj(string digitos) => Formatters.CpfCnpjValidator.Validar(digitos).IsValid;
}
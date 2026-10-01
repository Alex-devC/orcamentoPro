namespace OrcPro.Domain.Common;

/// <summary>
/// Regras de validação de documentos brasileiros usadas pelos cadastros.
/// Regra de negócio pura (sem dependências), compartilhada entre serviços.
/// Aceita o valor com ou sem máscara: a comparação é feita apenas sobre os dígitos.
/// </summary>
public static class CpfCnpjValidator
{
    /// <summary>Remove tudo que não é dígito (máscaras, pontos, barras, hífens e espaços).</summary>
    public static string Normalizar(string? valor)
        => new string((valor ?? string.Empty).Where(char.IsDigit).ToArray());

    /// <summary>
    /// Indica se o valor informado é um CPF (11 dígitos) ou CNPJ (14 dígitos) válido,
    /// incluindo a conferência dos dígitos verificadores.
    /// </summary>
    public static bool EhValido(string? valor)
    {
        var digitos = Normalizar(valor);

        return digitos.Length switch
        {
            11 => EhCpf(digitos),
            14 => EhCnpj(digitos),
            _ => false
        };
    }

    /// <summary>Formata para exibição (CPF 000.000.000-00 / CNPJ 00.000.000/0000-00).</summary>
    public static string Formatar(string? valor)
    {
        var digitos = Normalizar(valor);

        if (digitos.Length == 11)
        {
            return $"{digitos[..3]}.{digitos[3..6]}.{digitos[6..9]}-{digitos[9..11]}";
        }

        if (digitos.Length == 14)
        {
            return $"{digitos[..2]}.{digitos[2..5]}.{digitos[5..8]}/{digitos[8..12]}-{digitos[12..14]}";
        }

        return valor?.Trim() ?? string.Empty;
    }

    private static bool EhCpf(string digitos)
    {
        // Sequências de dígitos repetidos (000..., 111...) passam no cálculo, mas não são válidos.
        if (TodosIguais(digitos))
            return false;

        var digito1 = CalcularDigito(digitos, 9, 10);
        if (digitos[9] - '0' != digito1)
            return false;

        var digito2 = CalcularDigito(digitos, 10, 11);
        return digitos[10] - '0' == digito2;
    }

    private static bool EhCnpj(string digitos)
    {
        if (TodosIguais(digitos))
            return false;

        var pesos1 = new[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        var digito1 = CalcularDigitoComPesos(digitos, pesos1);
        if (digitos[12] - '0' != digito1)
            return false;

        var pesos2 = new[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        var digito2 = CalcularDigitoComPesos(digitos, pesos2);
        return digitos[13] - '0' == digito2;
    }

    private static int CalcularDigito(string digitos, int quantidade, int pesoInicial)
    {
        var soma = 0;
        for (var i = 0; i < quantidade; i++)
        {
            soma += (digitos[i] - '0') * (pesoInicial - i);
        }

        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    private static int CalcularDigitoComPesos(string digitos, int[] pesos)
    {
        var soma = 0;
        for (var i = 0; i < pesos.Length; i++)
        {
            soma += (digitos[i] - '0') * pesos[i];
        }

        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    private static bool TodosIguais(string digitos) => digitos.All(c => c == digitos[0]);
}
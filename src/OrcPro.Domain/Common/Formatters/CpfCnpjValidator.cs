namespace OrcPro.Domain.Common.Formatters;

/// <summary>
/// Tipos de documento suportados.
/// </summary>
public enum DocumentoTipo
{
    /// <summary>Não identificado.</summary>
    Desconhecido = 0,

    /// <summary>CPF - 11 dígitos numéricos.</summary>
    Cpf = 1,

    /// <summary>CNPJ tradicional - 14 dígitos numéricos.</summary>
    CnpjNumerico = 2,

    /// <summary>CNPJ alfanumérico oficial (Receita Federal/SERPRO) - 14 posições (12 alfanuméricas + 2 dígitos verificadores numéricos).</summary>
    CnpjAlfanumerico = 3
}

/// <summary>
/// Resultado da validação de CPF/CNPJ.
/// </summary>
public readonly record struct DocumentoValidationResult(
    bool IsValid,
    DocumentoTipo Tipo,
    string NormalizedValue,
    string FormattedValue,
    string? ErrorMessage = null
);

/// <summary>
/// Validador e formatador unificado para CPF e CNPJ (tradicional e alfanumérico).
/// Implementa as regras oficiais da Receita Federal/SERPRO.
/// </summary>
public static class CpfCnpjValidator
{
    // Pesos para cálculo do primeiro dígito verificador (CNPJ - 12 posições base)
    private static readonly int[] CnpjPesos1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
    // Pesos para cálculo do segundo dígito verificador (CNPJ - 13 posições base)
    private static readonly int[] CnpjPesos2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

    /// <summary>
    /// Remove tudo que não é alfanumérico (mantém A-Z e 0-9).
    /// Auto-detecta: para CNPJ alfanumérico (com letras), mantém letras e dígitos (maiúsculo).
    /// Para CPF/CNPJ numérico (sem letras), mantém apenas dígitos.
    /// </summary>
    public static string Normalizar(string? valor, bool permitirLetras = false)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return string.Empty;

        if (permitirLetras)
        {
            // CNPJ alfanumérico: mantém A-Z e 0-9, converte para maiúsculo
            return new string(valor
                .Where(c => char.IsLetterOrDigit(c))
                .Select(c => char.ToUpperInvariant(c))
                .ToArray());
        }

        // Auto-detecta: se tem letras, mantém alfanumérico; caso contrário, apenas dígitos
        var temLetras = valor.Any(c => char.IsLetter(c));
        if (temLetras)
        {
            return new string(valor
                .Where(c => char.IsLetterOrDigit(c))
                .Select(c => char.ToUpperInvariant(c))
                .ToArray());
        }

        // CPF/CNPJ numérico: mantém apenas dígitos
        return new string(valor.Where(char.IsDigit).ToArray());
    }

    /// <summary>
    /// Normaliza CPF (apenas dígitos).
    /// </summary>
    public static string NormalizarCpf(string? valor)
        => Normalizar(valor, permitirLetras: false);

    /// <summary>
    /// Normaliza CNPJ (suporta tanto numérico quanto alfanumérico).
    /// Detecta automaticamente se contém letras.
    /// </summary>
    public static string NormalizarCnpj(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return string.Empty;

        var temLetras = valor.Any(c => char.IsLetter(c));
        return Normalizar(valor, permitirLetras: temLetras);
    }

    /// <summary>
    /// Normaliza genericamente: detecta se é CPF (11 dígitos), CNPJ numérico (14 dígitos) ou CNPJ alfanumérico (14 posições com letras).
    /// </summary>
    public static string NormalizarGenerico(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return string.Empty;

        var apenasDigitos = new string(valor.Where(char.IsDigit).ToArray());
        var temLetras = valor.Any(c => char.IsLetter(c));

        // Se tem letras, é CNPJ alfanumérico
        if (temLetras)
            return new string(valor
                .Where(c => char.IsLetterOrDigit(c))
                .Select(c => char.ToUpperInvariant(c))
                .ToArray());

        // Se não tem letras, usa apenas dígitos
        return apenasDigitos;
    }

    /// <summary>
    /// Identifica o tipo de documento baseado no valor normalizado.
    /// </summary>
    public static DocumentoTipo IdentificarTipo(string normalizado)
    {
        if (string.IsNullOrWhiteSpace(normalizado))
            return DocumentoTipo.Desconhecido;

        // CNPJ alfanumérico: 14 posições, contém letras nas primeiras 12
        if (normalizado.Length == 14 && normalizado[..12].Any(char.IsLetter))
            return DocumentoTipo.CnpjAlfanumerico;

        // CNPJ numérico tradicional: 14 dígitos
        if (normalizado.Length == 14 && normalizado.All(char.IsDigit))
            return DocumentoTipo.CnpjNumerico;

        // CPF: 11 dígitos
        if (normalizado.Length == 11 && normalizado.All(char.IsDigit))
            return DocumentoTipo.Cpf;

        return DocumentoTipo.Desconhecido;
    }

    /// <summary>
    /// Valida CPF ou CNPJ (numérico ou alfanumérico) e retorna resultado detalhado.
    /// </summary>
    public static DocumentoValidationResult Validar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return new DocumentoValidationResult(false, DocumentoTipo.Desconhecido, string.Empty, string.Empty, "Valor não informado");

        var trimado = valor.Trim();
        // Verifica caracteres inválidos: apenas alfanuméricos, pontos, barras, hífens e espaços são permitidos
        if (trimado.Any(c => !char.IsLetterOrDigit(c) && c != '.' && c != '/' && c != '-' && c != ' '))
        {
            return new DocumentoValidationResult(false, DocumentoTipo.Desconhecido, string.Empty, trimado, "Valor contém caracteres inválidos");
        }

        var normalizado = NormalizarGenerico(valor);
        var tipo = IdentificarTipo(normalizado);

        return tipo switch
        {
            DocumentoTipo.Cpf => ValidarCpf(normalizado),
            DocumentoTipo.CnpjNumerico => ValidarCnpjNumerico(normalizado),
            DocumentoTipo.CnpjAlfanumerico => ValidarCnpjAlfanumerico(normalizado),
            _ => new DocumentoValidationResult(false, DocumentoTipo.Desconhecido, normalizado, valor.Trim(), "Formato inválido: deve ser CPF (11 dígitos), CNPJ numérico (14 dígitos) ou CNPJ alfanumérico (14 posições)")
        };
    }

    /// <summary>
    /// Indica se o valor informado é um CPF ou CNPJ válido (compatibilidade com API existente).
    /// </summary>
    public static bool EhValido(string? valor)
        => Validar(valor).IsValid;

    /// <summary>
    /// Valida CPF (11 dígitos numéricos).
    /// </summary>
    private static DocumentoValidationResult ValidarCpf(string digitos)
    {
        if (digitos.Length != 11 || !digitos.All(char.IsDigit))
            return new DocumentoValidationResult(false, DocumentoTipo.Cpf, digitos, digitos, "CPF deve ter 11 dígitos numéricos");

        if (TodosIguais(digitos))
            return new DocumentoValidationResult(false, DocumentoTipo.Cpf, digitos, FormatarCpf(digitos), "CPF inválido: dígitos repetidos");

        var digito1 = CalcularDigitoCpf(digitos, 9, 10);
        if (digitos[9] - '0' != digito1)
            return new DocumentoValidationResult(false, DocumentoTipo.Cpf, digitos, FormatarCpf(digitos), "CPF inválido: primeiro dígito verificador incorreto");

        var digito2 = CalcularDigitoCpf(digitos, 10, 11);
        if (digitos[10] - '0' != digito2)
            return new DocumentoValidationResult(false, DocumentoTipo.Cpf, digitos, FormatarCpf(digitos), "CPF inválido: segundo dígito verificador incorreto");

        return new DocumentoValidationResult(true, DocumentoTipo.Cpf, digitos, FormatarCpf(digitos));
    }

    /// <summary>
    /// Valida CNPJ numérico tradicional (14 dígitos).
    /// </summary>
    private static DocumentoValidationResult ValidarCnpjNumerico(string digitos)
    {
        if (digitos.Length != 14 || !digitos.All(char.IsDigit))
            return new DocumentoValidationResult(false, DocumentoTipo.CnpjNumerico, digitos, digitos, "CNPJ numérico deve ter 14 dígitos");

        if (TodosIguais(digitos))
            return new DocumentoValidationResult(false, DocumentoTipo.CnpjNumerico, digitos, FormatarCnpjNumerico(digitos), "CNPJ inválido: dígitos repetidos");

        var digito1 = CalcularDigitoCnpj(digitos, CnpjPesos1);
        if (digitos[12] - '0' != digito1)
            return new DocumentoValidationResult(false, DocumentoTipo.CnpjNumerico, digitos, FormatarCnpjNumerico(digitos), "CNPJ inválido: primeiro dígito verificador incorreto");

        var digito2 = CalcularDigitoCnpj(digitos, CnpjPesos2);
        if (digitos[13] - '0' != digito2)
            return new DocumentoValidationResult(false, DocumentoTipo.CnpjNumerico, digitos, FormatarCnpjNumerico(digitos), "CNPJ inválido: segundo dígito verificador incorreto");

        return new DocumentoValidationResult(true, DocumentoTipo.CnpjNumerico, digitos, FormatarCnpjNumerico(digitos));
    }

    /// <summary>
    /// Valida CNPJ alfanumérico oficial (Receita Federal/SERPRO).
    /// 14 posições: 12 alfanuméricas (A-Z, 0-9) + 2 dígitos verificadores numéricos.
    /// Algoritmo módulo 11 com conversão oficial de caracteres.
    /// </summary>
    private static DocumentoValidationResult ValidarCnpjAlfanumerico(string valor)
    {
        if (valor.Length != 14)
            return new DocumentoValidationResult(false, DocumentoTipo.CnpjAlfanumerico, valor, valor, "CNPJ alfanumérico deve ter 14 posições");

        // Primeiras 12 posições: A-Z ou 0-9
        var baseCnpj = valor[..12];
        if (baseCnpj.Any(c => !char.IsLetterOrDigit(c)))
            return new DocumentoValidationResult(false, DocumentoTipo.CnpjAlfanumerico, valor, FormatarCnpjAlfanumerico(valor), "CNPJ alfanumérico: caracteres inválidos nas 12 primeiras posições (use apenas A-Z e 0-9)");

        // Últimas 2 posições: apenas dígitos numéricos
        var digitosVerificadores = valor[12..];
        if (!digitosVerificadores.All(char.IsDigit))
            return new DocumentoValidationResult(false, DocumentoTipo.CnpjAlfanumerico, valor, FormatarCnpjAlfanumerico(valor), "CNPJ alfanumérico: os 2 últimos caracteres devem ser dígitos numéricos (0-9)");

        // Converter base para valores numéricos (0-9 mantêm valor, A=10, B=11, ..., Z=35)
        var valores = new int[12];
        for (int i = 0; i < 12; i++)
        {
            var c = baseCnpj[i];
            valores[i] = char.IsDigit(c) ? c - '0' : (c - 'A' + 10);
        }

        // Calcular primeiro dígito verificador (peso 5 a 2, depois 9 a 2)
        var soma1 = 0;
        for (int i = 0; i < 12; i++)
        {
            soma1 += valores[i] * CnpjPesos1[i];
        }
        var resto1 = soma1 % 11;
        var digito1Calculado = resto1 < 2 ? 0 : 11 - resto1;
        var digito1Informado = valor[12] - '0';

        if (digito1Calculado != digito1Informado)
            return new DocumentoValidationResult(false, DocumentoTipo.CnpjAlfanumerico, valor, FormatarCnpjAlfanumerico(valor), "CNPJ alfanumérico inválido: primeiro dígito verificador incorreto");

        // Calcular segundo dígito verificador (inclui o primeiro dígito verificado)
        var valoresComDigito1 = new int[13];
        Array.Copy(valores, valoresComDigito1, 12);
        valoresComDigito1[12] = digito1Informado;

        var soma2 = 0;
        for (int i = 0; i < 13; i++)
        {
            soma2 += valoresComDigito1[i] * CnpjPesos2[i];
        }
        var resto2 = soma2 % 11;
        var digito2Calculado = resto2 < 2 ? 0 : 11 - resto2;
        var digito2Informado = valor[13] - '0';

        if (digito2Calculado != digito2Informado)
            return new DocumentoValidationResult(false, DocumentoTipo.CnpjAlfanumerico, valor, FormatarCnpjAlfanumerico(valor), "CNPJ alfanumérico inválido: segundo dígito verificador incorreto");

        return new DocumentoValidationResult(true, DocumentoTipo.CnpjAlfanumerico, valor, FormatarCnpjAlfanumerico(valor));
    }

    /// <summary>
    /// Formata para exibição (detecta automaticamente o tipo).
    /// CPF: 000.000.000-00
    /// CNPJ numérico: 00.000.000/0000-00
    /// CNPJ alfanumérico: AA.AAA.AAA/AAAA-00
    /// </summary>
    public static string Formatar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return string.Empty;

        var normalizado = NormalizarGenerico(valor);
        var tipo = IdentificarTipo(normalizado);

        return tipo switch
        {
            DocumentoTipo.Cpf => FormatarCpf(normalizado),
            DocumentoTipo.CnpjNumerico => FormatarCnpjNumerico(normalizado),
            DocumentoTipo.CnpjAlfanumerico => FormatarCnpjAlfanumerico(normalizado),
            _ => valor.Trim()
        };
    }

    /// <summary>Formata CPF: 000.000.000-00</summary>
    public static string FormatarCpf(string digitos)
    {
        if (digitos.Length != 11)
            return digitos;
        return $"{digitos[..3]}.{digitos[3..6]}.{digitos[6..9]}-{digitos[9..11]}";
    }

    /// <summary>Formata CNPJ numérico: 00.000.000/0000-00</summary>
    public static string FormatarCnpjNumerico(string digitos)
    {
        if (digitos.Length != 14)
            return digitos;
        return $"{digitos[..2]}.{digitos[2..5]}.{digitos[5..8]}/{digitos[8..12]}-{digitos[12..14]}";
    }

    /// <summary>Formata CNPJ alfanumérico: AA.AAA.AAA/AAAA-00</summary>
    public static string FormatarCnpjAlfanumerico(string valor)
    {
        if (valor.Length != 14)
            return valor;
        // Padrão: AA.AAA.AAA/AAAA-00
        return $"{valor[..2]}.{valor[2..5]}.{valor[5..8]}/{valor[8..12]}-{valor[12..14]}";
    }

    /// <summary>
    /// Calcula dígito verificador do CPF.
    /// </summary>
    private static int CalcularDigitoCpf(string digitos, int quantidade, int pesoInicial)
    {
        var soma = 0;
        for (var i = 0; i < quantidade; i++)
        {
            soma += (digitos[i] - '0') * (pesoInicial - i);
        }
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    /// <summary>
    /// Calcula dígito verificador do CNPJ usando pesos fornecidos.
    /// </summary>
    private static int CalcularDigitoCnpj(string digitos, int[] pesos)
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

    /// <summary>
    /// Gera CPF válido para testes (apenas dígitos).
    /// </summary>
    public static string GerarCpfValido()
    {
        var random = new Random();
        var baseCpf = new char[9];
        for (int i = 0; i < 9; i++)
            baseCpf[i] = (char)('0' + random.Next(10));

        var digitos = new string(baseCpf);
        var d1 = CalcularDigitoCpf(digitos, 9, 10);
        var d2 = CalcularDigitoCpf(digitos + d1, 10, 11);
        return digitos + d1 + d2;
    }

    /// <summary>
    /// Gera CNPJ numérico válido para testes (apenas dígitos).
    /// </summary>
    public static string GerarCnpjNumericoValido()
    {
        var random = new Random();
        var baseCnpj = new char[12];
        for (int i = 0; i < 12; i++)
            baseCnpj[i] = (char)('0' + random.Next(10));

        var digitos = new string(baseCnpj);
        var d1 = CalcularDigitoCnpj(digitos, CnpjPesos1);
        var d2 = CalcularDigitoCnpj(digitos + d1, CnpjPesos2);
        return digitos + d1 + d2;
    }

    /// <summary>
    /// Gera CNPJ alfanumérico válido para testes.
    /// </summary>
    public static string GerarCnpjAlfanumericoValido()
    {
        var random = new Random();
        const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var baseCnpj = new char[12];
        for (int i = 0; i < 12; i++)
            baseCnpj[i] = chars[random.Next(chars.Length)];

        var baseStr = new string(baseCnpj);

        // Calcular primeiro dígito
        var valores = new int[12];
        for (int i = 0; i < 12; i++)
        {
            var c = baseStr[i];
            valores[i] = char.IsDigit(c) ? c - '0' : (c - 'A' + 10);
        }

        var soma1 = 0;
        for (int i = 0; i < 12; i++)
            soma1 += valores[i] * CnpjPesos1[i];
        var resto1 = soma1 % 11;
        var d1 = resto1 < 2 ? 0 : 11 - resto1;

        // Calcular segundo dígito
        var valoresComD1 = new int[13];
        Array.Copy(valores, valoresComD1, 12);
        valoresComD1[12] = d1;

        var soma2 = 0;
        for (int i = 0; i < 13; i++)
            soma2 += valoresComD1[i] * CnpjPesos2[i];
        var resto2 = soma2 % 11;
        var d2 = resto2 < 2 ? 0 : 11 - resto2;

        return baseStr + d1 + d2;
    }
}
using System.Globalization;
using System.Linq;

namespace OrcPro.Domain.Common.Formatters;

/// <summary>
/// Conversão de valores numéricos digitados pelo usuário (padrão brasileiro).
///
/// <para><b>Princípio: digitação natural → validação → conversão.</b> Esta classe nunca
/// reescreve o que o usuário digitou; apenas interpreta o texto no momento de salvar.
/// Isso elimina o problema clássico de máscara em <c>TextBox</c>, no qual digitar "1"
/// vira "1.000" ou "10,50" vira "1.050,00".</para>
///
/// <para>Regras de leitura (pt-BR): vírgula é o separador decimal e ponto é o
/// separador de milhar. Sem vírgula, um ponto seguido de exatamente três dígitos é
/// milhar ("1.000" = 1000); nos demais casos é decimal ("1.5" = 1,5).</para>
/// </summary>
public static class DecimalInputHelper
{
    /// <summary>
    /// Tenta converter o texto digitado em <see cref="decimal"/>. Nunca lança e nunca
    /// reformata a entrada. Texto vazio ou inválido devolve <c>false</c>.
    /// </summary>
    public static bool TentarConverter(string? texto, out decimal valor)
    {
        valor = 0m;

        if (string.IsNullOrWhiteSpace(texto))
            return false;

        var bruto = texto.Trim();
        var negativo = bruto.StartsWith('-');

        if (negativo)
            bruto = bruto[1..].TrimStart();

        var corte = LocalizarSeparadorDecimal(bruto);

        var parteInteira = corte is null ? bruto : bruto[..corte.Value];
        var parteFracao = corte is null ? string.Empty : bruto[(corte.Value + 1)..];

        var digitosInteiros = new string(parteInteira.Where(char.IsDigit).ToArray());
        var digitosFracao = new string(parteFracao.Where(char.IsDigit).ToArray());

        if (digitosInteiros.Length == 0 && digitosFracao.Length == 0)
            return false;

        if (digitosInteiros.Length == 0)
            digitosInteiros = "0";

        if (!decimal.TryParse(digitosInteiros, NumberStyles.Integer, CultureInfo.InvariantCulture, out var inteiro))
            return false;

        decimal resultado = inteiro;

        if (digitosFracao.Length > 0 &&
            decimal.TryParse("0." + digitosFracao, NumberStyles.Float, CultureInfo.InvariantCulture, out var fracao))
        {
            resultado += fracao;
        }

        valor = negativo ? -resultado : resultado;
        return true;
    }

    /// <summary>Converte o texto ou devolve <paramref name="padrao"/> quando inválido.</summary>
    public static decimal ConverterOuPadrao(string? texto, decimal padrao = 0m)
        => TentarConverter(texto, out var valor) ? valor : padrao;

    /// <summary>Indica se o texto digitado representa um número válido.</summary>
    public static bool EhValido(string? texto) => TentarConverter(texto, out _);

    /// <summary>
    /// Formata apenas para EXIBIÇÃO no padrão brasileiro (grid, ficha e resumos).
    /// Ex.: 1250,75 → "1.250,75". Nunca use durante a digitação.
    /// </summary>
    public static string FormatarMonetario(decimal valor, int casasDecimais = 2)
        => valor.ToString("N" + casasDecimais, CultureInfo.GetCultureInfo("pt-BR"));

    /// <summary>
    /// Formata apenas para EXIBIÇÃO de quantidades, omitindo zeros desnecessários
    /// (10,000 → "10"; 1,500 → "1,5").
    /// </summary>
    public static string FormatarQuantidade(decimal valor, int casasDecimais = 3)
    {
        var texto = valor.ToString("N" + casasDecimais, CultureInfo.GetCultureInfo("pt-BR"));

        return texto.Contains(',')
            ? texto.TrimEnd('0').TrimEnd(',')
            : texto;
    }

    /// <summary>
    /// Devolve o índice do separador decimal no texto, ou <c>null</c> quando o número
    /// é inteiro. Resolve a ambiguidade do ponto em português.
    /// </summary>
    private static int? LocalizarSeparadorDecimal(string bruto)
    {
        var ultimaVirgula = bruto.LastIndexOf(',');
        var ultimoPonto = bruto.LastIndexOf('.');

        // Vírgula depois do ponto: vírgula é sempre o decimal em pt-BR ("1.234,56").
        if (ultimaVirgula >= 0 && ultimaVirgula > ultimoPonto)
            return ultimaVirgula;

        if (ultimoPonto < 0)
            return null;

        var aDireita = bruto[(ultimoPonto + 1)..].Count(char.IsDigit);
        var aEsquerda = bruto[..ultimoPonto].Count(char.IsDigit);

        // "1.000" / "12.500": três dígitos à direita e um único ponto = separador de milhar.
        var ehMilhar = aDireita == 3 && aEsquerda is >= 1 and <= 3
            && bruto.Count(c => c == '.') == 1 && !bruto.Contains(',');

        return ehMilhar ? null : ultimoPonto;
    }
}
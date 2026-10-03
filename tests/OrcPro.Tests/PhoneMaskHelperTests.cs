using OrcPro.Domain.Common.Formatters;
using Xunit;

namespace OrcPro.Tests;

public class PhoneMaskHelperTests
{
    [Theory]
    [InlineData("11999998888", "11999998888")]
    [InlineData("(11) 99999-8888", "11999998888")]
    [InlineData("11 99999 8888", "11999998888")]
    [InlineData("  (11) 99999-8888  ", "11999998888")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalizar_DeveRemoverMascaraEDevolverApenasDigitos(string? input, string expected)
    {
        Assert.Equal(expected, PhoneMaskHelper.Normalizar(input));
    }

    [Theory]
    [InlineData("11999998888", "(11) 99999-8888")]
    [InlineData("1122223333", "(11) 2222-3333")]
    public void Formatar_DeveGerarMascaraCorreta(string input, string expected)
    {
        Assert.Equal(expected, PhoneMaskHelper.Formatar(input));
    }

    [Theory]
    [InlineData("1199999888", "(11) 9999-9888")]
    [InlineData("(11) 99999-8888", "(11) 99999-8888")]
    [InlineData("11999998888", "(11) 99999-8888")]
    public void AplicarMascaraDigitacao_DeveAplicarMascaraProgressiva(string input, string expected)
    {
        // AplicarMascaraDigitacao formata progressivamente
        var result = PhoneMaskHelper.AplicarMascaraDigitacao(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("11999998888", true)]
    [InlineData("1122223333", true)]
    [InlineData("123", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void EhValido_DeveRetornarTrueAPara10Ou11Digitos(string? input, bool expected)
    {
        Assert.Equal(expected, PhoneMaskHelper.EhValido(input));
    }

    [Theory]
    [InlineData("11999998888", true)] // 11 dígitos começando com 9 = celular
    [InlineData("11988887777", true)] // 11 dígitos começando com 9 = celular
    [InlineData("1122223333", false)] // 10 dígitos = fixo
    public void EhCelular_DeveIdentificarCelular(string input, bool expected)
    {
        Assert.Equal(expected, PhoneMaskHelper.EhCelular(input));
    }

    [Theory]
    [InlineData("1122223333", true)] // 10 dígitos = fixo
    [InlineData("11999998888", false)] // 11 dígitos começando com 9 = celular
    public void EhFixo_DeveIdentificarFixo(string input, bool expected)
    {
        Assert.Equal(expected, PhoneMaskHelper.EhFixo(input));
    }
}
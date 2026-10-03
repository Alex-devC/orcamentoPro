using OrcPro.Domain.Common.Formatters;
using Xunit;

namespace OrcPro.Tests;

public class CepMaskHelperTests
{
    [Theory]
    [InlineData("01310000", "01310000")]
    [InlineData("01310-000", "01310000")]
    [InlineData("  01310-000  ", "01310000")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalizar_DeveRemoverMascaraEDevolverApenasDigitos(string? input, string expected)
    {
        Assert.Equal(expected, CepMaskHelper.Normalizar(input));
    }

    [Theory]
    [InlineData("01310000", "01310-000")]
    [InlineData("01310-000", "01310-000")]
    public void Formatar_DeveGerarMascaraCorreta(string input, string expected)
    {
        Assert.Equal(expected, CepMaskHelper.Formatar(input));
    }

    [Theory]
    [InlineData("01310", "01310")]
    [InlineData("013100", "01310-0")]
    [InlineData("01310000", "01310-000")]
    public void AplicarMascaraDigitacao_DeveAplicarMascaraProgressiva(string input, string expected)
    {
        Assert.Equal(expected, CepMaskHelper.AplicarMascaraDigitacao(input));
    }

    [Theory]
    [InlineData("01310000", true)]
    [InlineData("01310-000", true)]
    [InlineData("01310", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void EhValido_DeveRetornarTruePara8Digitos(string? input, bool expected)
    {
        Assert.Equal(expected, CepMaskHelper.EhValido(input));
    }

    [Theory]
    [InlineData("01310000", true)]
    [InlineData("01310-000", true)]
    [InlineData("01310", false)]
    public void EstaCompletoParaConsulta_DeveRetornarTruePara8Digitos(string input, bool expected)
    {
        Assert.Equal(expected, CepMaskHelper.EstaCompletoParaConsulta(input));
    }
}
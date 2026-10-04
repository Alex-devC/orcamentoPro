using System;
using System.Collections.Generic;
using Xunit;
using OrcPro.Domain.Common.Formatters;

namespace OrcPro.Tests;

/// <summary>
/// Regressão do problema "digitar 1 vira 1000" na entrada numérica.
/// O texto digitado nunca é reformatado; a conversão só acontece na validação.
/// </summary>
public class DecimalInputHelperTests
{
    [Theory]
    // Regressão explícita: digitar "1" precisa continuar "1", nunca "1000".
    [InlineData("1", 1.0)]
    [InlineData("10", 10.0)]
    [InlineData("100", 100.0)]
    [InlineData("1000", 1000.0)]
    [InlineData("0", 0.0)]
    [InlineData("10,50", 10.50)]
    [InlineData("1,5", 1.5)]
    [InlineData("1250,75", 1250.75)]
    [InlineData("1.000", 1000.0)]
    [InlineData("12.500,75", 12500.75)]
    [InlineData("1,50", 1.50)]
    [InlineData("0,001", 0.001)]
    [InlineData("  1250,75  ", 1250.75)]
    [InlineData("R$ 1.250,75", 1250.75)]
    [InlineData("10,", 10.0)]
    [InlineData("1.5", 1.5)]
    public void TentarConverter_InterpretacaoBrasileira(string entrada, double esperado)
    {
        Assert.True(DecimalInputHelper.TentarConverter(entrada, out var valor), $"'{entrada}' deveria converter.");
        Assert.Equal((decimal)esperado, valor);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("10")]
    [InlineData("100")]
    [InlineData("1000")]
    [InlineData("10,50")]
    [InlineData("1250,75")]
    public void EntradaNaoEhReformatada_TextoPermaneceComoDigitado(string entrada)
    {
        // O helper nunca altera o texto recebido: apenas o interpreta.
        Assert.True(DecimalInputHelper.TentarConverter(entrada, out _));
        Assert.Equal(entrada, entrada);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("R$")]
    public void TentarConverter_TextoInvalido_RetornaFalse(string? entrada)
    {
        Assert.False(DecimalInputHelper.TentarConverter(entrada, out _));
        Assert.False(DecimalInputHelper.EhValido(entrada));
    }

    [Fact]
    public void ConverterOuPadrao_TextoVazio_UsaPadrao()
    {
        Assert.Equal(0m, DecimalInputHelper.ConverterOuPadrao(""));
        Assert.Equal(7m, DecimalInputHelper.ConverterOuPadrao(null, 7m));
    }

    [Fact]
    public void FormatarMonetario_UsarPadraoBrasileiro()
    {
        Assert.Equal("1.250,75", DecimalInputHelper.FormatarMonetario(1250.75m));
        Assert.Equal("0,00", DecimalInputHelper.FormatarMonetario(0m));
    }

    [Fact]
    public void FormatarQuantidade_RemoveZerosDesnecessarios()
    {
        Assert.Equal("10", DecimalInputHelper.FormatarQuantidade(10m));
        Assert.Equal("1,5", DecimalInputHelper.FormatarQuantidade(1.5m));
        Assert.Equal("0", DecimalInputHelper.FormatarQuantidade(0m));
    }
}
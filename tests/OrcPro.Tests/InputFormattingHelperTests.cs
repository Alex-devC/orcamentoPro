using OrcPro.Domain.Common.Formatters;
using Xunit;

namespace OrcPro.Tests;

public class InputFormattingHelperTests
{
    [Theory]
    [InlineData("texto normal", "TEXTO NORMAL")]
    [InlineData("AbCdEf", "ABCDEF")]
    [InlineData("123abc", "123ABC")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("   espaços   ", "   ESPAÇOS   ")]
    public void ToUpperCase_DeveConverterParaMaiusculo(string? input, string expected)
    {
        Assert.Equal(expected, InputFormattingHelper.ToUpperCase(input));
    }

    [Theory]
    [InlineData("Test@Email.COM", "test@email.com")]
    [InlineData("USER@DOMAIN.COM.BR", "user@domain.com.br")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("  Email@Site.Com  ", "email@site.com")]
    public void ToLowerCaseEmail_DeveConverterParaMinusculo(string? input, string expected)
    {
        Assert.Equal(expected, InputFormattingHelper.ToLowerCaseEmail(input));
    }

    [Theory]
    [InlineData("  texto  ", "TEXTO")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("AbCdEf", "ABCDEF")]
    public void NormalizeText_DeveTrimarEConverterParaMaiusculo(string? input, string? expected)
    {
        Assert.Equal(expected, InputFormattingHelper.NormalizeText(input));
    }

    [Theory]
    [InlineData("  Email@Test.COM  ", "email@test.com")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("User@DOMAIN.Com", "user@domain.com")]
    public void NormalizeEmail_DeveTrimarEConverterParaMinusculo(string? input, string? expected)
    {
        Assert.Equal(expected, InputFormattingHelper.NormalizeEmail(input));
    }

    [Theory]
    [InlineData("texto", "TEXTO", false)]
    [InlineData("Email@COM", "email@com", true)]
    [InlineData("Test@Email.COM", "test@email.com", true)]
    public void FormatField_DeveAplicarFormatacaoCorreta(string input, string expected, bool isEmail)
    {
        Assert.Equal(expected, InputFormattingHelper.FormatField(input, isEmail));
    }
}
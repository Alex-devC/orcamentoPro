using OrcPro.App.ViewModels;
using Xunit;

namespace OrcPro.Tests;

public class FormValidatorTests
{
    // ===== Required =====

    [Theory]
    [InlineData("texto", true)]
    [InlineData("  texto  ", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void Required_DeveValidarCampoObrigatorio(string? value, bool isValid)
    {
        var result = FormValidator.Required(value, "o campo");
        Assert.Equal(isValid, result.IsValid);
    }

    [Fact]
    public void Required_CampoVazio_DeveRetornarMensagemCorreta()
    {
        var result = FormValidator.Required(null, "o Nome / Razão Social do cliente");
        Assert.False(result.IsValid);
        Assert.Equal("Informe o Nome / Razão Social do cliente.", result.FirstError);
    }

    [Fact]
    public void Required_CampoPreenchido_DeveRetornarSucesso()
    {
        var result = FormValidator.Required("João", "o nome");
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    // ===== Email =====

    [Theory]
    [InlineData("test@email.com", true)]
    [InlineData("USER@DOMAIN.COM.BR", true)]
    [InlineData("", true)]   // Campo opcional
    [InlineData(null, true)] // Campo opcional
    [InlineData("invalid-email", false)]
    [InlineData("@email.com", false)]
    [InlineData("test@", false)]
    public void Email_DeveValidarFormatoEmail(string? value, bool isValid)
    {
        var result = FormValidator.Email(value, "cliente");
        Assert.Equal(isValid, result.IsValid);
    }

    [Fact]
    public void Email_FormatoInvalido_DeveRetornarMensagemCorreta()
    {
        var result = FormValidator.Email("invalid-email", "cliente");
        Assert.False(result.IsValid);
        Assert.Contains("inválido", result.FirstError);
    }

    // ===== CpfCnpj =====

    [Theory]
    [InlineData("111.444.777-35", true)]  // CPF válido
    [InlineData("11222333000181", true)]    // CNPJ válido
    [InlineData("", true)]                   // Campo opcional
    [InlineData(null, true)]                 // Campo opcional
    [InlineData("123", false)]               // CPF/CNPJ inválido
    [InlineData("999.999.999-99", false)]    // CPF inválido
    public void CpfCnpj_DeveValidarDocumento(string? value, bool isValid)
    {
        var result = FormValidator.CpfCnpj(value);
        Assert.Equal(isValid, result.IsValid);
    }

    // ===== CepObrigatorio =====

    [Theory]
    [InlineData("01310100", true)]
    [InlineData("01310-100", true)]
    [InlineData("01310", false)] // Completo para consulta exige 8 dígitos
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("   ", false)]
    public void CepObrigatorio_DeveValidarCepObrigatorio(string? value, bool isValid)
    {
        var result = FormValidator.CepObrigatorio(value);
        Assert.Equal(isValid, result.IsValid);
    }

    [Fact]
    public void CepObrigatorio_CepIncompleto_DeveRetornarMensagemDe8Digitos()
    {
        var result = FormValidator.CepObrigatorio("01310");
        Assert.False(result.IsValid);
        Assert.Contains("8 dígitos", result.FirstError);
    }

    // ===== DecimalNaoNegativo =====

    [Theory]
    [InlineData(0, true)]
    [InlineData(10, true)]
    [InlineData(-1, false)]
    public void DecimalNaoNegativo_DeveValidarValorNaoNegativo(decimal value, bool isValid)
    {
        var result = FormValidator.DecimalNaoNegativo(value, "preço de venda");
        Assert.Equal(isValid, result.IsValid);
    }

    [Fact]
    public void DecimalNaoNegativo_ValorNegativo_DeveRetornarMensagemCorreta()
    {
        var result = FormValidator.DecimalNaoNegativo(-5, "preço de venda");
        Assert.False(result.IsValid);
        Assert.Equal("O preço de venda não pode ser negativo.", result.FirstError);
    }

    [Fact]
    public void DecimalNaoNegativo_ValorDecimalNegativo_DeveRetornarInvalido()
    {
        var result = FormValidator.DecimalNaoNegativo(-0.01m, "preço de venda");
        Assert.False(result.IsValid);
    }

    // ===== FluentValidator =====

    [Fact]
    public void FluentValidator_MultiplasValidacoes_TodasValidas_DeveRetornarSucesso()
    {
        var result = FormValidator.Create()
            .Required("João", "o nome")
            .Email("joao@email.com", "cliente")
            .Build();

        Assert.True(result.IsValid);
    }

    [Fact]
    public void FluentValidator_MultiplasValidacoes_ComErros_DeveRetornarTodosOsErros()
    {
        var result = FormValidator.Create()
            .Required("", "o nome")
            .Required("", "o email")
            .Build();

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
        Assert.Equal("Informe o nome.", result.Errors[0]);
        Assert.Equal("Informe o email.", result.Errors[1]);
    }

    [Fact]
    public void FluentValidator_CustomCondition_DeveAdicionarErroQuandoVerdadeiro()
    {
        var result = FormValidator.Create()
            .Custom(true, "Condição customizada falhou")
            .Build();

        Assert.False(result.IsValid);
        Assert.Contains("Condição customizada falhou", result.Errors);
    }

    [Fact]
    public void FluentValidator_CustomCondition_NaoDeveAdicionarErroQuandoFalso()
    {
        var result = FormValidator.Create()
            .Custom(false, "Não deveria aparecer")
            .Build();

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    // ===== ValidationResult =====

    [Fact]
    public void ValidationResult_Success_DeveEstarValido()
    {
        var result = ValidationResult.Success();
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidationResult_Failure_DeveConterErro()
    {
        var result = ValidationResult.Failure("Campo obrigatório");
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("Campo obrigatório", result.FirstError);
    }

    [Fact]
    public void ValidationResult_FailureComMultiplosErros_DeveConterTodos()
    {
        var result = ValidationResult.Failure(new[] { "Erro 1", "Erro 2" });
        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void ValidationResult_FirstError_ListaVazia_DeveRetornarStringVazia()
    {
        var result = ValidationResult.Success();
        Assert.Equal(string.Empty, result.FirstError);
    }
}

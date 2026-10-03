using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Common.Formatters;
using Xunit;

namespace OrcPro.Tests;

public class InputBehaviorTests
{
    // ===== CEP Caching Tests =====

    [Fact]
    public void CepQueryCache_NovaConsulta_DeveRetornarTrue()
    {
        var cache = new CepQueryCache();
        Assert.True(cache.ShouldQuery("01310100"));
    }

    [Fact]
    public void CepQueryCache_CepRepetido_DeveRetornarFalse()
    {
        var cache = new CepQueryCache();
        cache.ShouldQuery("01310100");
        Assert.False(cache.ShouldQuery("01310100"));
    }

    [Fact]
    public void CepQueryCache_CepDiferente_DeveRetornarTrue()
    {
        var cache = new CepQueryCache();
        cache.ShouldQuery("01310100");
        Assert.True(cache.ShouldQuery("12345678"));
    }

    [Fact]
    public void CepQueryCache_AposReset_DevePermitirNovaConsulta()
    {
        var cache = new CepQueryCache();
        cache.ShouldQuery("01310100");
        cache.Reset();
        Assert.True(cache.ShouldQuery("01310100"));
    }

    [Fact]
    public async Task CepQueryCache_ConsultaDuplicada_ComServiceFake_NaoRepete()
    {
        var service = new ContadorCepService();
        var cache = new CepQueryCache();
        var cep = "01310100";

        // Primeira consulta: deve chamar o serviço
        if (cache.ShouldQuery(cep))
        {
            await service.ConsultarAsync(cep);
        }

        // Segunda tentativa com o mesmo CEP: não deve chamar o serviço
        if (cache.ShouldQuery(cep))
        {
            await service.ConsultarAsync(cep);
        }

        Assert.Equal(1, service.NumeroConsultas);
    }

    // ===== CEP Validation Tests =====

    [Theory]
    [InlineData("01310-000", true)]
    [InlineData("01310000", true)]
    [InlineData("01310", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("123", false)]
    public void CepMaskHelper_EstaCompletoParaConsulta_RetornaResultadoCorreto(string? cep, bool esperado)
    {
        Assert.Equal(esperado, CepMaskHelper.EstaCompletoParaConsulta(cep));
    }

    [Theory]
    [InlineData("01310-000", "01310000")]
    [InlineData("01310000", "01310000")]
    [InlineData("01310-100", "01310100")]
    [InlineData("abc", "")]
    [InlineData(null, "")]
    public void CepMaskHelper_Normalizar_RemoverMascara(string? input, string expected)
    {
        Assert.Equal(expected, CepMaskHelper.Normalizar(input));
    }

    // ===== Enter Navigation Tests =====
    // Estes testes validam a lógica de decisão de quais controles devem ou não
    // receber o comportamento de navegação com ENTER, usando strings com os nomes
    // dos tipos padrão do WPF (TextBox, Button, etc.) para evitar dependência
    // direta do assembly WindowsBase nos testes de domínio.

    [Theory]
    [InlineData("TextBox", true)]
    [InlineData("ComboBox", true)]
    [InlineData("DatePicker", true)]
    [InlineData("PasswordBox", true)]
    [InlineData("Button", false)]
    [InlineData("DataGrid", false)]
    [InlineData("ListBox", false)]
    public void EnterNavigation_SoDeveAplicarEmControlesEditaveis(string typeName, bool esperado)
    {
        Assert.Equal(esperado, ShouldHandleEnterNavigation(typeName));
    }

    /// <summary>
    /// Simula a lógica usada em TextFormattingBehavior.OnPreviewKeyDownMoveFocus
    /// para validar quais controles recebem navegação ENTER.
    /// Não aplicar a controles onde ENTER tem função própria (Button, DataGrid, ListBox).
    /// </summary>
    private static bool ShouldHandleEnterNavigation(string typeName)
    {
        // Controles onde ENTER tem função própria
        if (typeName == "Button" || typeName == "DataGrid" || typeName == "ListBox")
            return false;

        // Controles editáveis que devem navegar com ENTER
        return typeName == "TextBox"
            || typeName == "ComboBox"
            || typeName == "DatePicker"
            || typeName == "PasswordBox";
    }

    // ===== Fake Cep Service with counter =====

    /// <summary>
    /// Wrapper que conta quantas vezes ConsultarAsync foi chamado.
    /// </summary>
    private class ContadorCepService : ICepService
    {
        public int NumeroConsultas { get; private set; }
        public string ProviderName => "Contador";

        public Task<CepAddressResult> ConsultarAsync(string cep, CancellationToken cancellationToken = default)
        {
            NumeroConsultas++;
            var normalized = CepMaskHelper.Normalizar(cep);
            return Task.FromResult(CepAddressResult.SuccessResult(normalized, "Rua Teste", "Bairro Teste", "Cidade Teste", "SP"));
        }
    }
}


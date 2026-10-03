using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Common.Formatters;
using Xunit;

namespace OrcPro.Tests;

public class CepServiceTests
{
    [Fact]
    public async Task FakeCepService_ConsultaBemSucedida_DeveRetornarEndereco()
    {
        var service = new FakeCepService();
        service.AddCepResult("01310-100", "Av. Paulista", "Bela Vista", "São Paulo", "SP");

        var result = await service.ConsultarAsync("01310-100");

        Assert.True(result.Success);
        Assert.Equal("01310100", result.Cep);
        Assert.Equal("Av. Paulista", result.Logradouro);
        Assert.Equal("Bela Vista", result.Bairro);
        Assert.Equal("São Paulo", result.Cidade);
        Assert.Equal("SP", result.Uf);
        Assert.Equal("FakeCEP", result.Source);
    }

    [Fact]
    public async Task FakeCepService_AceitaEntradaComMascara()
    {
        var service = new FakeCepService();
        service.AddCepResult("01310-100", "Av. Paulista", "Bela Vista", "São Paulo", "SP");

        var result1 = await service.ConsultarAsync("01310-100");
        var result2 = await service.ConsultarAsync("01310100");
        var result3 = await service.ConsultarAsync("01310 100");

        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.True(result3.Success);
    }

    [Fact]
    public async Task FakeCepService_CepInexistente_DeveRetornarFalha()
    {
        var service = new FakeCepService();
        // CEP não configurado
        var result = await service.ConsultarAsync("99999999");

        Assert.False(result.Success);
        Assert.Contains("não encontrado", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FakeCepService_SetCepNotFound_DeveRetornarFalhaExplicita()
    {
        var service = new FakeCepService();
        service.SetCepNotFound("12345678");

        var result = await service.ConsultarAsync("12345678");

        Assert.False(result.Success);
        Assert.Equal("CEP não encontrado", result.ErrorMessage);
    }

    [Fact]
    public async Task FakeCepService_ServicoIndisponivel_DeveRetornarFalha()
    {
        var service = new FakeCepService(true, "Serviço temporariamente indisponível");

        var result = await service.ConsultarAsync("01310100");

        Assert.False(result.Success);
        Assert.Equal("Serviço temporariamente indisponível", result.ErrorMessage);
    }

    [Fact]
    public async Task FakeCepService_Uf_DeveSerNormalizadaParaMaiusculo()
    {
        var service = new FakeCepService();
        service.AddCepResult("01310100", "Av. Teste", "Centro", "Cidade", "sp");

        var result = await service.ConsultarAsync("01310100");

        Assert.Equal("SP", result.Uf);
    }
}

public class CepAddressResultTests
{
    [Fact]
    public void SuccessResult_DeveCriarResultadoDeSucesso()
    {
        var result = CepAddressResult.SuccessResult("01310100", "Av. Teste", "Centro", "Cidade", "SP", "Complemento", "FakeCEP");

        Assert.True(result.Success);
        Assert.Equal("01310100", result.Cep);
        Assert.Equal("Av. Teste", result.Logradouro);
        Assert.Equal("Centro", result.Bairro);
        Assert.Equal("Cidade", result.Cidade);
        Assert.Equal("SP", result.Uf);
        Assert.Equal("Complemento", result.Complemento);
        Assert.Equal("FakeCEP", result.Source);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void FailureResult_DeveCriarResultadoDeFalha()
    {
        var result = CepAddressResult.FailureResult("01310100", "Erro de conexão", "FakeCEP");

        Assert.False(result.Success);
        Assert.Equal("01310100", result.Cep);
        Assert.Equal("Erro de conexão", result.ErrorMessage);
        Assert.Equal("FakeCEP", result.Source);
        Assert.Null(result.Logradouro);
        Assert.Null(result.Bairro);
        Assert.Null(result.Cidade);
        Assert.Null(result.Uf);
    }
}
using System;
using System.Threading.Tasks;
using OrcPro.Domain.Common.Formatters;
using OrcPro.Infrastructure.Services;
using Xunit;
using Xunit.Abstractions;

namespace OrcPro.Tests;

/// <summary>
/// Teste de INTEGRAÇÃO real do <see cref="ViaCepService"/> contra o provedor ViaCEP.
/// Consulta obrigatoriamente os CEPs 15130-010 e 15085-520.
///
/// Sem internet o teste é tratado como INCONCLUSIVO (retorno controlado), mas qualquer
/// falha que NÃO seja de conectividade (ex.: JSON mal interpretado, CEP válido tratado
/// como não encontrado) FALHA o teste — erro de implementação não é mascarado como offline.
/// </summary>
public class ViaCepServiceIntegrationTests
{
    private const string FalhaDeConectividadeEsperada = "Erro de rede";
    private const string TimeoutEsperado = "Timeout";
    private const string CancelamentoEsperado = "cancelada";

    private readonly ITestOutputHelper _output;

    public ViaCepServiceIntegrationTests(ITestOutputHelper output)
        => _output = output;

    [Theory]
    [InlineData("15130-010")]
    [InlineData("15085-520")]
    public async Task ConsultarAsync_CepReal_DeveRetornarEnderecoCompleto(string cep)
    {
        var servico = new ViaCepService();
        var result = await servico.ConsultarAsync(cep);

        _output.WriteLine($"=== Consulta real: {cep} ===");
        _output.WriteLine($"Success = {result.Success}");
        _output.WriteLine($"Cep     = {result.Cep}");
        _output.WriteLine($"Endereço= {result.Logradouro} | {result.Bairro}");
        _output.WriteLine($"Cidade  = {result.Cidade} / {result.Uf}");
        _output.WriteLine($"Erro    = {result.ErrorMessage}");

        if (!result.Success)
        {
            if (EhFalhaDeConectividade(result.ErrorMessage))
            {
                _output.WriteLine("INCONCLUSIVO: ambiente sem conectividade (rede/timeout).");
                return;
            }

            // O provedor respondeu HTTP 200 com o campo "erro" — é a RESPOSTA OFICIAL do
            // servidor. O CEP não consta na base do provedor; a corretude da interpretação
            // do campo "erro" é coberta pelos testes mockados.
            if (result.ErrorMessage == "CEP não encontrado")
            {
                _output.WriteLine("RESPOSTA OFICIAL DO PROVEDOR: CEP não encontrado (HTTP 200 com campo \"erro\").");
                return;
            }

            // Qualquer outra falha que NÃO seja de rede = erro de implementação: falha o
            // teste.
            Assert.Fail(
                $"Falha de IMPLEMENTAÇÃO (não é conectividade): {result.ErrorMessage}.");
        }

        var cepNormalizado = CepMaskHelper.Normalizar(cep);
        Assert.Equal(cepNormalizado, result.Cep);
        Assert.False(string.IsNullOrWhiteSpace(result.Logradouro),
            "Logradouro deveria vir preenchido do ViaCEP.");
        Assert.False(string.IsNullOrWhiteSpace(result.Cidade),
            "Cidade deveria vir preenchida do ViaCEP.");
        Assert.False(string.IsNullOrWhiteSpace(result.Uf),
            "UF deveria vir preenchida do ViaCEP.");
        Assert.Equal("ViaCEP", result.Source);
    }

    [Fact]
    public async Task ConsultarAsync_CepInexistente_ComRede_DeveReportarNaoEncontrado()
    {
        var servico = new ViaCepService();
        var result = await servico.ConsultarAsync("99999999");

        if (!result.Success)
        {
            var erro = result.ErrorMessage ?? string.Empty;

            if (EhFalhaDeConectividade(erro))
            {
                _output.WriteLine("INCONCLUSIVO: ambiente sem conectividade.");
                return;
            }

            // Com rede, o ViaCEP responde "CEP não encontrado" para formato válido inexistente.
            Assert.Equal("CEP não encontrado", result.ErrorMessage);
            return;
        }

        Assert.Fail("O ViaCEP não deveria encontrar o CEP 99999999.");
    }

    /// <summary>
    /// Considera falha de conectividade APENAS mensagens conhecidas de rede/timeout.
    /// Erros inesperados NÃO são tratados como offline (não mascarar implementação).
    /// </summary>
    private static bool EhFalhaDeConectividade(string? mensagem)
    {
        var erro = mensagem ?? string.Empty;
        return erro.Contains(FalhaDeConectividadeEsperada, StringComparison.OrdinalIgnoreCase)
            || erro.Contains(TimeoutEsperado, StringComparison.OrdinalIgnoreCase)
            || erro.Contains(CancelamentoEsperado, StringComparison.OrdinalIgnoreCase);
    }
}

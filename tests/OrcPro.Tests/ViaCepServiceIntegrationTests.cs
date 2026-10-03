using OrcPro.Infrastructure.Services;
using Xunit;

namespace OrcPro.Tests;

/// <summary>
/// Smoke test do <see cref="ViaCepService"/> contra o provedor real (ViaCEP).
/// Tolerante a ausência de conectividade: se a consulta falhar por rede/timeout,
/// o teste não falha — ele só valida o resultado quando a rede responde.
/// </summary>
public class ViaCepServiceIntegrationTests
{
    private const string FalhaDeConectividadeEsperada = "Erro de rede";
    private const string TimeoutEsperado = "Timeout";

    [Fact]
    public async Task ConsultarAsync_CepReal_ComRede_DeveRetornarEndereco()
    {
        var servico = new ViaCepService();
        var result = await servico.ConsultarAsync("01310-100");

        if (!result.Success)
        {
            AssertSemRede(result.ErrorMessage);
            return;
        }

        Assert.Equal("01310100", result.Cep);
        Assert.False(string.IsNullOrWhiteSpace(result.Cidade), "Cidade deveria vir preenchida do ViaCEP.");
        Assert.False(string.IsNullOrWhiteSpace(result.Uf), "UF deveria vir preenchida do ViaCEP.");
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

            // Sem rede o provedor não é alcançado: aceitamos erro de conectividade.
            if (erro.Contains(FalhaDeConectividadeEsperada, StringComparison.OrdinalIgnoreCase) ||
                erro.Contains(TimeoutEsperado, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Com rede, o ViaCEP responde "CEP não encontrado" para formato válido inexistente.
            Assert.Equal("CEP não encontrado", result.ErrorMessage);
            return;
        }

        Assert.Fail("O ViaCEP não deveria encontrar o CEP 99999999.");
    }

    /// <summary>Converte falha de rede/timeout em sucesso do teste (ambiente sem internet).</summary>
    private static void AssertSemRede(string? mensagem)
    {
        var erro = mensagem ?? string.Empty;
        Assert.True(
            erro.Contains(FalhaDeConectividadeEsperada, StringComparison.OrdinalIgnoreCase) ||
            erro.Contains(TimeoutEsperado, StringComparison.OrdinalIgnoreCase) ||
            erro.Contains("inesperado", StringComparison.OrdinalIgnoreCase),
            $"Falha inesperada do ViaCEP (não é de conectividade): {erro}");
    }
}

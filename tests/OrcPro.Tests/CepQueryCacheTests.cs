using OrcPro.Domain.Common.Formatters;
using Xunit;

namespace OrcPro.Tests;

/// <summary>
/// Testes do cache de consulta de CEP (<see cref="CepQueryCache"/>): evita consultas
/// duplicadas, mas nunca deve bloquear nova tentativa após falha ou limpeza do campo.
/// </summary>
public class CepQueryCacheTests
{
    [Fact]
    public void ShouldQuery_PrimeiraVez_DevePermitirConsulta()
    {
        var cache = new CepQueryCache();

        Assert.True(cache.ShouldQuery("01310100"));
    }

    [Fact]
    public void ShouldQuery_CepRepetido_NaoDeveConsultarDeNovo()
    {
        // Contrato: a entrada já vem normalizada (só dígitos) — quem chama normaliza antes.
        var cache = new CepQueryCache();
        cache.ShouldQuery("01310100");

        Assert.False(cache.ShouldQuery("01310100"));
    }

    [Fact]
    public void ShouldQuery_AlternandoCeps_DevePermitirCadaConsulta()
    {
        var cache = new CepQueryCache();

        Assert.True(cache.ShouldQuery("01310100"));
        Assert.True(cache.ShouldQuery("20040000"));
        Assert.True(cache.ShouldQuery("01310100"), "Voltar para um CEP anterior deve permitir nova consulta.");
    }

    [Fact]
    public void Reset_DevePermitirNovaConsultaDoMesmoCep()
    {
        var cache = new CepQueryCache();
        cache.ShouldQuery("01310100");

        cache.Reset();

        Assert.True(cache.ShouldQuery("01310100"));
    }

    [Fact]
    public void Reset_AposFalha_DevePermitirNovaTentativa()
    {
        var cache = new CepQueryCache();

        // Primeira tentativa consulta e falha (provedor fora do ar).
        Assert.True(cache.ShouldQuery("01310100"));

        // O ViewModel reseta o cache ao falhar para que o usuário possa tentar de novo.
        cache.Reset();

        Assert.True(cache.ShouldQuery("01310100"), "Falha anterior não pode bloquear a nova tentativa.");
    }

    [Fact]
    public void Registrar_DeveMarcarCepComoJaConsultado()
    {
        var cache = new CepQueryCache();

        // Usado após consulta manual bem-sucedida (lupa) para não repetir a consulta automática.
        cache.Registrar("01310100");

        Assert.False(cache.ShouldQuery("01310100"));
    }

    [Fact]
    public void Registrar_OutroCep_DevePermitirConsultaDoProximo()
    {
        var cache = new CepQueryCache();
        cache.Registrar("01310100");

        Assert.True(cache.ShouldQuery("20040000"));
    }
}

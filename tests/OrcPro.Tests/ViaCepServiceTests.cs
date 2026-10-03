using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OrcPro.Infrastructure.Services;
using Xunit;

namespace OrcPro.Tests;

/// <summary>
/// Testes isolados do <see cref="ViaCepService"/> usando um <see cref="HttpMessageHandler"/>
/// simulado: nenhum teste desta classe depende de rede. O smoke test com a rede real
/// fica em <see cref="ViaCepServiceIntegrationTests"/>.
/// </summary>
public class ViaCepServiceTests
{
    private const string RespostaValida =
        """
        {
          "cep": "01310-100",
          "logradouro": "Avenida Paulista",
          "complemento": "até 1578 - lado par",
          "bairro": "Bela Vista",
          "localidade": "São Paulo",
          "uf": "SP",
          "ibge": "3550308",
          "gia": "1004",
          "ddd": "11",
          "siafi": "7107"
        }
        """;

    #region Sucesso

    [Fact]
    public async Task ConsultarAsync_RespostaValida_DeveMapearEndereco()
    {
        var handler = new StubHandler(_ => Task.FromResult(CriarResposta(RespostaValida)));
        var servico = new ViaCepService(new HttpClient(handler));

        var result = await servico.ConsultarAsync("01310-100");

        Assert.True(result.Success);
        Assert.Equal("01310100", result.Cep);
        Assert.Equal("Avenida Paulista", result.Logradouro);
        Assert.Equal("Bela Vista", result.Bairro);
        Assert.Equal("São Paulo", result.Cidade);
        Assert.Equal("SP", result.Uf);
        Assert.Equal("ViaCEP", result.Source);
        Assert.Equal("ViaCEP", servico.ProviderName);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ConsultarAsync_CepComMascara_DeveNormalizarAntesDeConsultar()
    {
        var handler = new StubHandler(_ => Task.FromResult(CriarResposta(RespostaValida)));
        var servico = new ViaCepService(new HttpClient(handler));

        await servico.ConsultarAsync("01310-100");

        var uri = Assert.Single(handler.Uris);
        Assert.Equal("https://viacep.com.br/ws/01310100/json/", uri.ToString());
    }

    [Fact]
    public async Task ConsultarAsync_SemBaseAddress_DeveUsarUrlAbsolutaDoViaCep()
    {
        var handler = new StubHandler(_ => Task.FromResult(CriarResposta(RespostaValida)));
        var servico = new ViaCepService(new HttpClient(handler));

        await servico.ConsultarAsync("01310100");

        var uri = Assert.Single(handler.Uris);
        Assert.True(uri.IsAbsoluteUri, "A URL da requisição deveria ser absoluta.");
        Assert.Equal("viacep.com.br", uri.Host);
        Assert.Equal("/ws/01310100/json/", uri.AbsolutePath);
    }

    [Fact]
    public async Task ConsultarAsync_ComBaseAddress_DeveUsarCaminhoRelativo()
    {
        // Em produção o HttpClient chega com BaseAddress configurado (injeção de dependência).
        var handler = new StubHandler(_ => Task.FromResult(CriarResposta(RespostaValida)));
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://viacep.com.br/ws/") };
        var servico = new ViaCepService(client);

        await servico.ConsultarAsync("01310100");

        var uri = Assert.Single(handler.Uris);
        Assert.Equal("https://viacep.com.br/ws/01310100/json/", uri.ToString());
    }

    #endregion

    #region Falhas

    /// <summary>
    /// O ViaCEP já devolveu este campo como booleano (<c>true</c>) e como texto (<c>"true"</c>).
    /// Os dois formatos devem resultar em "CEP não encontrado" — nunca em erro de JSON.
    /// </summary>
    [Theory]
    [InlineData("""{"erro":true}""")]
    [InlineData("""{"erro":"true"}""")]
    public async Task ConsultarAsync_CepInexistente_DeveRetornarNaoEncontrado(string corpo)
    {
        var handler = new StubHandler(_ => Task.FromResult(CriarResposta(corpo)));
        var servico = new ViaCepService(new HttpClient(handler));

        var result = await servico.ConsultarAsync("99999999");

        Assert.False(result.Success);
        Assert.Equal("CEP não encontrado", result.ErrorMessage);
        Assert.Equal("99999999", result.Cep);
        Assert.Equal("ViaCEP", result.Source);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("abc")]
    public async Task ConsultarAsync_CepInvalido_DeveFalharSemChamarHttp(string cep)
    {
        var handler = new StubHandler(_ => Task.FromResult(CriarResposta(RespostaValida)));
        var servico = new ViaCepService(new HttpClient(handler));

        var result = await servico.ConsultarAsync(cep);

        Assert.False(result.Success);
        Assert.Contains("8 dígitos", result.ErrorMessage);
        Assert.Empty(handler.Uris);
    }

    [Fact]
    public async Task ConsultarAsync_ErroHttp_DeveInformarStatus()
    {
        var handler = new StubHandler(_ => Task.FromResult(CriarResposta("erro", HttpStatusCode.InternalServerError)));
        var servico = new ViaCepService(new HttpClient(handler));

        var result = await servico.ConsultarAsync("01310100");

        Assert.False(result.Success);
        Assert.Contains("Erro na consulta: HTTP InternalServerError", result.ErrorMessage);
    }

    [Fact]
    public async Task ConsultarAsync_FalhaDeRede_DeveRetornarErroDeRede()
    {
        var handler = new StubHandler(_ =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("falha de DNS")));
        var servico = new ViaCepService(new HttpClient(handler));

        var result = await servico.ConsultarAsync("01310100");

        Assert.False(result.Success);
        Assert.StartsWith("Erro de rede:", result.ErrorMessage);
        Assert.Contains("falha de DNS", result.ErrorMessage);
    }

    [Fact]
    public async Task ConsultarAsync_Cancelado_DeveRetornarConsultaCancelada()
    {
        // O handler nunca responde; o token cancelado interrompe a espera e o
        // ViaCepService deve distinguir cancelamento de erro de rede/timeout.
        var handler = new StubHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return CriarResposta(RespostaValida);
        });
        var servico = new ViaCepService(new HttpClient(handler));

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var result = await servico.ConsultarAsync("01310100", cts.Token);

        Assert.False(result.Success);
        Assert.Equal("Consulta cancelada", result.ErrorMessage);
    }

    [Fact]
    public async Task ConsultarAsync_JsonInvalido_DeveRetornarErroDeProcessamento()
    {
        var handler = new StubHandler(_ => Task.FromResult(CriarResposta("<html>não é json</html>")));
        var servico = new ViaCepService(new HttpClient(handler));

        var result = await servico.ConsultarAsync("01310100");

        Assert.False(result.Success);
        Assert.StartsWith("Erro ao processar resposta:", result.ErrorMessage);
    }

    #endregion

    #region Stub / helpers

    private static HttpResponseMessage CriarResposta(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    /// <summary>Handler HTTP que registra as URIs chamadas e devolve uma resposta simulada.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
            => _responder = responder;

        /// <summary>Atalho para respostas que não dependem do token de cancelamento.</summary>
        public StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
            => _responder = (request, _) => responder(request);

        public List<Uri> Uris { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is not null)
                Uris.Add(request.RequestUri);

            return _responder(request, cancellationToken);
        }
    }

    #endregion
}

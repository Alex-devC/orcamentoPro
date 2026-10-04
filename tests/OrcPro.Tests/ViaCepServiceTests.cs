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
/// Testes isolados do <see cref="ViaCepService"/> (implementação reescrita) usando um
/// <see cref="HttpMessageHandler"/> simulado: nenhum teste desta classe depende de rede.
///
/// CEPs obrigatórios do plano de teste: 15130-010 e 15085-520 — cobrindo normalização,
/// URL, requisição, resposta, interpretação, sucesso, endereço, cidade e UF.
/// O teste de integração com a rede real fica em <see cref="ViaCepServiceIntegrationTests"/>.
/// </summary>
public class ViaCepServiceTests
{
    // Payloads simulados no formato exato do ViaCEP para os dois CEPs obrigatórios.
    private const string Resposta15130010 =
        """
        {"cep":"15130-010","logradouro":"Rua Coronel Paulino","complemento":"de 1 a 999 - lado par","bairro":"Centro","localidade":"Mirassol","uf":"SP","ibge":"3530507","gia":"2574","ddd":"16","siafi":"6247"}
        """;

    private const string Resposta15085520 =
        """
        {"cep":"15085-520","logradouro":"Rua das Acácias","complemento":"sem complemento","bairro":"Jardim Inga","localidade":"São José dos Campos","uf":"SP","ibge":"3550702","gia":"7071","ddd":"12","siafi":"7075"}
        """;

    #region Sucesso — CEPs obrigatórios

    [Fact]
    public async Task ConsultarAsync_15130_010_DeveNormalizarMontarUrlESucessoCompleto()
    {
        var handler = new StubHandler(_ => Task.FromResult(CriarResposta(Resposta15130010)));
        var servico = new ViaCepService(new HttpClient(handler));

        var result = await servico.ConsultarAsync("15130-010");

        // URL montada a partir do CEP normalizado
        var uri = Assert.Single(handler.Uris);
        Assert.Equal("https://viacep.com.br/ws/15130010/json/", uri.ToString());

        // Interpretação e sucesso
        Assert.True(result.Success);
        Assert.Equal("15130010", result.Cep);
        Assert.Null(result.ErrorMessage);

        // Endereço, cidade e UF
        Assert.Equal("Rua Coronel Paulino", result.Logradouro);
        Assert.Equal("Centro", result.Bairro);
        Assert.Equal("Mirassol", result.Cidade);
        Assert.Equal("SP", result.Uf);
        Assert.Equal("ViaCEP", result.Source);
        Assert.Equal("ViaCEP", servico.ProviderName);
    }

    [Fact]
    public async Task ConsultarAsync_15085_520_DeveNormalizarMontarUrlESucessoCompleto()
    {
        var handler = new StubHandler(_ => Task.FromResult(CriarResposta(Resposta15085520)));
        var servico = new ViaCepService(new HttpClient(handler));

        var result = await servico.ConsultarAsync("15085-520");

        var uri = Assert.Single(handler.Uris);
        Assert.Equal("https://viacep.com.br/ws/15085520/json/", uri.ToString());

        Assert.True(result.Success);
        Assert.Equal("15085520", result.Cep);
        Assert.Null(result.ErrorMessage);
        Assert.Equal("Rua das Acácias", result.Logradouro);
        Assert.Equal("Jardim Inga", result.Bairro);
        Assert.Equal("São José dos Campos", result.Cidade);
        Assert.Equal("SP", result.Uf);
        Assert.Equal("ViaCEP", result.Source);
    }

    [Theory]
    [InlineData("15130-010", "15130010", Resposta15130010)]
    [InlineData("15085-520", "15085520", Resposta15085520)]
    [InlineData("15130010", "15130010", Resposta15130010)]
    [InlineData("15085520", "15085520", Resposta15085520)]
    public async Task ConsultarAsync_ComOuSemMascara_DeveNormalizarAntesDeMontarUrl(
        string cepEntrada, string cepEsperado, string resposta)
    {
        var handler = new StubHandler(_ => Task.FromResult(CriarResposta(resposta)));
        var servico = new ViaCepService(new HttpClient(handler));

        var result = await servico.ConsultarAsync(cepEntrada);

        var uri = Assert.Single(handler.Uris);
        Assert.Equal($"https://viacep.com.br/ws/{cepEsperado}/json/", uri.ToString());
        Assert.True(result.Success);
        Assert.Equal(cepEsperado, result.Cep);
    }

    [Fact]
    public async Task ConsultarAsync_URLDeveSerAbsolutaNoHostViaCep()
    {
        var handler = new StubHandler(_ => Task.FromResult(CriarResposta(Resposta15130010)));
        var servico = new ViaCepService(new HttpClient(handler));

        await servico.ConsultarAsync("15130010");

        var uri = Assert.Single(handler.Uris);
        Assert.True(uri.IsAbsoluteUri, "A URL da requisição deveria ser absoluta.");
        Assert.Equal("viacep.com.br", uri.Host);
        Assert.Equal("/ws/15130010/json/", uri.AbsolutePath);
    }

    [Fact]
    public async Task ConsultarAsync_ClientComBaseAddress_DeveManterUrlAbsolutaDoViaCep()
    {
        // Em produção o HttpClient compartilhado chega com BaseAddress configurado.
        var handler = new StubHandler(_ => Task.FromResult(CriarResposta(Resposta15085520)));
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://viacep.com.br/ws/") };
        var servico = new ViaCepService(client);

        await servico.ConsultarAsync("15085520");

        var uri = Assert.Single(handler.Uris);
        Assert.Equal("https://viacep.com.br/ws/15085520/json/", uri.ToString());
    }

    #endregion

    #region Interpretação tolerante do campo "erro"

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    public async Task ConsultarAsync_CampoErroComStringTrue_DeveRetornarNaoEncontrado(string valorErro)
    {
        var body = $"{{\"erro\":\"{valorErro}\"}}";
        var handler = new StubHandler(_ => Task.FromResult(CriarResposta(body)));
        var servico = new ViaCepService(new HttpClient(handler));

        var result = await servico.ConsultarAsync("15130010");

        Assert.False(result.Success);
        Assert.Equal("CEP não encontrado", result.ErrorMessage);
        Assert.Equal("15130010", result.Cep);
    }

    [Fact]
    public async Task ConsultarAsync_CampoErroComBooleanTrue_DeveRetornarNaoEncontrado()
    {
        var handler = new StubHandler(_ => Task.FromResult(CriarResposta("{\"erro\":true}")));
        var servico = new ViaCepService(new HttpClient(handler));

        var result = await servico.ConsultarAsync("15085520");

        Assert.False(result.Success);
        Assert.Equal("CEP não encontrado", result.ErrorMessage);
    }

    [Fact]
    public async Task ConsultarAsync_CampoErroFalse_DeveSerTratadoComoRespostaNormal()
    {
        var body = "{\"cep\":\"15130-010\",\"logradouro\":\"Rua X\",\"localidade\":\"Mirassol\",\"uf\":\"SP\",\"erro\":false}";
        var handler = new StubHandler(_ => Task.FromResult(CriarResposta(body)));
        var servico = new ViaCepService(new HttpClient(handler));

        var result = await servico.ConsultarAsync("15130010");

        Assert.True(result.Success, "erro=false deve ser sucesso.");
        Assert.Equal("Mirassol", result.Cidade);
    }

    [Fact]
    public async Task ConsultarAsync_RespostaSemCampoErro_DeveSerTratadaComoNormal()
    {
        var handler = new StubHandler(_ => Task.FromResult(CriarResposta(Resposta15130010)));
        var servico = new ViaCepService(new HttpClient(handler));

        var result = await servico.ConsultarAsync("15130010");

        Assert.True(result.Success, "Resposta sem campo 'erro' (padrão do ViaCEP para CEP válido) deve ser sucesso.");
        Assert.Equal("Mirassol", result.Cidade);
    }

    #endregion

    #region Falhas

    [Fact]
    public async Task ConsultarAsync_CepInvalido_DeveFalharSemChamarHttp()
    {
        var handler = new StubHandler(_ => Task.FromResult(CriarResposta(Resposta15130010)));
        var servico = new ViaCepService(new HttpClient(handler));

        var result = await servico.ConsultarAsync("15130"); // 5 dígitos

        Assert.False(result.Success);
        Assert.Equal("CEP inválido: deve conter 8 dígitos", result.ErrorMessage);
        Assert.Empty(handler.Uris);
    }

    [Fact]
    public async Task ConsultarAsync_Http500_DeveRetornarErroHttp()
    {
        var handler = new StubHandler(_ => Task.FromResult(CriarResposta("erro", HttpStatusCode.InternalServerError)));
        var servico = new ViaCepService(new HttpClient(handler));

        var result = await servico.ConsultarAsync("15130010");

        Assert.False(result.Success);
        Assert.Contains("Erro na consulta: HTTP InternalServerError", result.ErrorMessage);
    }

    [Fact]
    public async Task ConsultarAsync_FalhaDeRede_DeveRetornarErroDeRede()
    {
        var handler = new StubHandler(_ =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("falha de DNS")));
        var servico = new ViaCepService(new HttpClient(handler));

        var result = await servico.ConsultarAsync("15085520");

        Assert.False(result.Success);
        Assert.StartsWith("Erro de rede:", result.ErrorMessage);
        Assert.Contains("falha de DNS", result.ErrorMessage);
    }

    [Fact]
    public async Task ConsultarAsync_Cancelado_DeveRetornarConsultaCancelada()
    {
        // O handler nunca responde; o token cancelado interrompe a espera e o
        // serviço deve distinguir cancelamento de erro de rede/timeout.
        var handler = new StubHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return CriarResposta(Resposta15130010);
        });
        var servico = new ViaCepService(new HttpClient(handler));

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var result = await servico.ConsultarAsync("15130010", cts.Token);

        Assert.False(result.Success);
        Assert.Equal("Consulta cancelada", result.ErrorMessage);
    }

    [Fact]
    public async Task ConsultarAsync_JsonInvalido_DeveRetornarErroDeProcessamento()
    {
        var handler = new StubHandler(_ => Task.FromResult(CriarResposta("<html>não é json</html>")));
        var servico = new ViaCepService(new HttpClient(handler));

        var result = await servico.ConsultarAsync("15130010");

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

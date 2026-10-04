using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Common.Formatters;

namespace OrcPro.Infrastructure.Services;

/// <summary>
/// Consulta de CEP via API pública ViaCEP (https://viacep.com.br).
///
/// Fluxo linear:
///   CEP recebido → normalização → validação dos 8 dígitos → montagem da URL →
///   requisição HTTP → status HTTP → texto bruto da resposta → desserialização
///   (JsonDocument) → interpretação do campo "erro" (tolerante a true / "true" /
///   ausente) → retorno estruturado (CepAddressResult).
///
/// A interpretação NÃO depende de DTO rígido: JsonDocument lê cada campo
/// explicitamente.
/// </summary>
public sealed class ViaCepService : ICepService
{
    private const string Provedor = "ViaCEP";
    private const string BaseAddressUrl = "https://viacep.com.br/ws/";

    /// <summary>HttpClient compartilhado (singleton estático).</summary>
    private static readonly HttpClient HttpClientCompartilhado = new()
    {
        BaseAddress = new Uri(BaseAddressUrl),
        Timeout = TimeSpan.FromSeconds(10)
    };

    private readonly HttpClient _httpClient;

    public ViaCepService()
        : this(HttpClientCompartilhado)
    {
    }

    public ViaCepService(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public string ProviderName => Provedor;

    public async Task<CepAddressResult> ConsultarAsync(string cep, CancellationToken cancellationToken = default)
    {
        var normalizado = CepMaskHelper.Normalizar(cep);
        var valido = CepMaskHelper.EhValido(normalizado);

        if (!valido)
            return CepAddressResult.FailureResult(normalizado, "CEP inválido: deve conter 8 dígitos", Provedor);

        var url = $"{BaseAddressUrl}{normalizado}/json/";

        HttpResponseMessage resposta;
        try
        {
            resposta = await _httpClient.GetAsync(new Uri(url, UriKind.Absolute), cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return CepAddressResult.FailureResult(normalizado, $"Erro de rede: {ex.Message}", Provedor);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CepAddressResult.FailureResult(normalizado, "Consulta cancelada", Provedor);
        }
        catch (OperationCanceledException ex)
        {
            return CepAddressResult.FailureResult(normalizado, $"Timeout: {ex.Message}", Provedor);
        }

        string textoBruto;
        try
        {
            textoBruto = await resposta.Content.ReadAsStringAsync(cancellationToken) ?? string.Empty;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CepAddressResult.FailureResult(normalizado, "Consulta cancelada", Provedor);
        }
        catch (OperationCanceledException ex)
        {
            return CepAddressResult.FailureResult(normalizado, $"Timeout: {ex.Message}", Provedor);
        }
        catch (HttpRequestException ex)
        {
            return CepAddressResult.FailureResult(normalizado, $"Erro de rede: {ex.Message}", Provedor);
        }

        if (!resposta.IsSuccessStatusCode)
            return CepAddressResult.FailureResult(normalizado, $"Erro na consulta: HTTP {resposta.StatusCode}", Provedor);

        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(textoBruto);
        }
        catch (JsonException ex)
        {
            return CepAddressResult.FailureResult(normalizado, $"Erro ao processar resposta: {ex.Message}", Provedor);
        }

        using (documento)
        {
            if (documento.RootElement.ValueKind != JsonValueKind.Object)
                return CepAddressResult.FailureResult(
                    normalizado,
                    $"Erro ao processar resposta: JSON raiz é {documento.RootElement.ValueKind}, esperado Object",
                    Provedor);

            var raiz = documento.RootElement;

            if (CampoErroIndicaFalha(raiz))
                return CepAddressResult.FailureResult(normalizado, "CEP não encontrado", Provedor);

            var logradouro = LerTexto(raiz, "logradouro");
            var bairro = LerTexto(raiz, "bairro");
            var cidade = LerTexto(raiz, "localidade");
            var uf = LerTexto(raiz, "uf");
            var complemento = LerTexto(raiz, "complemento");

            return CepAddressResult.SuccessResult(
                cep: normalizado,
                logradouro: logradouro,
                bairro: bairro,
                cidade: cidade,
                uf: uf,
                complemento: complemento,
                source: Provedor);
        }
    }

    /// <summary>
    /// Interpreta o campo "erro" do ViaCEP de forma tolerante via JsonDocument:
    /// aceita true (bool), "true" (string), false, null, ausente e formatos
    /// desconhecidos sem quebrar a consulta.
    /// </summary>
    private static bool CampoErroIndicaFalha(JsonElement raiz)
    {
        if (!raiz.TryGetProperty("erro", out var campoErro))
            return false;

        return campoErro.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => string.Equals(campoErro.GetString(), "true", StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Null => false,
            _ => false
        };
    }

    /// <summary>Lê um campo de texto do JSON; tolera ausência e tipo não-string.</summary>
    private static string LerTexto(JsonElement raiz, string propriedade)
    {
        if (!raiz.TryGetProperty(propriedade, out var valor))
            return string.Empty;

        return valor.ValueKind == JsonValueKind.String
            ? valor.GetString() ?? string.Empty
            : valor.ToString();
    }
}

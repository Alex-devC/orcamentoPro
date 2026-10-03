using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Common.Formatters;

namespace OrcPro.Infrastructure.Services;

/// <summary>
/// Implementação do ICepService usando a API pública ViaCEP (https://viacep.com.br).
/// Gratuita, sem necessidade de chave de API, limite de 30 req/s por IP.
/// </summary>
public sealed class ViaCepService : ICepService
{
    private const string BaseAddressUrl = "https://viacep.com.br/ws/";

    private static readonly HttpClient DefaultHttpClient = new()
    {
        BaseAddress = new Uri(BaseAddressUrl),
        Timeout = TimeSpan.FromSeconds(10)
    };

    private readonly HttpClient _httpClient;

    /// <summary>Construtor padrão (usado pela injeção de dependência).</summary>
    public ViaCepService()
        : this(DefaultHttpClient)
    {
    }

    /// <summary>
    /// Construtor que recebe um <see cref="HttpClient"/> — permite testes isolados com um
    /// handler HTTP simulado, sem depender de rede. Se o cliente informado não tiver
    /// <see cref="HttpClient.BaseAddress"/>, a URL absoluta do ViaCEP é utilizada.
    /// </summary>
    public ViaCepService(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public string ProviderName => "ViaCEP";

    public async Task<CepAddressResult> ConsultarAsync(string cep, CancellationToken cancellationToken = default)
    {
        var cepNormalizado = CepMaskHelper.Normalizar(cep);

        if (!CepMaskHelper.EhValido(cepNormalizado))
        {
            return CepAddressResult.FailureResult(cepNormalizado, "CEP inválido: deve conter 8 dígitos", ProviderName);
        }

        try
        {
            var response = await _httpClient.GetAsync(MontarRequestUri(cepNormalizado), cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return CepAddressResult.FailureResult(cepNormalizado, $"Erro na consulta: HTTP {response.StatusCode}", ProviderName);
            }

            var result = await response.Content.ReadFromJsonAsync<ViaCepResponse>(cancellationToken: cancellationToken);

            if (result is null || result.Erro == true)
            {
                return CepAddressResult.FailureResult(cepNormalizado, "CEP não encontrado", ProviderName);
            }

            return CepAddressResult.SuccessResult(
                cep: cepNormalizado,
                logradouro: result.Logradouro,
                bairro: result.Bairro,
                cidade: result.Localidade,
                uf: result.Uf,
                complemento: result.Complemento,
                source: ProviderName
            );
        }
        catch (HttpRequestException ex)
        {
            return CepAddressResult.FailureResult(cepNormalizado, $"Erro de rede: {ex.Message}", ProviderName);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CepAddressResult.FailureResult(cepNormalizado, "Consulta cancelada", ProviderName);
        }
        catch (TaskCanceledException)
        {
            return CepAddressResult.FailureResult(cepNormalizado, "Timeout na consulta", ProviderName);
        }
        catch (JsonException ex)
        {
            return CepAddressResult.FailureResult(cepNormalizado, $"Erro ao processar resposta: {ex.Message}", ProviderName);
        }
        catch (Exception ex)
        {
            return CepAddressResult.FailureResult(cepNormalizado, $"Erro inesperado: {ex.Message}", ProviderName);
        }
    }

    /// <summary>
    /// Monta o URI de consulta. Usa caminho relativo quando o cliente possui BaseAddress
    /// (produção) e URL absoluta do ViaCEP quando o cliente foi injetado sem BaseAddress (testes).
    /// </summary>
    private Uri MontarRequestUri(string cepNormalizado)
    {
        var caminho = $"{cepNormalizado}/json/";
        return _httpClient.BaseAddress is null
            ? new Uri(BaseAddressUrl + caminho)
            : new Uri(caminho, UriKind.Relative);
    }

    private sealed class ViaCepResponse
    {
        public string? Cep { get; set; }
        public string? Logradouro { get; set; }
        public string? Complemento { get; set; }
        public string? Bairro { get; set; }
        public string? Localidade { get; set; }
        public string? Uf { get; set; }
        public string? Ibge { get; set; }
        public string? Gia { get; set; }
        public string? Ddd { get; set; }
        public string? Siafi { get; set; }

        /// <summary>
        /// O ViaCEP devolve este campo como booleano (<c>true</c>) ou como texto (<c>"true"</c>),
        /// dependendo do tipo de CEP inexistente consultado. O conversor tolera os dois formatos:
        /// sem ele, a desserialização falharia e o usuário veria um erro de JSON em vez de
        /// "CEP não encontrado".
        /// </summary>
        [JsonConverter(typeof(ViaCepErroConverter))]
        public bool? Erro { get; set; }

        private sealed class ViaCepErroConverter : JsonConverter<bool?>
        {
            public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                => reader.TokenType switch
                {
                    JsonTokenType.True => true,
                    JsonTokenType.False => false,
                    JsonTokenType.Null => null,
                    JsonTokenType.String => bool.TryParse(reader.GetString(), out var valor) ? valor : null,
                    _ => throw new JsonException($"Token inesperado para o campo 'erro': {reader.TokenType}")
                };

            public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
            {
                if (value.HasValue)
                    writer.WriteBooleanValue(value.Value);
                else
                    writer.WriteNullValue();
            }
        }
    }
}
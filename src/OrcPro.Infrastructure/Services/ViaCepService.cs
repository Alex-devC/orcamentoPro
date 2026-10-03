using System.Net.Http.Json;
using System.Text.Json;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Common.Formatters;

namespace OrcPro.Infrastructure.Services;

/// <summary>
/// Implementação do ICepService usando a API pública ViaCEP (https://viacep.com.br).
/// Gratuita, sem necessidade de chave de API, limite de 30 req/s por IP.
/// </summary>
public sealed class ViaCepService : ICepService
{
    private static readonly HttpClient HttpClient = new()
    {
        BaseAddress = new Uri("https://viacep.com.br/ws/"),
        Timeout = TimeSpan.FromSeconds(10)
    };

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
            var response = await HttpClient.GetAsync($"{cepNormalizado}/json/", cancellationToken);

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
        public bool Erro { get; set; }
    }
}
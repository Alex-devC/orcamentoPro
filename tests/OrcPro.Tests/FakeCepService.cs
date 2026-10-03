using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Common.Formatters;

namespace OrcPro.Tests;

/// <summary>
/// Implementação fake de ICepService para testes unitários.
/// Permite configurar resultados de sucesso ou falha.
/// </summary>
public class FakeCepService : ICepService
{
    private readonly Dictionary<string, CepAddressResult> _results = new();
    private readonly bool _returnFailure;
    private readonly string _failureMessage;

    public FakeCepService()
    {
        _returnFailure = false;
        _failureMessage = string.Empty;
    }

    public FakeCepService(bool returnFailure, string failureMessage)
    {
        _returnFailure = returnFailure;
        _failureMessage = failureMessage;
    }

    public string ProviderName => "FakeCEP";

    public void AddCepResult(string cep, string? logradouro, string? bairro, string? cidade, string? uf)
    {
        var normalized = CepMaskHelper.Normalizar(cep);
        _results[normalized] = CepAddressResult.SuccessResult(normalized, logradouro, bairro, cidade, uf, source: ProviderName);
    }

    public void SetCepNotFound(string cep)
    {
        var normalized = CepMaskHelper.Normalizar(cep);
        _results[normalized] = CepAddressResult.FailureResult(normalized, "CEP não encontrado", ProviderName);
    }

    public Task<CepAddressResult> ConsultarAsync(string cep, CancellationToken cancellationToken = default)
    {
        if (_returnFailure)
        {
            return Task.FromResult(CepAddressResult.FailureResult(
                CepMaskHelper.Normalizar(cep), _failureMessage, ProviderName));
        }

        var normalized = CepMaskHelper.Normalizar(cep);

        if (_results.TryGetValue(normalized, out var result))
        {
            return Task.FromResult(result);
        }

        return Task.FromResult(CepAddressResult.FailureResult(
            normalized, "CEP não encontrado", ProviderName));
    }
}
using OrcPro.Domain.Common.Formatters;

namespace OrcPro.Application.Interfaces.Services;

/// <summary>
/// Resultado da consulta de CEP.
/// </summary>
public sealed class CepAddressResult
{
    /// <summary>CEP consultado (apenas dígitos).</summary>
    public string Cep { get; init; } = string.Empty;

    /// <summary>Logradouro (rua, avenida, etc.).</summary>
    public string? Logradouro { get; init; }

    /// <summary>Bairro.</summary>
    public string? Bairro { get; init; }

    /// <summary>Cidade.</summary>
    public string? Cidade { get; init; }

    /// <summary>UF (2 letras).</summary>
    public string? Uf { get; init; }

    /// <summary>Complemento (se disponível).</summary>
    public string? Complemento { get; init; }

    /// <summary>Indica se a consulta foi bem-sucedida.</summary>
    public bool Success { get; init; }

    /// <summary>Mensagem de erro (se Success = false).</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Fonte da consulta (ex: "ViaCEP", "BrasilAPI", "Correios").</summary>
    public string? Source { get; init; }

    public static CepAddressResult SuccessResult(string cep, string? logradouro, string? bairro, string? cidade, string? uf, string? complemento = null, string? source = null)
        => new()
        {
            Cep = cep,
            Logradouro = logradouro,
            Bairro = bairro,
            Cidade = cidade,
            Uf = uf?.ToUpperInvariant(),
            Complemento = complemento,
            Success = true,
            Source = source
        };

    public static CepAddressResult FailureResult(string cep, string errorMessage, string? source = null)
        => new()
        {
            Cep = cep,
            Success = false,
            ErrorMessage = errorMessage,
            Source = source
        };
}

/// <summary>
/// Interface para serviço de consulta de CEP.
/// Implementações podem usar ViaCEP, BrasilAPI, Correios, etc.
/// </summary>
public interface ICepService
{
    /// <summary>
    /// Consulta endereço por CEP de forma assíncrona.
    /// </summary>
    /// <param name="cep">CEP com ou sem máscara (será normalizado para 8 dígitos).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Resultado com endereço ou erro.</returns>
    Task<CepAddressResult> ConsultarAsync(string cep, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nome identificador do provedor (ex: "ViaCEP", "BrasilAPI").
    /// </summary>
    string ProviderName { get; }
}
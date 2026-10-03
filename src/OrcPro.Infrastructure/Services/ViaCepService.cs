using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Application.Services;
using OrcPro.Domain.Common.Formatters;

namespace OrcPro.Infrastructure.Services;

/// <summary>
/// Consulta de CEP via API pública ViaCEP (https://viacep.com.br) — REESCRITA com
/// foco em observabilidade: cada consulta gera uma sequência de diagnóstico no
/// arquivo <c>logcep.txt</c> ao lado do executável (ver <see cref="CepDiagnosticLogger"/>).
///
/// Fluxo linear, explícito e sem estado:
///   CEP recebido → normalização → validação dos 8 dígitos → montagem da URL →
///   requisição HTTP → status HTTP → texto bruto da resposta → desserialização
///   (JsonDocument) → interpretação do campo "erro" (tolerante a true / "true" /
///   ausente) → retorno estruturado (CepAddressResult).
///
/// A interpretação NÃO depende de DTO rígido: <see cref="JsonDocument"/> lê cada
/// campo explicitamente, e o JSON bruto é sempre registrado no log.
/// </summary>
public sealed class ViaCepService : ICepService
{
    private const string Provedor = "ViaCEP";
    private const string BaseAddressUrl = "https://viacep.com.br/ws/";

    /// <summary>
    /// HttpClient compartilhado (singleton estático) — não cria um cliente por clique.
    /// Timeout razoável de 10 segundos.
    /// </summary>
    private static readonly HttpClient HttpClientCompartilhado = new()
    {
        BaseAddress = new Uri(BaseAddressUrl),
        Timeout = TimeSpan.FromSeconds(10)
    };

    private static int _inicializacaoRegistrada;

    private readonly HttpClient _httpClient;

    /// <summary>Construtor padrão (usado pela injeção de dependência): usa o cliente compartilhado.</summary>
    public ViaCepService()
        : this(HttpClientCompartilhado)
    {
    }

    /// <summary>
    /// Construtor que recebe um <see cref="HttpClient"/> — permite testes isolados com um
    /// handler HTTP simulado, sem depender de rede. A URL consultada é sempre absoluta
    /// (https://viacep.com.br/ws/{cep}/json/), independentemente do BaseAddress informado.
    /// </summary>
    public ViaCepService(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public string ProviderName => Provedor;

    public async Task<CepAddressResult> ConsultarAsync(string cep, CancellationToken cancellationToken = default)
    {
        RegistrarInicializacao();

        var sessao = CepDiagnosticLogger.Iniciar("INÍCIO DA CONSULTA CEP");
        try
        {
            // [1-5] Recebimento, normalização e validação ----------------------------
            sessao.Etapa("CEP recebido pelo serviço:", string.IsNullOrWhiteSpace(cep) ? "(vazio/nulo)" : cep);
            sessao.Etapa("CEP antes da normalização:", string.IsNullOrWhiteSpace(cep) ? "(vazio/nulo)" : cep);

            var normalizado = CepMaskHelper.Normalizar(cep);
            sessao.Etapa("CEP normalizado:", normalizado);
            sessao.Etapa("Quantidade de dígitos:", normalizado.Length.ToString());

            var valido = CepMaskHelper.EhValido(normalizado);
            sessao.Etapa("CEP considerado válido:", valido ? "SIM" : "NÃO");

            if (!valido)
            {
                sessao.Etapa("Resultado da consulta:", "FALHA — CEP inválido (são necessários 8 dígitos)");
                sessao.Encerrar();
                return CepAddressResult.FailureResult(normalizado, "CEP inválido: deve conter 8 dígitos", Provedor);
            }

            // [6-7] Provedor e URL ----------------------------------------------------
            sessao.Etapa("Provedor:", Provedor);

            var url = $"{BaseAddressUrl}{normalizado}/json/";
            sessao.Etapa("URL montada:", url);
            sessao.Etapa("Timeout do HttpClient:", $"{_httpClient.Timeout.TotalSeconds:0} s");

            // [8-9] Requisição HTTP ---------------------------------------------------
            sessao.Etapa("Iniciando requisição HTTP...");

            HttpResponseMessage resposta;
            try
            {
                resposta = await _httpClient.GetAsync(new Uri(url, UriKind.Absolute), cancellationToken);
                sessao.Etapa(
                    "HTTP Status:",
                    $"{(int)resposta.StatusCode} {resposta.StatusCode} ({resposta.ReasonPhrase})");
            }
            catch (HttpRequestException ex)
            {
                LogarExcecao(sessao, "Falha na requisição HTTP", ex);
                sessao.Etapa("Resultado da consulta:", "FALHA — erro de rede");
                sessao.Encerrar();
                return CepAddressResult.FailureResult(normalizado, $"Erro de rede: {ex.Message}", Provedor);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                sessao.Etapa("Resultado da consulta:", "FALHA — consulta cancelada pelo chamador");
                sessao.Encerrar();
                return CepAddressResult.FailureResult(normalizado, "Consulta cancelada", Provedor);
            }
            catch (OperationCanceledException ex)
            {
                LogarExcecao(sessao, "Requisição cancelada (provável timeout)", ex);
                sessao.Etapa("Resultado da consulta:", "FALHA — timeout na requisição");
                sessao.Encerrar();
                return CepAddressResult.FailureResult(normalizado, "Timeout na consulta", Provedor);
            }

            // [10-11] Texto bruto da resposta ------------------------------------------
            string? textoBruto;
            try
            {
                textoBruto = await resposta.Content.ReadAsStringAsync(cancellationToken);
                sessao.Etapa(
                    "Resposta bruta recebida:",
                    string.IsNullOrEmpty(textoBruto) ? "(corpo vazio)" : textoBruto);
                sessao.Etapa("Tamanho da resposta:", $"{Encoding.UTF8.GetByteCount(textoBruto ?? string.Empty)} bytes");
            }
            catch (OperationCanceledException ex)
            {
                LogarExcecao(sessao, "Leitura do corpo cancelada (provável timeout)", ex);
                sessao.Etapa("Resultado da consulta:", "FALHA — timeout ao ler corpo da resposta");
                sessao.Encerrar();
                return CepAddressResult.FailureResult(
                    normalizado,
                    cancellationToken.IsCancellationRequested ? "Consulta cancelada" : "Timeout na consulta",
                    Provedor);
            }
            catch (HttpRequestException ex)
            {
                LogarExcecao(sessao, "Falha ao ler corpo da resposta", ex);
                sessao.Etapa("Resultado da consulta:", "FALHA — erro de rede ao ler resposta");
                sessao.Encerrar();
                return CepAddressResult.FailureResult(normalizado, $"Erro de rede: {ex.Message}", Provedor);
            }

            // ReadAsStringAsync pode retornar null (corpo ausente): normaliza para vazio.
            textoBruto ??= string.Empty;

            if (!resposta.IsSuccessStatusCode)
            {
                sessao.Etapa("Resultado da consulta:", $"FALHA — HTTP {(int)resposta.StatusCode}");
                sessao.Encerrar();
                return CepAddressResult.FailureResult(
                    normalizado,
                    $"Erro na consulta: HTTP {resposta.StatusCode}",
                    Provedor);
            }

            // [12-13] Desserialização e interpretação ---------------------------------
            sessao.Etapa("Iniciando interpretação JSON...");

            JsonDocument documento;
            try
            {
                documento = JsonDocument.Parse(textoBruto);
            }
            catch (JsonException ex)
            {
                LogarExcecao(sessao, "Falha ao interpretar resposta — JSON inválido", ex);
                sessao.Etapa("Resultado da consulta:", "FALHA — resposta não é JSON válido");
                sessao.Encerrar();
                return CepAddressResult.FailureResult(normalizado, $"Erro ao processar resposta: {ex.Message}", Provedor);
            }

            using (documento)
            {
                if (documento.RootElement.ValueKind != JsonValueKind.Object)
                {
                    sessao.Etapa("Tipo da raiz do JSON:", documento.RootElement.ValueKind.ToString());
                    sessao.Etapa("Resultado da consulta:", "FALHA — estrutura inesperada (esperado objeto JSON)");
                    sessao.Encerrar();
                    return CepAddressResult.FailureResult(
                        normalizado,
                        $"Erro ao processar resposta: JSON raiz é {documento.RootElement.ValueKind}, esperado Object",
                        Provedor);
                }

                var raiz = documento.RootElement;
                var erro = InterpretarCampoErro(raiz, sessao);

                if (erro)
                {
                    sessao.Etapa("Resultado da consulta:", "FALHA — CEP não encontrado (provedor respondeu erro)");
                    sessao.Encerrar();
                    return CepAddressResult.FailureResult(normalizado, "CEP não encontrado", Provedor);
                }

                // [14-18] Extração dos campos de endereço -------------------------------
                var logradouro = LerTexto(raiz, "logradouro");
                var bairro = LerTexto(raiz, "bairro");
                var cidade = LerTexto(raiz, "localidade");
                var uf = LerTexto(raiz, "uf");
                var complemento = LerTexto(raiz, "complemento");

                sessao.Etapa("Resultado da consulta:", "SUCESSO");
                sessao.Etapa("Logradouro:", string.IsNullOrEmpty(logradouro) ? "(vazio)" : logradouro);
                sessao.Etapa("Bairro:", string.IsNullOrEmpty(bairro) ? "(vazio)" : bairro);
                sessao.Etapa("Cidade:", string.IsNullOrEmpty(cidade) ? "(vazio)" : cidade);
                sessao.Etapa("UF:", string.IsNullOrEmpty(uf) ? "(vazio)" : uf);
                sessao.Etapa("Consulta concluída com sucesso.");
                sessao.Encerrar();

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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            sessao.Etapa("Resultado da consulta:", "FALHA — consulta cancelada");
            sessao.Encerrar();
            return CepAddressResult.FailureResult(CepMaskHelper.Normalizar(cep), "Consulta cancelada", Provedor);
        }
        catch (Exception ex)
        {
            LogarExcecao(sessao, "Erro inesperado na consulta de CEP", ex);
            sessao.Etapa("Resultado da consulta:", "FALHA — exceção inesperada");
            sessao.Encerrar();
            return CepAddressResult.FailureResult(CepMaskHelper.Normalizar(cep), $"Erro inesperado: {ex.Message}", Provedor);
        }
    }

    /// <summary>
    /// Interpreta o campo "erro" do ViaCEP de forma tolerante via <see cref="JsonDocument"/>:
    /// aceita <c>true</c> (bool), <c>"true"</c> (string), <c>false</c>, <c>null</c>, ausente
    /// e formatos desconhecidos (registrados no log) sem quebrar a consulta.
    /// </summary>
    private static bool InterpretarCampoErro(JsonElement raiz, CepLogSession sessao)
    {
        if (!raiz.TryGetProperty("erro", out var campoErro))
        {
            sessao.Etapa("Campo erro encontrado:", "ausente — tratando como resposta normal");
            return false;
        }

        switch (campoErro.ValueKind)
        {
            case JsonValueKind.True:
                sessao.Etapa("Campo erro encontrado:", "true (boolean)");
                return true;

            case JsonValueKind.False:
                sessao.Etapa("Campo erro encontrado:", "false (boolean)");
                return false;

            case JsonValueKind.String:
            {
                var valor = campoErro.GetString() ?? string.Empty;
                var interpretado = string.Equals(valor.Trim(), "true", StringComparison.OrdinalIgnoreCase);
                sessao.Etapa(
                    "Campo erro encontrado:",
                    $"'{valor}' (string) → interpretado como {(interpretado ? "true" : "false")}");
                return interpretado;
            }

            case JsonValueKind.Null:
                sessao.Etapa("Campo erro encontrado:", "null → interpretado como ausente");
                return false;

            default:
                sessao.Etapa(
                    "Campo erro encontrado:",
                    $"{campoErro} ({campoErro.ValueKind}) — formato desconhecido, tratado como ausente");
                return false;
        }
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

    /// <summary>Registra a inicialização do serviço uma única vez por execução.</summary>
    private static void RegistrarInicializacao()
    {
        if (Interlocked.Exchange(ref _inicializacaoRegistrada, 1) != 0)
            return;

        CepDiagnosticLogger.Linha("[CEP] Serviço inicializado");
        CepDiagnosticLogger.Linha($"[CEP] Provider: {Provedor}");
        CepDiagnosticLogger.Linha($"[CEP] BaseAddress: {BaseAddressUrl}");
        CepDiagnosticLogger.Linha("[CEP] Timeout: 10 s");
        CepDiagnosticLogger.Linha($"[CEP] Log: {CepDiagnosticLogger.CaminhoArquivo}");
    }

    /// <summary>Registra tipo, mensagem e stack trace de uma exceção no log.</summary>
    private static void LogarExcecao(CepLogSession sessao, string contexto, Exception ex)
    {
        sessao.Etapa($"{contexto} — Exception:");
        sessao.Linha($"Tipo: {ex.GetType().FullName}");
        sessao.Linha($"Mensagem: {ex.Message}");
        sessao.Linha($"StackTrace: {ex.StackTrace}");
    }
}

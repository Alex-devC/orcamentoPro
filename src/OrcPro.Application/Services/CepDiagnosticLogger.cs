using System;
using System.IO;
using System.Text;

namespace OrcPro.Application.Services;

/// <summary>
/// Log de diagnóstico específico do fluxo de CEP.
///
/// Registra em <c>logcep.txt</c> ao lado do executável (AppContext.BaseDirectory,
/// nunca %APPDATA% nem pasta escondida). Arquivo append-only, UTF-8, criado se não
/// existir, adicionado se existir. Thread-safe (lock global) — nunca lança exceção:
/// um problema de IO não pode derrubar a consulta.
///
/// Uso:
/// - <see cref="Linha(string)"/> para linhas avulsas ([CEP] ..., [APLICAÇÃO] ...);
/// - <see cref="Iniciar(string)"/> para a sequência numerada de etapas de uma consulta
///   (retorna uma <see cref="CepLogSession"/> com numeração automática).
/// </summary>
public static class CepDiagnosticLogger
{
    private static readonly object Sync = new();

    /// <summary>Caminho completo do arquivo de log — sempre ao lado do executável.</summary>
    public static string CaminhoArquivo
        => Path.Combine(AppContext.BaseDirectory, "logcep.txt");

    /// <summary>Escreve uma linha simples com timestamp (append, UTF-8).</summary>
    public static void Linha(string texto)
        => EscreverLinhaComTimestamp(texto);

    /// <summary>Registra uma exceção completa (tipo, mensagem e stack trace) no log.</summary>
    public static void LogarException(string contexto, Exception ex)
    {
        Linha($"{contexto} — Exception:");
        Linha($"Tipo: {ex.GetType().FullName}");
        Linha($"Mensagem: {ex.Message}");
        Linha($"StackTrace: {ex.StackTrace}");
    }

    /// <summary>Inicia uma seção numerada de diagnóstico (ex.: "INÍCIO DA CONSULTA CEP").</summary>
    public static CepLogSession Iniciar(string titulo)
        => new(titulo);

    internal static void EscreverLinhaComTimestamp(string texto)
    {
        EscreverComRetry(
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {texto}{Environment.NewLine}");
    }

    internal static void EscreverBloco(string bloco)
    {
        EscreverComRetry(bloco + Environment.NewLine);
    }

    /// <summary>
    /// Escrita append com pequenas novas tentativas: com dezenas de testes gravando
    /// em paralelo, o leitor (File.ReadAllText) pode segurar o arquivo por um instante
    /// e uma única tentativa pode perder a linha silenciosamente.
    /// </summary>
    private static void EscreverComRetry(string conteudo, int tentativas = 8)
    {
        var codificacao = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        for (var tentativa = 0; tentativa < tentativas; tentativa++)
        {
            try
            {
                lock (Sync)
                {
                    var caminho = CaminhoArquivo;
                    var diretorio = Path.GetDirectoryName(caminho);
                    if (!string.IsNullOrEmpty(diretorio))
                        Directory.CreateDirectory(diretorio);

                    File.AppendAllText(caminho, conteudo, codificacao);
                }

                return;
            }
            catch (IOException) when (tentativa < tentativas - 1)
            {
                Thread.Sleep(20);
            }
            catch
            {
                // Log de diagnóstico nunca pode quebrar a consulta de CEP.
                return;
            }
        }
    }
}

/// <summary>
/// Sequência numerada de etapas de uma única consulta de CEP.
/// Numeração automática (1, 2, 3, ...), cabeçalho com separador e rodapé idempotente.
/// </summary>
public sealed class CepLogSession
{
    private readonly object _sync = new();
    private int _etapa;
    private bool _encerrada;

    public CepLogSession(string titulo)
    {
        var separador = new string('=', 64);
        CepDiagnosticLogger.EscreverBloco(
            separador + Environment.NewLine +
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}]" + Environment.NewLine +
            titulo + Environment.NewLine +
            separador);
    }

    /// <summary>
    /// Registra uma etapa numerada. Sem valor: imprime só o rótulo (ex.: "[8] Iniciando requisição HTTP...").
    /// Com valor: imprime o rótulo e o valor na linha seguinte.
    /// </summary>
    public void Etapa(string rotulo, string? valor = null)
    {
        lock (_sync)
        {
            _etapa++;
            var texto = valor is null
                ? $"[{_etapa}] {rotulo}"
                : $"[{_etapa}] {rotulo}{Environment.NewLine}{valor}";
            CepDiagnosticLogger.EscreverBloco(texto);
        }
    }

    /// <summary>Registra uma linha adicional (sem numeração) dentro da seção.</summary>
    public void Linha(string texto)
        => CepDiagnosticLogger.EscreverBloco(texto);

    /// <summary>Encerra a seção com rodapé. Chamadas repetidas são ignoradas.</summary>
    public void Encerrar(string rodape = "FIM DA CONSULTA")
    {
        lock (_sync)
        {
            if (_encerrada)
                return;
            _encerrada = true;
            CepDiagnosticLogger.EscreverBloco(rodape + Environment.NewLine + new string('=', 64));
        }
    }
}

using System;
using System.IO;
using System.Threading;
using OrcPro.Application.Services;
using Xunit;

namespace OrcPro.Tests;

/// <summary>
/// Testes do <see cref="CepDiagnosticLogger"/>: arquivo logcep.txt ao lado do executável
/// (AppContext.BaseDirectory, nunca %APPDATA%), criação automática, append-only e UTF-8.
/// </summary>
public class CepDiagnosticLoggerTests
{
    [Fact]
    public void CaminhoArquivo_DeveFicarAoLadoDoExecutavel()
    {
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "logcep.txt"), CepDiagnosticLogger.CaminhoArquivo);
        Assert.StartsWith(AppContext.BaseDirectory, CepDiagnosticLogger.CaminhoArquivo, StringComparison.Ordinal);
        Assert.EndsWith("logcep.txt", CepDiagnosticLogger.CaminhoArquivo, StringComparison.Ordinal);
    }

    [Fact]
    public void Linha_DeveCriarArquivoSeNaoExistirEAdicionarSeExistir()
    {
        var marcador1 = $"[TESTE-LOGGER] {Guid.NewGuid():N}";
        var marcador2 = $"[TESTE-LOGGER] {Guid.NewGuid():N}";

        // Primeira linha: cria (se necessário). Segunda: adiciona sem apagar a primeira.
        CepDiagnosticLogger.Linha(marcador1);
        Thread.Sleep(20);
        CepDiagnosticLogger.Linha(marcador2);

        var conteudo = AguardarConteudo(marcador2 + Environment.NewLine);
        Assert.Contains(marcador1, conteudo); // append-only: a linha anterior continua lá
        Assert.Contains(marcador2, conteudo);
    }

    [Fact]
    public void Sessao_DeveNumerarEtapasEEncerrarComRodape()
    {
        var titulo = $"SESSAO TESTE {Guid.NewGuid():N}";

        var sessao = CepDiagnosticLogger.Iniciar(titulo);
        sessao.Etapa("Primeira etapa:", "valor1");
        sessao.Etapa("Segunda etapa:");
        sessao.Encerrar();
        sessao.Encerrar(); // idempotente — não deve lançar exceção

        var conteudo = AguardarConteudo("FIM DA CONSULTA" + Environment.NewLine);
        Assert.Contains(titulo, conteudo);
        Assert.Contains("[1] Primeira etapa:", conteudo);
        Assert.Contains("valor1", conteudo);
        Assert.Contains("[2] Segunda etapa:", conteudo);
        Assert.Contains("FIM DA CONSULTA", conteudo);
    }

    [Fact]
    public void LogarException_DeveRegistrarTipoMensagemEStackTrace()
    {
        var contexto = $"[TESTE-LOGGER] {Guid.NewGuid():N}";
        CepDiagnosticLogger.LogarException(contexto, new InvalidOperationException("erro simulado"));

        // Aguarda as 4 linhas (contexto + Tipo + Mensagem) para não casar uma leitura
        // rasgada do arquivo enquanto outros testes gravam concorrentemente.
        var conteudo = AguardarPredicado(c =>
            c.Contains($"{contexto} — Exception:", StringComparison.Ordinal) &&
            c.Contains("InvalidOperationException", StringComparison.Ordinal) &&
            c.Contains("erro simulado", StringComparison.Ordinal));
        Assert.Contains($"{contexto} — Exception:", conteudo);
        Assert.Contains("InvalidOperationException", conteudo);
        Assert.Contains("erro simulado", conteudo);
    }

    /// <summary>Lê logcep.txt com pequenas novas tentativas (escrita concorrente de outros testes).</summary>
    private static string AguardarConteudo(string marcador, int timeoutMs = 3000)
        => AguardarPredicado(c => c.Contains(marcador, StringComparison.Ordinal), timeoutMs);

    /// <summary>Lê logcep.txt até um predicado ser satisfeito (evita leitura rasgada concorrente).</summary>
    private static string AguardarPredicado(Func<string, bool> predicado, int timeoutMs = 3000)
    {
        var caminho = CepDiagnosticLogger.CaminhoArquivo;
        var fim = DateTime.Now.AddMilliseconds(timeoutMs);
        string conteudo = string.Empty;

        while (DateTime.Now < fim)
        {
            try
            {
                conteudo = File.ReadAllText(caminho);
                if (predicado(conteudo))                    return conteudo;
            }
            catch (IOException)
            {
                // Escrita concorrente ou arquivo ainda não criado: tenta de novo.
            }

            Thread.Sleep(50);
        }

        return conteudo;
    }
}

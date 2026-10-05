using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using OrcPro.Application.Exceptions;
using OrcPro.Application.Interfaces.Services;

namespace OrcPro.Infrastructure.Services;

/// <summary>
/// Mantém o logotipo da empresa em <c>%LocalAppData%\OrcPro\logo</c>.
/// </summary>
/// <para>A pasta é escolhida de propósito: é independente do local do executável, então a
/// logo continua disponível se o programa for movido para outro diretório. O banco grava
/// somente o nome do arquivo, e este serviço reconstrói o caminho absoluto sob demanda —
/// é esse contrato que os futuros módulos de PDF e relatórios vão consumir.</para>
/// </remarks>
public sealed class EmpresaLogoStorage : IEmpresaLogoStorage
{
    /// <summary>Nome fixo do arquivo de logo; trocar de logo apenas sobrescreve este arquivo.</summary>
    public const string NomeArquivoPadrao = "logo-empresa";

    private static readonly string[] Extensoes = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

    private readonly string _pasta;

    public EmpresaLogoStorage() : this(PastaPadrao())
    {
    }

    /// <summary>Permite injetar a pasta (usado em testes).</summary>
    public EmpresaLogoStorage(string pasta)
    {
        _pasta = pasta;
    }

    public IReadOnlyList<string> ExtensoesAceitas => Extensoes;

    /// <summary>Pasta gerenciada de logos, criada sob demanda.</summary>
    public static string PastaPadrao()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OrcPro",
            "logo");

    public string Importar(string caminhoOrigem, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(caminhoOrigem))
            throw new ValidationException("Informe o arquivo de logo.");

        if (!File.Exists(caminhoOrigem))
            throw new NotFoundException("Arquivo de logo", caminhoOrigem);

        var extensao = Path.GetExtension(caminhoOrigem).ToLowerInvariant();
        if (Array.IndexOf(Extensoes, extensao) < 0)
            throw new ValidationException(
                $"Formato de imagem não suportado ({extensao}). Use: {string.Join(", ", Extensoes)}.");

        Directory.CreateDirectory(_pasta);

        var destino = Path.Combine(_pasta, NomeArquivoPadrao + extensao);

        // Remove a logo anterior que usasse outra extensão, para não deixar lixo acumulado.
        foreach (var antiga in Extensoes)
        {
            if (antiga == extensao)
                continue;

            var caminhoAntigo = Path.Combine(_pasta, NomeArquivoPadrao + antiga);
            if (File.Exists(caminhoAntigo))
                File.Delete(caminhoAntigo);
        }

        // File.Copy sobrescreve: trocar a logo é a operação normal.
        File.Copy(caminhoOrigem, destino, overwrite: true);

        return Path.GetFileName(destino);
    }

    public void Remover(string? nomeArquivo)
    {
        foreach (var extensao in Extensoes)
        {
            var caminho = Path.Combine(_pasta, NomeArquivoPadrao + extensao);
            if (File.Exists(caminho))
                File.Delete(caminho);
        }

        // Arquivo legado com nome arbitrário (persistido por versões anteriores).
        if (!string.IsNullOrWhiteSpace(nomeArquivo))
        {
            var legado = ResolverCaminho(nomeArquivo);
            if (legado is not null && File.Exists(legado))
                File.Delete(legado);
        }
    }

    public string? ObterCaminhoAbsoluto(string? nomeArquivo)
    {
        var caminho = ResolverCaminho(nomeArquivo);
        return caminho is not null && File.Exists(caminho) ? caminho : null;
    }

    public bool Existe(string? nomeArquivo) => ObterCaminhoAbsoluto(nomeArquivo) is not null;

    /// <summary>
    /// Reconstrói o caminho a partir do nome persistido. Um valor já absoluto é aceito
    /// (registros criados antes desta mudança podem conter um caminho completo).
    /// </summary>
    private string? ResolverCaminho(string? nomeArquivo)
    {
        if (string.IsNullOrWhiteSpace(nomeArquivo))
            return null;

        var nome = nomeArquivo.Trim();

        return Path.IsPathRooted(nome)
            ? nome
            : Path.Combine(_pasta, nome);
    }
}
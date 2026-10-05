namespace OrcPro.Application.Interfaces.Services;

/// <summary>
/// Guarda o arquivo de logotipo da empresa em uma área gerenciada pela própria aplicação.
/// </summary>
/// <para>A logo é COPIADA para a pasta de dados do OrcPro e, no banco, persiste apenas o
/// <b>nome</b> do arquivo. Isso é o que permite que a logo continue disponível depois de
/// fechar o programa, reabrir e até mover o executável para outro diretório — e também
/// depois que o usuário apagar ou mover o arquivo original que escolheu.</para>
/// </remarks>
public interface IEmpresaLogoStorage
{
    /// <summary>Extensões de imagem aceitas (png, jpg, jpeg, bmp, gif).</summary>
    IReadOnlyList<string> ExtensoesAceitas { get; }

    /// <summary>
    /// Copia a imagem escolhida para a área gerenciada e devolve o <b>nome</b> do arquivo
    /// gravado (o valor a persistir). Lança se a origem não existir ou não for uma imagem aceita.
    /// </summary>
    string Importar(string caminhoOrigem, CancellationToken cancellationToken = default);

    /// <summary>Remove a logo gerenciada. Silencioso quando não há logo.</summary>
    void Remover(string? nomeArquivo);

    /// <summary>
    /// Caminho absoluto do arquivo a partir do nome persistido, ou <c>null</c> quando não
    /// houver logo ou o arquivo não existir mais.
    /// </summary>
    string? ObterCaminhoAbsoluto(string? nomeArquivo);

    /// <summary>Indica se existe arquivo de logo para o nome persistido.</summary>
    bool Existe(string? nomeArquivo);
}
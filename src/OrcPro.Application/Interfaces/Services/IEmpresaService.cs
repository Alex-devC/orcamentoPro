using OrcPro.Application.DTOs.Empresa;

namespace OrcPro.Application.Interfaces.Services;

/// <summary>
/// Acesso ao Emitente / Minha Empresa: registro único por instalação.
/// </summary>
/// <remarks>
/// É o ponto de entrada que Orçamentos, PDF e relatórios futures vão consumir para obter
/// razão social, CNPJ, endereço, contatos e logotipo. A implementação esconde por completo
/// a persistência (banco e arquivo de logo).
/// </remarks>
public interface IEmpresaService
{
    /// <summary>
    /// Emitente configurado, ou <c>null</c> quando a empresa ainda não foi cadastrada.
    /// Nunca cria dados fictícios: a tela precisa abrir vazia para o usuário preencher.
    /// </summary>
    Task<EmpresaDto?> ObterAsync(CancellationToken cancellationToken = default);

    /// <summary>Indica se o emitente já foi configurado nesta instalação.</summary>
    Task<bool> EstaConfiguradoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cadastra na primeira vez e atualiza depois. A operação é idempotente quanto ao
    /// registro: nunca cria um segundo emitente, mesmo que o DTO venha com Id = 0 depois
    /// que outro cadastro já exista.
    /// </summary>
    Task<EmpresaDto> SalvarAsync(SalvarEmpresaDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Caminho absoluto do arquivo de logotipo mantido pela aplicação, ou <c>null</c>.
    /// Preparado para consumo por PDF e relatórios.
    /// </summary>
    Task<string?> ObterLogoCaminhoAbsolutoAsync(CancellationToken cancellationToken = default);

    /// <summary>Conteúdo bruto do logotipo (bytes), ou <c>null</c> quando não houver logo.</summary>
    Task<byte[]?> ObterLogoBytesAsync(CancellationToken cancellationToken = default);
}

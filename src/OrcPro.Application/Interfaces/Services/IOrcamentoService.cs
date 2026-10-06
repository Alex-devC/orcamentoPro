using OrcPro.Application.DTOs.Common;
using OrcPro.Application.DTOs.Orcamento;

namespace OrcPro.Application.Interfaces.Services;

public interface IOrcamentoService
{
    // Casos de uso principais
    Task<OrcamentoDto> ObterPorIdAsync(int id, CancellationToken cancellationToken = default);
    Task<OrcamentoDto> ObterPorNumeroAsync(string numero, CancellationToken cancellationToken = default);
    Task<PagedResult<OrcamentoResumoDto>> ListarPaginadoAsync(PagedRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrcamentoStatusDto>> ListarStatusDisponiveisAsync(CancellationToken cancellationToken = default);

    Task<OrcamentoDto> CriarAsync(CriarOrcamentoDto dto, CancellationToken cancellationToken = default);
    Task<OrcamentoDto> EditarAsync(AtualizarOrcamentoDto dto, int usuarioId, CancellationToken cancellationToken = default);
    Task<OrcamentoDto> AlterarStatusAsync(AlterarStatusOrcamentoDto dto, CancellationToken cancellationToken = default);
    Task<OrcamentoDto> ClonarAsync(int orcamentoId, int usuarioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exclui o orçamento respeitando as regras: orçamento finalizado ou cancelado é
    /// terminal e não pode ser excluído (use a inativação pelo status).
    /// </summary>
    Task ExcluirAsync(ExcluirOrcamentoDto dto, CancellationToken cancellationToken = default);

    // Itens (Peças/Materiais)
    Task<OrcamentoDto> AdicionarItemAsync(int orcamentoId, AdicionarItemDto dto, int usuarioId, CancellationToken cancellationToken = default);
    Task<OrcamentoDto> RemoverItemAsync(int orcamentoId, int itemId, int usuarioId, CancellationToken cancellationToken = default);

    // Mão de obra
    Task<OrcamentoDto> AdicionarMaoDeObraAsync(int orcamentoId, AdicionarMaoDeObraDto dto, int usuarioId, CancellationToken cancellationToken = default);
    Task<OrcamentoDto> RemoverMaoDeObraAsync(int orcamentoId, int maoDeObraId, int usuarioId, CancellationToken cancellationToken = default);

    // Técnicos
    Task<OrcamentoDto> AssociarTecnicoAsync(int orcamentoId, AssociarTecnicoDto dto, int usuarioId, CancellationToken cancellationToken = default);
    Task<OrcamentoDto> DesassociarTecnicoAsync(int orcamentoId, int tecnicoId, int usuarioId, CancellationToken cancellationToken = default);

    // Cálculos de Totais
    Task<decimal> CalcularTotalPecasAsync(int orcamentoId, CancellationToken cancellationToken = default);
    Task<decimal> CalcularTotalMaoDeObraAsync(int orcamentoId, CancellationToken cancellationToken = default);
    Task<decimal> CalcularTotalGeralAsync(int orcamentoId, CancellationToken cancellationToken = default);
}

using OrcPro.Application.DTOs.Relatorio;

namespace OrcPro.Application.Interfaces.Services;

public interface IRelatorioService
{
    Task<RelatorioVendasPeriodoDto> ObterRelatorioVendasAsync(RelatorioFiltroDto filtro, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RelatorioOrcamentosPorStatusDto>> ObterOrcamentosPorStatusAsync(RelatorioFiltroDto filtro, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RelatorioDesempenhoTecnicoDto>> ObterDesempenhoTecnicosAsync(RelatorioFiltroDto filtro, CancellationToken cancellationToken = default);
}

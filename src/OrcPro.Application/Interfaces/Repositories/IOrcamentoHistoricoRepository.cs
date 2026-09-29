using OrcPro.Domain.Entities.Orcamento;

namespace OrcPro.Application.Interfaces.Repositories;

public interface IOrcamentoHistoricoRepository : IRepository<OrcamentoHistorico>
{
    Task<IReadOnlyList<OrcamentoHistorico>> GetByOrcamentoIdAsync(int orcamentoId, CancellationToken cancellationToken = default);
}

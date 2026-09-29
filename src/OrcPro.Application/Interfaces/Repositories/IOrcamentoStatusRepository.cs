using OrcPro.Domain.Entities.Orcamento;

namespace OrcPro.Application.Interfaces.Repositories;

public interface IOrcamentoStatusRepository : IRepository<OrcamentoStatus>
{
    Task<OrcamentoStatus?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrcamentoStatus>> GetAllAtivosAsync(CancellationToken cancellationToken = default);
}

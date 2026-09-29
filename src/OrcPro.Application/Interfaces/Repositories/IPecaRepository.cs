using OrcPro.Domain.Entities.Peca;

namespace OrcPro.Application.Interfaces.Repositories;

public interface IPecaRepository : IRepository<Peca>
{
    Task<Peca?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default);
    Task<bool> ExistsCodigoAsync(string codigo, int? ignorarId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Peca>> GetAllAtivosAsync(CancellationToken cancellationToken = default);
}

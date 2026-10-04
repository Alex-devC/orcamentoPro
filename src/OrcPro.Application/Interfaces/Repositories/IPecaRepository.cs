using OrcPro.Domain.Entities.Peca;

namespace OrcPro.Application.Interfaces.Repositories;

public interface IPecaRepository : IRepository<Peca>
{
    Task<Peca?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default);
    Task<bool> ExistsCodigoAsync(string codigo, int? ignorarId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Peca>> GetAllAtivosAsync(CancellationToken cancellationToken = default);

    /// <summary>Próximo código sequencial disponível (PEC-0001, PEC-0002, ...).</summary>
    Task<string> GerarProximoCodigoAsync(CancellationToken cancellationToken = default);
}

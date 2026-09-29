using OrcPro.Domain.Entities.Tecnico;

namespace OrcPro.Application.Interfaces.Repositories;

public interface ITecnicoRepository : IRepository<Tecnico>
{
    Task<Tecnico?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default);
    Task<Tecnico?> GetByCpfAsync(string cpf, CancellationToken cancellationToken = default);
    Task<bool> ExistsCodigoAsync(string codigo, int? ignorarId = null, CancellationToken cancellationToken = default);
    Task<string> GerarProximoCodigoAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tecnico>> GetAllAtivosAsync(CancellationToken cancellationToken = default);
}

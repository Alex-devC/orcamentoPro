using OrcPro.Domain.Entities.Seguranca;

namespace OrcPro.Application.Interfaces.Repositories;

public interface IPerfilRepository : IRepository<Perfil>
{
    Task<Perfil?> GetWithPermissoesAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Perfil>> GetAllAtivosAsync(CancellationToken cancellationToken = default);
}

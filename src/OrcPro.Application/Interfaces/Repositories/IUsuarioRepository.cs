using OrcPro.Domain.Entities.Seguranca;

namespace OrcPro.Application.Interfaces.Repositories;

public interface IUsuarioRepository : IRepository<Usuario>
{
    Task<Usuario?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<Usuario?> GetWithPerfilAndPermissoesByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<Usuario?> GetWithPerfilAndPermissoesAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ExistsUsernameAsync(string username, int? ignorarId = null, CancellationToken cancellationToken = default);
    Task<int> CountByPerfilIdAsync(int perfilId, CancellationToken cancellationToken = default);
}

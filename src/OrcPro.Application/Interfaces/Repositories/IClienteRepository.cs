using OrcPro.Domain.Entities.Cliente;

namespace OrcPro.Application.Interfaces.Repositories;

public interface IClienteRepository : IRepository<Cliente>
{
    Task<Cliente?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default);
    Task<Cliente?> GetByCpfCnpjAsync(string cpfCnpj, CancellationToken cancellationToken = default);
    Task<bool> ExistsCpfCnpjAsync(string cpfCnpj, int? ignorarId = null, CancellationToken cancellationToken = default);
    Task<string> GerarProximoCodigoAsync(CancellationToken cancellationToken = default);
}

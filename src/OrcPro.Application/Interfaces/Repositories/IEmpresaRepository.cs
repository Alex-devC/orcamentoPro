using OrcPro.Domain.Entities.Empresa;

namespace OrcPro.Application.Interfaces.Repositories;

public interface IEmpresaRepository : IRepository<Empresa>
{
    Task<Empresa?> GetEmitentePrincipalAsync(CancellationToken cancellationToken = default);
}

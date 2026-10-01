using OrcPro.Domain.Entities.Tecnico;

namespace OrcPro.Application.Interfaces.Repositories;

public interface ITecnicoRepository : IRepository<Tecnico>
{
    Task<Tecnico?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default);
    Task<Tecnico?> GetByCpfAsync(string cpf, CancellationToken cancellationToken = default);
    Task<bool> ExistsCodigoAsync(string codigo, int? ignorarId = null, CancellationToken cancellationToken = default);
    Task<bool> ExistsCpfAsync(string cpf, int? ignorarId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Quantos orçamentos usam o técnico (como responsável e/ou mão de obra). Define se o
    /// técnico pode ser excluído ou apenas inativado.
    /// </summary>
    Task<int> CountOrcamentosAsync(int tecnicoId, CancellationToken cancellationToken = default);

    Task<string> GerarProximoCodigoAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tecnico>> GetAllAtivosAsync(CancellationToken cancellationToken = default);
}

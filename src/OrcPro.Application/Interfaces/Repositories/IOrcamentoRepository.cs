using OrcPro.Domain.Entities.Orcamento;

namespace OrcPro.Application.Interfaces.Repositories;

public interface IOrcamentoRepository : IRepository<Orcamento>
{
    Task<Orcamento?> GetWithDetailsByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Orcamento?> GetByNumeroAsync(string numero, CancellationToken cancellationToken = default);
    Task<int> ObterProximoSequencialAsync(int ano, CancellationToken cancellationToken = default);
    Task<bool> ExistsNumeroAsync(string numero, int? ignorarId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Orcamento>> GetByClienteIdAsync(int clienteId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Orcamento>> GetByStatusIdAsync(int statusId, CancellationToken cancellationToken = default);
    Task<int> CountByUsuarioIdAsync(int usuarioId, CancellationToken cancellationToken = default);
    Task<int> CountByClienteIdAsync(int clienteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executa a operação dentro de uma transação: qualquer exceção provoca rollback,
    /// garantindo que cabeçalho + itens + mão de obra + técnicos + histórico não fiquem
    /// gravados pela metade.
    /// </summary>
    Task ExecutarEmTransacaoAsync(Func<Task> operacao, CancellationToken cancellationToken = default);
}

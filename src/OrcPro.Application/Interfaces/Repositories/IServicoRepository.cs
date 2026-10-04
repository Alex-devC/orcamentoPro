using OrcPro.Domain.Entities.Servico;

namespace OrcPro.Application.Interfaces.Repositories;

public interface IServicoRepository : IRepository<Servico>
{
    Task<Servico?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default);
    Task<bool> ExistsCodigoAsync(string codigo, int? ignorarId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Servico>> GetAllAtivosAsync(CancellationToken cancellationToken = default);

    /// <summary>Próximo código sequencial disponível (SRV-0001, SRV-0002, ...).</summary>
    Task<string> GerarProximoCodigoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Quantos itens de orçamento já referenciam este serviço. Enquanto o módulo de
    /// Orçamentos não existir o retorno é sempre zero; o método existe para que a
    /// bloqueio de exclusão já esteja no lugar e não precise ser redesenhado depois.
    /// </summary>
    Task<int> CountVinculosOrcamentoAsync(int servicoId, CancellationToken cancellationToken = default);
}
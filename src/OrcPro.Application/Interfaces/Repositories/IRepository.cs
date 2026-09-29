using System.Linq.Expressions;
using OrcPro.Application.DTOs.Common;
using OrcPro.Domain.Common;

namespace OrcPro.Application.Interfaces.Repositories;

public interface IRepository<TEntity> where TEntity : BaseEntity
{
    Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    Task<PagedResult<TEntity>> GetPagedAsync(PagedRequest request, CancellationToken cancellationToken = default);
    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
}

using System.Linq.Expressions;
using OrcPro.Application.DTOs.Common;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace OrcPro.Infrastructure.Persistence.Repositories;

public class BaseRepository<TEntity> : IRepository<TEntity> where TEntity : BaseEntity
{
    protected readonly OrcProDbContext Context;
    protected readonly DbSet<TEntity> DbSet;

    public BaseRepository(OrcProDbContext context)
    {
        Context = context;
        DbSet = context.Set<TEntity>();
    }

    public virtual async Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await DbSet.FindAsync(new object[] { id }, cancellationToken);
    }

    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking().ToListAsync(cancellationToken);
    }

    public virtual async Task<IReadOnlyList<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking().Where(predicate).ToListAsync(cancellationToken);
    }

    public virtual async Task<PagedResult<TEntity>> GetPagedAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        IQueryable<TEntity> query = DbSet.AsNoTracking();

        query = ApplyCustomFilters(query, request);

        var totalCount = await query.CountAsync(cancellationToken);

        query = ApplySorting(query, request);

        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TEntity>(items, totalCount, request.PageNumber, request.PageSize);
    }

    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await DbSet.AddAsync(entity, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public virtual async Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        Context.Entry(entity).State = EntityState.Modified;
        await Context.SaveChangesAsync(cancellationToken);
    }

    public virtual async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await DbSet.FindAsync(new object[] { id }, cancellationToken);
        if (entity != null)
        {
            DbSet.Remove(entity);
            await Context.SaveChangesAsync(cancellationToken);
        }
    }

    public virtual async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(e => e.Id == id, cancellationToken);
    }

    protected virtual IQueryable<TEntity> ApplyCustomFilters(IQueryable<TEntity> query, PagedRequest request)
    {
        return query;
    }

    protected virtual IQueryable<TEntity> ApplySorting(IQueryable<TEntity> query, PagedRequest request)
    {
        if (request.Sorts.Count > 0)
        {
            var firstSort = request.Sorts[0];
            return firstSort.IsDescending 
                ? query.OrderByDescending(e => EF.Property<object>(e, firstSort.PropertyName))
                : query.OrderBy(e => EF.Property<object>(e, firstSort.PropertyName));
        }

        return query.OrderByDescending(e => e.Id);
    }
}

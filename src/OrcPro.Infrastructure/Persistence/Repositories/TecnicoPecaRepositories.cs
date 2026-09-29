using OrcPro.Application.DTOs.Common;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Domain.Entities.Peca;
using OrcPro.Domain.Entities.Tecnico;
using Microsoft.EntityFrameworkCore;

namespace OrcPro.Infrastructure.Persistence.Repositories;

public class TecnicoRepository : BaseRepository<Tecnico>, ITecnicoRepository
{
    public TecnicoRepository(OrcProDbContext context) : base(context) { }

    public async Task<Tecnico?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Codigo == codigo, cancellationToken);
    }

    public async Task<Tecnico?> GetByCpfAsync(string cpf, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Cpf == cpf, cancellationToken);
    }

    public async Task<bool> ExistsCodigoAsync(string codigo, int? ignorarId = null, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(t => t.Codigo == codigo && (!ignorarId.HasValue || t.Id != ignorarId.Value), cancellationToken);
    }

    public async Task<string> GerarProximoCodigoAsync(CancellationToken cancellationToken = default)
    {
        var total = await DbSet.CountAsync(cancellationToken);
        return $"TEC-{total + 1:D3}";
    }

    public async Task<IReadOnlyList<Tecnico>> GetAllAtivosAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(t => t.Ativo)
            .OrderBy(t => t.Nome)
            .ToListAsync(cancellationToken);
    }

    protected override IQueryable<Tecnico> ApplyCustomFilters(IQueryable<Tecnico> query, PagedRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(t => 
                t.Codigo.ToLower().Contains(term) ||
                t.Nome.ToLower().Contains(term) ||
                (t.Cpf != null && t.Cpf.Contains(term)) ||
                (t.Especialidade != null && t.Especialidade.ToLower().Contains(term)) ||
                (t.Email != null && t.Email.ToLower().Contains(term)));
        }

        foreach (var filter in request.Filters)
        {
            if (filter.PropertyName.Equals("Status", StringComparison.OrdinalIgnoreCase))
            {
                if (filter.Value.Equals("ativos", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(t => t.Ativo);
                else if (filter.Value.Equals("inativos", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(t => !t.Ativo);
            }
            else if (filter.PropertyName.Equals("Especialidade", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(filter.Value))
            {
                query = query.Where(t => t.Especialidade != null && t.Especialidade.ToLower().Contains(filter.Value.ToLower()));
            }
        }

        return query;
    }
}

public class PecaRepository : BaseRepository<Peca>, IPecaRepository
{
    public PecaRepository(OrcProDbContext context) : base(context) { }

    public async Task<Peca?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Codigo == codigo, cancellationToken);
    }

    public async Task<bool> ExistsCodigoAsync(string codigo, int? ignorarId = null, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(p => p.Codigo == codigo && (!ignorarId.HasValue || p.Id != ignorarId.Value), cancellationToken);
    }

    public async Task<IReadOnlyList<Peca>> GetAllAtivosAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(p => p.Ativo)
            .OrderBy(p => p.Descricao)
            .ToListAsync(cancellationToken);
    }

    protected override IQueryable<Peca> ApplyCustomFilters(IQueryable<Peca> query, PagedRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(p => 
                p.Codigo.ToLower().Contains(term) || 
                p.Descricao.ToLower().Contains(term));
        }

        return query;
    }
}

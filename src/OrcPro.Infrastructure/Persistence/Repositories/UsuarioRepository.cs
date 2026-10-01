using OrcPro.Application.DTOs.Common;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Domain.Entities.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace OrcPro.Infrastructure.Persistence.Repositories;

public class UsuarioRepository : BaseRepository<Usuario>, IUsuarioRepository
{
    public UsuarioRepository(OrcProDbContext context) : base(context) { }

    public async Task<Usuario?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);
    }

    public async Task<Usuario?> GetWithPerfilAndPermissoesByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(u => u.Perfil)
                .ThenInclude(p => p!.PerfilPermissoes)
                    .ThenInclude(pp => pp.Permissao)
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);
    }

    public async Task<Usuario?> GetWithPerfilAndPermissoesAsync(int id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(u => u.Perfil)
                .ThenInclude(p => p!.PerfilPermissoes)
                    .ThenInclude(pp => pp.Permissao)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsUsernameAsync(string username, int? ignorarId = null, CancellationToken cancellationToken = default)
    {
        // Comparação sem diferenciar maiúsculas/minúsculas: evita dois usuários lógicos
        // ("Admin" e "admin") apontando para o mesmo login.
        var normalizado = username.Trim().ToLower();
        return await DbSet.AnyAsync(u => u.Username.ToLower() == normalizado && (!ignorarId.HasValue || u.Id != ignorarId.Value), cancellationToken);
    }

    public Task<int> CountByPerfilIdAsync(int perfilId, CancellationToken cancellationToken = default)
        => DbSet.CountAsync(u => u.PerfilId == perfilId, cancellationToken);

    protected override IQueryable<Usuario> ApplyCustomFilters(IQueryable<Usuario> query, PagedRequest request)
    {
        query = query.Include(u => u.Perfil);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(u => u.Username.ToLower().Contains(term) || u.NomeCompleto.ToLower().Contains(term) || (u.Email != null && u.Email.ToLower().Contains(term)));
        }

        // Filtros da tela de Usuários (situação e perfil) chegam como FilterRequest.
        foreach (var filtro in request.Filters)
        {
            if (string.Equals(filtro.PropertyName, "Ativo", StringComparison.OrdinalIgnoreCase)
                && bool.TryParse(filtro.Value, out var ativo))
            {
                query = query.Where(u => u.Ativo == ativo);
            }
            else if (string.Equals(filtro.PropertyName, "PerfilId", StringComparison.OrdinalIgnoreCase)
                     && int.TryParse(filtro.Value, out var perfilId))
            {
                query = query.Where(u => u.PerfilId == perfilId);
            }
        }

        return query;
    }
}

public class PermissaoRepository : BaseRepository<Permissao>, IPermissaoRepository
{
    public PermissaoRepository(OrcProDbContext context) : base(context) { }
}

public class PerfilRepository : BaseRepository<Perfil>, IPerfilRepository
{
    public PerfilRepository(OrcProDbContext context) : base(context) { }

    public async Task<Perfil?> GetWithPermissoesAsync(int id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(p => p.PerfilPermissoes)
                .ThenInclude(pp => pp.Permissao)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Perfil>> GetAllAtivosAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(p => p.Ativo)
            .OrderBy(p => p.Nome)
            .ToListAsync(cancellationToken);
    }

    protected override IQueryable<Perfil> ApplyCustomFilters(IQueryable<Perfil> query, PagedRequest request)
    {
        // As permissões entram no grid apenas para contagem/exibição futura do módulo de permissões.
        query = query.Include(p => p.PerfilPermissoes)
            .ThenInclude(pp => pp.Permissao);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(p => p.Nome.ToLower().Contains(term) || (p.Descricao != null && p.Descricao.ToLower().Contains(term)));
        }

        foreach (var filtro in request.Filters)
        {
            if (string.Equals(filtro.PropertyName, "Ativo", StringComparison.OrdinalIgnoreCase)
                && bool.TryParse(filtro.Value, out var ativo))
            {
                query = query.Where(p => p.Ativo == ativo);
            }
        }

        return query;
    }
}

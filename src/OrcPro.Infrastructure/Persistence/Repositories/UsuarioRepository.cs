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
        return await DbSet.AnyAsync(u => u.Username == username && (!ignorarId.HasValue || u.Id != ignorarId.Value), cancellationToken);
    }

    protected override IQueryable<Usuario> ApplyCustomFilters(IQueryable<Usuario> query, PagedRequest request)
    {
        query = query.Include(u => u.Perfil);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(u => u.Username.ToLower().Contains(term) || u.NomeCompleto.ToLower().Contains(term) || (u.Email != null && u.Email.ToLower().Contains(term)));
        }

        return query;
    }
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
}

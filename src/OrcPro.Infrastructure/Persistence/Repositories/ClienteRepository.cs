using OrcPro.Application.DTOs.Common;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Domain.Entities.Cliente;
using OrcPro.Domain.Entities.Empresa;
using Microsoft.EntityFrameworkCore;

namespace OrcPro.Infrastructure.Persistence.Repositories;

public class EmpresaRepository : BaseRepository<Empresa>, IEmpresaRepository
{
    public EmpresaRepository(OrcProDbContext context) : base(context) { }

    /// <summary>
    /// Emitente da instalação. Existe no máximo um registro; o mais antigo vence para que
    /// uma eventual duplicidade legada não troque os dados do emitente já em uso.
    /// </summary>
    public async Task<Empresa?> GetEmitentePrincipalAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .OrderBy(e => e.Id)
            .FirstOrDefaultAsync(e => e.Ativo, cancellationToken);
    }
}

public class ClienteRepository : BaseRepository<Cliente>, IClienteRepository
{
    public ClienteRepository(OrcProDbContext context) : base(context) { }

    public async Task<Cliente?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Codigo == codigo, cancellationToken);
    }

    public async Task<Cliente?> GetByCpfCnpjAsync(string cpfCnpj, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CpfCnpj == cpfCnpj, cancellationToken);
    }

    public async Task<bool> ExistsCpfCnpjAsync(string cpfCnpj, int? ignorarId = null, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(c => c.CpfCnpj == cpfCnpj && (!ignorarId.HasValue || c.Id != ignorarId.Value), cancellationToken);
    }

    public async Task<string> GerarProximoCodigoAsync(CancellationToken cancellationToken = default)
    {
        var total = await DbSet.CountAsync(cancellationToken);
        return $"CLI-{total + 1:D5}";
    }

    protected override IQueryable<Cliente> ApplyCustomFilters(IQueryable<Cliente> query, PagedRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(c => 
                c.Codigo.ToLower().Contains(term) ||
                c.NomeRazaoSocial.ToLower().Contains(term) ||
                (c.NomeFantasia != null && c.NomeFantasia.ToLower().Contains(term)) ||
                (c.CpfCnpj != null && c.CpfCnpj.Contains(term)) ||
                (c.Cidade != null && c.Cidade.ToLower().Contains(term)) ||
                c.Celular.Contains(term));
        }

        foreach (var filter in request.Filters)
        {
            if (filter.PropertyName.Equals("Status", StringComparison.OrdinalIgnoreCase))
            {
                if (filter.Value.Equals("ativos", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(c => c.Ativo);
                else if (filter.Value.Equals("inativos", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(c => !c.Ativo);
            }
            else if (filter.PropertyName.Equals("TipoPessoa", StringComparison.OrdinalIgnoreCase) && !filter.Value.Equals("todos", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(c => c.TipoPessoa == filter.Value);
            }
            else if (filter.PropertyName.Equals("Cidade", StringComparison.OrdinalIgnoreCase) && !filter.Value.Equals("todas", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(c => c.Cidade != null && c.Cidade.ToLower().Contains(filter.Value.ToLower()));
            }
        }

        return query;
    }
}

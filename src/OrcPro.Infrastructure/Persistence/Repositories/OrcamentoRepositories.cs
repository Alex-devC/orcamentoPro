using OrcPro.Application.DTOs.Common;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Domain.Entities.Orcamento;
using Microsoft.EntityFrameworkCore;

namespace OrcPro.Infrastructure.Persistence.Repositories;

public class OrcamentoRepository : BaseRepository<Orcamento>, IOrcamentoRepository
{
    public OrcamentoRepository(OrcProDbContext context) : base(context) { }

    public async Task<Orcamento?> GetWithDetailsByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(o => o.Cliente)
            .Include(o => o.Empresa)
            .Include(o => o.Usuario)
            .Include(o => o.Status)
            .Include(o => o.Itens)
                .ThenInclude(i => i.Peca)
            .Include(o => o.MaosDeObra)
                .ThenInclude(m => m.Tecnicos)
                    .ThenInclude(mot => mot.Tecnico)
            .Include(o => o.Tecnicos)
                .ThenInclude(ot => ot.Tecnico)
            .Include(o => o.Historicos)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<Orcamento?> GetByNumeroAsync(string numero, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(o => o.Cliente)
            .Include(o => o.Empresa)
            .Include(o => o.Usuario)
            .Include(o => o.Status)
            .Include(o => o.Itens)
            .Include(o => o.MaosDeObra)
                .ThenInclude(m => m.Tecnicos)
                    .ThenInclude(mot => mot.Tecnico)
            .Include(o => o.Tecnicos)
                .ThenInclude(ot => ot.Tecnico)
            .Include(o => o.Historicos)
            .FirstOrDefaultAsync(o => o.Numero == numero, cancellationToken);
    }

    public async Task<int> ObterProximoSequencialAsync(int ano, CancellationToken cancellationToken = default)
    {
        var maior = await DbSet
            .Where(o => o.Ano == ano)
            .Select(o => (int?)o.Sequencial)
            .MaxAsync(cancellationToken);

        return (maior ?? 0) + 1;
    }

    public async Task<bool> ExistsNumeroAsync(string numero, int? ignorarId = null, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(o => o.Numero == numero && (!ignorarId.HasValue || o.Id != ignorarId.Value), cancellationToken);
    }

    public async Task<IReadOnlyList<Orcamento>> GetByClienteIdAsync(int clienteId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(o => o.Status)
            .Where(o => o.ClienteId == clienteId)
            .OrderByDescending(o => o.DataEmissao)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Orcamento>> GetByStatusIdAsync(int statusId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(o => o.Cliente)
            .Include(o => o.Status)
            .Where(o => o.StatusId == statusId)
            .OrderByDescending(o => o.DataEmissao)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Quantos orçamentos pertencem ao usuário (guarda de exclusão no cadastro de usuários).</summary>
    public Task<int> CountByUsuarioIdAsync(int usuarioId, CancellationToken cancellationToken = default)
        => DbSet.CountAsync(o => o.UsuarioId == usuarioId, cancellationToken);

    /// <summary>Quantos orçamentos pertencem ao cliente (guarda de exclusão no cadastro de clientes).</summary>
    public Task<int> CountByClienteIdAsync(int clienteId, CancellationToken cancellationToken = default)
        => DbSet.CountAsync(o => o.ClienteId == clienteId, cancellationToken);

    /// <summary>
    /// Executa a operação em uma transação. Qualquer exceção provoca rollback, garantindo
    /// que cabeçalho + itens + mão de obra + técnicos + histórico não fiquem pela metade.
    /// </summary>
    public async Task ExecutarEmTransacaoAsync(Func<Task> operacao, CancellationToken cancellationToken = default)
    {
        // SQLite (e os demais providers suportados) exigem transação explícita.
        var transacao = await Context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await operacao();
            await transacao.CommitAsync(cancellationToken);
        }
        catch
        {
            await transacao.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            await transacao.DisposeAsync();
        }
    }

    protected override IQueryable<Orcamento> ApplyCustomFilters(IQueryable<Orcamento> query, PagedRequest request)
    {
        query = query
            .Include(o => o.Cliente)
            .Include(o => o.Status)
            .Include(o => o.Usuario);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(o => 
                o.Numero.ToLower().Contains(term) ||
                (o.Cliente != null && o.Cliente.NomeRazaoSocial.ToLower().Contains(term)) ||
                (o.Cliente != null && o.Cliente.CpfCnpj != null && o.Cliente.CpfCnpj.Contains(term)) ||
                (o.Status != null && o.Status.Nome.ToLower().Contains(term)));
        }

        foreach (var filter in request.Filters)
        {
            if (filter.PropertyName.Equals("StatusId", StringComparison.OrdinalIgnoreCase) && int.TryParse(filter.Value, out var statusId))
            {
                query = query.Where(o => o.StatusId == statusId);
            }
            else if (filter.PropertyName.Equals("ClienteId", StringComparison.OrdinalIgnoreCase) && int.TryParse(filter.Value, out var clienteId))
            {
                query = query.Where(o => o.ClienteId == clienteId);
            }
            else if (filter.PropertyName.Equals("Ano", StringComparison.OrdinalIgnoreCase) && int.TryParse(filter.Value, out var ano))
            {
                query = query.Where(o => o.Ano == ano);
            }
            else if (filter.PropertyName.Equals("DataInicial", StringComparison.OrdinalIgnoreCase)
                && DateTime.TryParse(filter.Value, out var dataInicial))
            {
                query = query.Where(o => o.DataEmissao >= dataInicial);
            }
            else if (filter.PropertyName.Equals("DataFinal", StringComparison.OrdinalIgnoreCase)
                && DateTime.TryParse(filter.Value, out var dataFinal))
            {
                // Fim do dia informado: o filtro por período é inclusivo.
                var fim = dataFinal.Date.AddDays(1).AddTicks(-1);
                query = query.Where(o => o.DataEmissao <= fim);
            }
        }

        return query;
    }
}

public class OrcamentoStatusRepository : BaseRepository<OrcamentoStatus>, IOrcamentoStatusRepository
{
    public OrcamentoStatusRepository(OrcProDbContext context) : base(context) { }

    /// <summary>
    /// Busca um status pelo código.
    ///
    /// <para>RETORNO RASTREADO de propósito: quando o status é atribuído à navegação de um
    /// orçamento ainda não gravado, um objeto desacoplado faria o EF tentar INSERT de uma
    /// linha de status já existente ("UNIQUE constraint failed: OrcamentoStatus.Id").</para>
    /// </summary>
    public async Task<OrcamentoStatus?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(s => s.Codigo == codigo, cancellationToken);
    }

    public async Task<IReadOnlyList<OrcamentoStatus>> GetAllAtivosAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(s => s.Ativo)
            .OrderBy(s => s.Ordem)
            .ToListAsync(cancellationToken);
    }
}

public class OrcamentoHistoricoRepository : BaseRepository<OrcamentoHistorico>, IOrcamentoHistoricoRepository
{
    public OrcamentoHistoricoRepository(OrcProDbContext context) : base(context) { }

    public async Task<IReadOnlyList<OrcamentoHistorico>> GetByOrcamentoIdAsync(int orcamentoId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(h => h.OrcamentoId == orcamentoId)
            .OrderByDescending(h => h.DataRegistro)
            .ToListAsync(cancellationToken);
    }
}

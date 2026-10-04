using OrcPro.Application.DTOs.Common;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Domain.Entities.Servico;
using Microsoft.EntityFrameworkCore;

namespace OrcPro.Infrastructure.Persistence.Repositories;

public class ServicoRepository : BaseRepository<Servico>, IServicoRepository
{
    /// <summary>Prefixo dos códigos gerados automaticamente.</summary>
    private const string PrefixoCodigo = "SRV-";

    /// <summary>Largura da parte numérica do código (SRV-0001).</summary>
    private const int CasasNumero = 4;

    public ServicoRepository(OrcProDbContext context) : base(context) { }

    public async Task<Servico?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Codigo == codigo, cancellationToken);
    }

    public async Task<bool> ExistsCodigoAsync(string codigo, int? ignorarId = null, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(s => s.Codigo == codigo && (!ignorarId.HasValue || s.Id != ignorarId.Value), cancellationToken);
    }

    /// <summary>
    /// Gera o próximo código sequencial seguro no formato SRV-0001.
    ///
    /// <para>Não usa "quantidade de registros + 1": consultas a quantidade quebram a
    /// sequência sempre que houver exclusões ou lacunas. Em vez disso, examina os
    /// <b>maiores</b> códigos já existentes (inclusive inativos) e avança a partir do
    /// maior número encontrado. Códigos não numéricos ou de outro prefixo são ignorados.</para>
    ///
    /// <para><b>Garantia:</b> excluir um registro no meio da sequência nunca faz o código
    /// dele ser oferecido de novo, porque o próximo continua acima do maior código
    /// existente. Esta é exatamente a mesma regra de Peças e Técnicos. Observação: excluir
    /// o registro com o <b>maior</b> código existente faz o número voltar a ficar disponível,
    /// pois não há registro que o comprove — se isso for unacceptable no futuro, o
    /// contador precisa de uma tabela de sequências própria.</para>
    /// </summary>
    public async Task<string> GerarProximoCodigoAsync(CancellationToken cancellationToken = default)
    {
        // Considera todos os registros (ativos e inativos): código nunca é reaproveitado.
        var codigos = await DbSet
            .AsNoTracking()
            .Select(s => s.Codigo)
            .ToListAsync(cancellationToken);

        var maiorNumero = 0;

        foreach (var codigo in codigos)
        {
            if (string.IsNullOrWhiteSpace(codigo))
                continue;

            var texto = codigo.Trim();

            if (texto.StartsWith(PrefixoCodigo, StringComparison.OrdinalIgnoreCase))
                texto = texto[PrefixoCodigo.Length..];

            if (int.TryParse(texto, out var numero) && numero > maiorNumero)
                maiorNumero = numero;
        }

        // Zeros à ESQUERDA da parte numérica: "SRV-" + "0001" => "SRV-0001".
        return $"{PrefixoCodigo}{(maiorNumero + 1).ToString().PadLeft(CasasNumero, '0')}";
    }

    public async Task<IReadOnlyList<Servico>> GetAllAtivosAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(s => s.Ativo)
            .OrderBy(s => s.Descricao)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Quantas linhas de orçamento usam este serviço.
    ///
    /// <para>O módulo de Orçamentos ainda não existe, então nenhum vínculo é possível e o
    /// retorno é zero — a exclusão permanece liberada. Quando os itens de serviço do
    /// orçamento surgirem, basta contar aqui (o mesmo caminho usado por
    /// <see cref="TecnicoRepository.CountOrcamentosAsync"/>) e o bloqueio de exclusão já
    /// aplicado pelo <c>ServicoService</c> passa a valer sem nova alteração de contrato.</para>
    /// </summary>
    public Task<int> CountVinculosOrcamentoAsync(int servicoId, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    protected override IQueryable<Servico> ApplyCustomFilters(IQueryable<Servico> query, PagedRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(s =>
                s.Codigo.ToLower().Contains(term) ||
                s.Descricao.ToLower().Contains(term) ||
                (s.Categoria != null && s.Categoria.ToLower().Contains(term)));
        }

        foreach (var filter in request.Filters)
        {
            if (filter.PropertyName.Equals("Status", StringComparison.OrdinalIgnoreCase))
            {
                if (filter.Value.Equals("ativos", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(s => s.Ativo);
                else if (filter.Value.Equals("inativos", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(s => !s.Ativo);
            }
        }

        return query;
    }
}
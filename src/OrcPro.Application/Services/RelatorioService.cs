using OrcPro.Application.DTOs.Relatorio;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Entities.Orcamento;

namespace OrcPro.Application.Services;

public class RelatorioService : IRelatorioService
{
    private readonly IOrcamentoRepository _orcamentoRepository;
    private readonly IOrcamentoStatusRepository _statusRepository;
    private readonly ITecnicoRepository _tecnicoRepository;

    public RelatorioService(
        IOrcamentoRepository orcamentoRepository,
        IOrcamentoStatusRepository statusRepository,
        ITecnicoRepository tecnicoRepository)
    {
        _orcamentoRepository = orcamentoRepository;
        _statusRepository = statusRepository;
        _tecnicoRepository = tecnicoRepository;
    }

    public async Task<RelatorioVendasPeriodoDto> ObterRelatorioVendasAsync(RelatorioFiltroDto filtro, CancellationToken cancellationToken = default)
    {
        var dataInicio = filtro.DataInicio ?? DateTime.UtcNow.AddDays(-30);
        var dataFim = filtro.DataFim ?? DateTime.UtcNow;

        var todosOrcamentos = await _orcamentoRepository.FindAsync(
            o => o.DataEmissao >= dataInicio && o.DataEmissao <= dataFim,
            cancellationToken);

        if (filtro.ClienteId.HasValue)
            todosOrcamentos = todosOrcamentos.Where(o => o.ClienteId == filtro.ClienteId.Value).ToList();

        if (filtro.UsuarioId.HasValue)
            todosOrcamentos = todosOrcamentos.Where(o => o.UsuarioId == filtro.UsuarioId.Value).ToList();

        var statuses = await _statusRepository.GetAllAtivosAsync(cancellationToken);
        var statusAprovado = statuses.FirstOrDefault(s => s.Codigo == OrcamentoStatus.CodigoAprovado || s.Codigo == OrcamentoStatus.CodigoFinalizado);

        var totalEmitidos = todosOrcamentos.Count;
        var orcamentosAprovados = todosOrcamentos
            .Where(o => o.StatusId == (statusAprovado?.Id ?? -1) ||
                        o.Status?.Codigo == OrcamentoStatus.CodigoAprovado ||
                        o.Status?.Codigo == OrcamentoStatus.CodigoFinalizado)
            .ToList();

        var totalAprovados = orcamentosAprovados.Count;
        var valorTotalAprovado = orcamentosAprovados.Sum(o => o.ValorTotal);
        var taxaConversao = totalEmitidos > 0 ? (decimal)totalAprovados / totalEmitidos * 100m : 0m;

        var statusResumo = await ObterOrcamentosPorStatusAsync(filtro, cancellationToken);

        return new RelatorioVendasPeriodoDto
        {
            DataInicio = dataInicio,
            DataFim = dataFim,
            TotalOrcamentosEmitidos = totalEmitidos,
            TotalAprovados = totalAprovados,
            ValorTotalAprovado = valorTotalAprovado,
            TaxaConversaoPercentual = Math.Round(taxaConversao, 2),
            StatusResumo = statusResumo.ToList()
        };
    }

    public async Task<IReadOnlyList<RelatorioOrcamentosPorStatusDto>> ObterOrcamentosPorStatusAsync(RelatorioFiltroDto filtro, CancellationToken cancellationToken = default)
    {
        var dataInicio = filtro.DataInicio ?? DateTime.UtcNow.AddDays(-30);
        var dataFim = filtro.DataFim ?? DateTime.UtcNow;

        var orcamentos = await _orcamentoRepository.FindAsync(
            o => o.DataEmissao >= dataInicio && o.DataEmissao <= dataFim,
            cancellationToken);

        if (filtro.ClienteId.HasValue)
            orcamentos = orcamentos.Where(o => o.ClienteId == filtro.ClienteId.Value).ToList();

        var statuses = await _statusRepository.GetAllAtivosAsync(cancellationToken);
        var totalGeral = orcamentos.Count;

        var lista = new List<RelatorioOrcamentosPorStatusDto>();
        foreach (var status in statuses.OrderBy(s => s.Ordem))
        {
            var orcsDoStatus = orcamentos.Where(o => o.StatusId == status.Id).ToList();
            var qtd = orcsDoStatus.Count;
            var valor = orcsDoStatus.Sum(o => o.ValorTotal);
            var pct = totalGeral > 0 ? Math.Round((decimal)qtd / totalGeral * 100m, 2) : 0m;

            lista.Add(new RelatorioOrcamentosPorStatusDto
            {
                StatusId = status.Id,
                StatusNome = status.Nome,
                CorHex = status.CorHex,
                Quantidade = qtd,
                ValorTotal = valor,
                Percentual = pct
            });
        }

        return lista;
    }

    public async Task<IReadOnlyList<RelatorioDesempenhoTecnicoDto>> ObterDesempenhoTecnicosAsync(RelatorioFiltroDto filtro, CancellationToken cancellationToken = default)
    {
        var dataInicio = filtro.DataInicio ?? DateTime.UtcNow.AddDays(-30);
        var dataFim = filtro.DataFim ?? DateTime.UtcNow;

        var orcamentos = await _orcamentoRepository.FindAsync(
            o => o.DataEmissao >= dataInicio && o.DataEmissao <= dataFim,
            cancellationToken);

        var tecnicos = await _tecnicoRepository.GetAllAtivosAsync(cancellationToken);
        if (filtro.TecnicoId.HasValue)
            tecnicos = tecnicos.Where(t => t.Id == filtro.TecnicoId.Value).ToList();

        var resultado = new List<RelatorioDesempenhoTecnicoDto>();

        foreach (var tec in tecnicos)
        {
            var orcsDoTecnico = orcamentos.Where(o => o.Tecnicos.Any(ot => ot.TecnicoId == tec.Id)).ToList();
            var maosDeObraDoTecnico = orcsDoTecnico
                .SelectMany(o => o.MaosDeObra)
                .Where(m => m.Tecnicos.Any(mot => mot.TecnicoId == tec.Id))
                .ToList();

            var horas = maosDeObraDoTecnico.Sum(m => m.QuantidadeHoras);
            var valorMaoDeObra = maosDeObraDoTecnico.Sum(m => m.ValorTotal);

            resultado.Add(new RelatorioDesempenhoTecnicoDto
            {
                TecnicoId = tec.Id,
                Codigo = tec.Codigo,
                Nome = tec.Nome,
                Especialidade = tec.Especialidade,
                QuantidadeOrcamentos = orcsDoTecnico.Count,
                TotalHorasTrabalhadas = horas,
                ValorTotalMaoDeObra = valorMaoDeObra
            });
        }

        return resultado;
    }
}

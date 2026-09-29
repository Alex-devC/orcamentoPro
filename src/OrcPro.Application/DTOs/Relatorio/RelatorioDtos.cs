namespace OrcPro.Application.DTOs.Relatorio;

public class RelatorioFiltroDto
{
    public DateTime? DataInicio { get; set; }
    public DateTime? DataFim { get; set; }
    public int? ClienteId { get; set; }
    public int? StatusId { get; set; }
    public int? TecnicoId { get; set; }
    public int? UsuarioId { get; set; }
}

public class RelatorioOrcamentosPorStatusDto
{
    public int StatusId { get; set; }
    public string StatusNome { get; set; } = string.Empty;
    public string? CorHex { get; set; }
    public int Quantidade { get; set; }
    public decimal ValorTotal { get; set; }
    public decimal Percentual { get; set; }
}

public class RelatorioVendasPeriodoDto
{
    public DateTime DataInicio { get; set; }
    public DateTime DataFim { get; set; }
    public int TotalOrcamentosEmitidos { get; set; }
    public int TotalAprovados { get; set; }
    public decimal ValorTotalAprovado { get; set; }
    public decimal TaxaConversaoPercentual { get; set; }
    public List<RelatorioOrcamentosPorStatusDto> StatusResumo { get; set; } = new();
}

public class RelatorioDesempenhoTecnicoDto
{
    public int TecnicoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Especialidade { get; set; }
    public int QuantidadeOrcamentos { get; set; }
    public decimal TotalHorasTrabalhadas { get; set; }
    public decimal ValorTotalMaoDeObra { get; set; }
}

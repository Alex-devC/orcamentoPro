namespace OrcPro.Application.DTOs.Servico;

/// <summary>
/// Ficha de um serviço/mão de obra. Estes são exatamente os dados que o Orçamento irá
/// consumir futuramente ao selecionar um serviço cadastrado.
/// </summary>
public class ServicoDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    public decimal Valor { get; set; }
    public string Unidade { get; set; } = "UN";
    public decimal TempoEstimado { get; set; }
    public string? Observacoes { get; set; }
    public bool Ativo { get; set; }
    public DateTime DataCriacao { get; set; }
}

public class CriarServicoDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    public decimal Valor { get; set; }
    public string Unidade { get; set; } = "UN";
    public decimal TempoEstimado { get; set; } = 1m;
    public string? Observacoes { get; set; }
    public bool Ativo { get; set; } = true;
}

public class AtualizarServicoDto : CriarServicoDto
{
    public int Id { get; set; }
}
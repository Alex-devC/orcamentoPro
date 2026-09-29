namespace OrcPro.Application.DTOs.Peca;

public class PecaDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string UnidadeMedida { get; set; } = "UN";
    public decimal PrecoCusto { get; set; }
    public decimal PrecoVenda { get; set; }
    public decimal EstoqueAtual { get; set; }
    public decimal EstoqueMinimo { get; set; }
    public string? Observacoes { get; set; }
    public bool Ativo { get; set; }
    public DateTime DataCriacao { get; set; }
}

public class CriarPecaDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string UnidadeMedida { get; set; } = "UN";
    public decimal PrecoCusto { get; set; }
    public decimal PrecoVenda { get; set; }
    public decimal EstoqueAtual { get; set; }
    public decimal EstoqueMinimo { get; set; }
    public string? Observacoes { get; set; }
    public bool Ativo { get; set; } = true;
}

public class AtualizarPecaDto : CriarPecaDto
{
    public int Id { get; set; }
}

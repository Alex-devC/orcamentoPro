using OrcPro.Domain.Common;

namespace OrcPro.Domain.Entities.Orcamento;

public class OrcamentoMaoDeObra : BaseEntity
{
    public int OrcamentoId { get; set; }
    public Orcamento? Orcamento { get; set; }

    public int NumeroItem { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal QuantidadeHoras { get; set; } = 1;

    // O valor da mão de obra pertence à linha do orçamento, não ao cadastro global de técnico
    public decimal ValorUnitario { get; set; }
    public decimal ValorDesconto { get; set; }
    public decimal ValorTotal { get; set; }

    public string? Observacoes { get; set; }

    // Uma linha de mão de obra pode envolver múltiplos técnicos na execução
    public ICollection<OrcamentoMaoDeObraTecnico> Tecnicos { get; set; } = new List<OrcamentoMaoDeObraTecnico>();

    public decimal CalcularTotal()
    {
        var bruto = QuantidadeHoras * ValorUnitario;
        ValorTotal = bruto >= ValorDesconto ? bruto - ValorDesconto : 0m;
        return ValorTotal;
    }
}

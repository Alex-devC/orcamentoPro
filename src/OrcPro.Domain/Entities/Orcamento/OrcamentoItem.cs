using OrcPro.Domain.Common;
using OrcPro.Domain.Entities.Peca;

namespace OrcPro.Domain.Entities.Orcamento;

public class OrcamentoItem : BaseEntity
{
    public int OrcamentoId { get; set; }
    public Orcamento? Orcamento { get; set; }

    public int? PecaId { get; set; }
    public Peca.Peca? Peca { get; set; }

    public int NumeroItem { get; set; }
    public string CodigoPeca { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string UnidadeMedida { get; set; } = "UN";

    public decimal Quantidade { get; set; } = 1;
    
    // O preço unitário é registrado no item para preservar o valor histórico do orçamento
    public decimal PrecoUnitario { get; set; }
    public decimal ValorDesconto { get; set; }
    public decimal ValorTotal { get; set; }

    public decimal CalcularTotal()
    {
        ValorTotal = Common.Calculos.OrcamentoCalculo.TotalItem(Quantidade, PrecoUnitario, ValorDesconto);
        return ValorTotal;
    }
}

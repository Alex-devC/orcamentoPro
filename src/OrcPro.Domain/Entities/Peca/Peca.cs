using OrcPro.Domain.Common;
using OrcPro.Domain.Entities.Orcamento;

namespace OrcPro.Domain.Entities.Peca;

public class Peca : BaseEntity
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

    public string? Categoria { get; set; }
    public string? Marca { get; set; }
    public string? Modelo { get; set; }
    public string? CodigoBarras { get; set; }

    public ICollection<OrcamentoItem> OrcamentoItens { get; set; } = new List<OrcamentoItem>();
}

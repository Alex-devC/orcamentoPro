using OrcPro.Domain.Common;
using OrcPro.Domain.Entities.Cliente;
using OrcPro.Domain.Entities.Empresa;
using OrcPro.Domain.Entities.Seguranca;

namespace OrcPro.Domain.Entities.Orcamento;

public class Orcamento : BaseEntity
{
    public string Numero { get; set; } = string.Empty;
    public int Ano { get; set; } = DateTime.UtcNow.Year;
    public int Sequencial { get; set; }

    public DateTime DataEmissao { get; set; } = DateTime.UtcNow;
    public int DiasValidade { get; set; } = 15;
    public DateTime DataValidade { get; set; } = DateTime.UtcNow.AddDays(15);

    // Relacionamentos principais
    public int ClienteId { get; set; }
    public Cliente.Cliente? Cliente { get; set; }

    public int EmpresaId { get; set; }
    public Empresa.Empresa? Empresa { get; set; }

    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public int StatusId { get; set; }
    public OrcamentoStatus? Status { get; set; }

    // Totais financeiros calculados
    public decimal ValorTotalItens { get; set; }
    public decimal ValorTotalMaoDeObra { get; set; }
    public decimal ValorDesconto { get; set; }
    public decimal ValorAcrescimo { get; set; }
    public decimal ValorTotal { get; set; }

    // Condições comerciais
    public string? CondicoesPagamento { get; set; }
    public string? PrazoEntrega { get; set; }
    public string? Garantia { get; set; }
    public string? Observacoes { get; set; }
    public string? ObservacoesInternas { get; set; }

    // Coleções sem restrições artificiais de quantidade
    public ICollection<OrcamentoItem> Itens { get; set; } = new List<OrcamentoItem>();
    public ICollection<OrcamentoMaoDeObra> MaosDeObra { get; set; } = new List<OrcamentoMaoDeObra>();
    public ICollection<OrcamentoTecnico> Tecnicos { get; set; } = new List<OrcamentoTecnico>();
    public ICollection<OrcamentoHistorico> Historicos { get; set; } = new List<OrcamentoHistorico>();

    public void RecalcularTotais()
    {
        ValorTotalItens = Itens.Sum(i => i.CalcularTotal());
        ValorTotalMaoDeObra = MaosDeObra.Sum(m => m.CalcularTotal());
        
        var subtotal = ValorTotalItens + ValorTotalMaoDeObra + ValorAcrescimo;
        ValorTotal = subtotal >= ValorDesconto ? subtotal - ValorDesconto : 0m;
    }
}

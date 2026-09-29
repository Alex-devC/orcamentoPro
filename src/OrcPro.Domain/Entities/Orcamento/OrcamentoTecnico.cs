using OrcPro.Domain.Common;
using OrcPro.Domain.Entities.Tecnico;

namespace OrcPro.Domain.Entities.Orcamento;

public class OrcamentoTecnico : BaseEntity
{
    public int OrcamentoId { get; set; }
    public Orcamento? Orcamento { get; set; }

    public int TecnicoId { get; set; }
    public Tecnico.Tecnico? Tecnico { get; set; }

    public string? Funcao { get; set; }
    public string? Observacoes { get; set; }
}

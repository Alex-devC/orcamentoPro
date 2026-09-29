using OrcPro.Domain.Common;
using OrcPro.Domain.Entities.Tecnico;

namespace OrcPro.Domain.Entities.Orcamento;

public class OrcamentoMaoDeObraTecnico : BaseEntity
{
    public int OrcamentoMaoDeObraId { get; set; }
    public OrcamentoMaoDeObra? OrcamentoMaoDeObra { get; set; }

    public int TecnicoId { get; set; }
    public Tecnico.Tecnico? Tecnico { get; set; }

    public string? Funcao { get; set; }
    public string? Observacoes { get; set; }
}

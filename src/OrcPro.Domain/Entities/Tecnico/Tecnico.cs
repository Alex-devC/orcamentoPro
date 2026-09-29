using OrcPro.Domain.Common;
using OrcPro.Domain.Entities.Orcamento;

namespace OrcPro.Domain.Entities.Tecnico;

public class Tecnico : BaseEntity
{
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Cpf { get; set; }
    public string? Telefone { get; set; }
    public string? Celular { get; set; }
    public string? Email { get; set; }
    public string? Especialidade { get; set; }
    public string? RegistroProfissional { get; set; }
    public string? Observacoes { get; set; }
    public bool Ativo { get; set; } = true;

    public ICollection<OrcamentoTecnico> OrcamentoTecnicos { get; set; } = new List<OrcamentoTecnico>();
    public ICollection<OrcamentoMaoDeObraTecnico> MaoDeObraTecnicos { get; set; } = new List<OrcamentoMaoDeObraTecnico>();
}

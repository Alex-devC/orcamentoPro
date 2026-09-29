using OrcPro.Domain.Common;
using OrcPro.Domain.Entities.Seguranca;

namespace OrcPro.Domain.Entities.Orcamento;

public class OrcamentoHistorico : BaseEntity
{
    public int OrcamentoId { get; set; }
    public Orcamento? Orcamento { get; set; }

    public DateTime DataRegistro { get; set; } = DateTime.UtcNow;

    public int? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public string? NomeUsuario { get; set; }

    public string Acao { get; set; } = string.Empty;
    public string? StatusAnterior { get; set; }
    public string? StatusNovo { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string? Detalhes { get; set; }
}

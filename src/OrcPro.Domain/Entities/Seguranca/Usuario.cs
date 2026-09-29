using OrcPro.Domain.Common;
using OrcPro.Domain.Entities.Orcamento;

namespace OrcPro.Domain.Entities.Seguranca;

public class Usuario : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string NomeCompleto { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime? UltimoLogin { get; set; }

    public int PerfilId { get; set; }
    public Perfil? Perfil { get; set; }

    public ICollection<Orcamento.Orcamento> Orcamentos { get; set; } = new List<Orcamento.Orcamento>();
}

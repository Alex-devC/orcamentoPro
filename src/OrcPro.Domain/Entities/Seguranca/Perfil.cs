using OrcPro.Domain.Common;

namespace OrcPro.Domain.Entities.Seguranca;

public class Perfil : BaseEntity
{
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public bool Ativo { get; set; } = true;

    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    public ICollection<PerfilPermissao> PerfilPermissoes { get; set; } = new List<PerfilPermissao>();
}

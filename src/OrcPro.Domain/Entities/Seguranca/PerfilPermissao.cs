using OrcPro.Domain.Common;

namespace OrcPro.Domain.Entities.Seguranca;

public class PerfilPermissao : BaseEntity
{
    public int PerfilId { get; set; }
    public Perfil? Perfil { get; set; }

    public int PermissaoId { get; set; }
    public Permissao? Permissao { get; set; }
}

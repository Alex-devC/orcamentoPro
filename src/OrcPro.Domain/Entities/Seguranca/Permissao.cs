using OrcPro.Domain.Common;

namespace OrcPro.Domain.Entities.Seguranca;

public class Permissao : BaseEntity
{
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Modulo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public bool Ativo { get; set; } = true;

    public ICollection<PerfilPermissao> PerfilPermissoes { get; set; } = new List<PerfilPermissao>();
}

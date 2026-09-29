using OrcPro.Domain.Common;

namespace OrcPro.Domain.Entities.Configuracao;

public class Configuracao : BaseEntity
{
    public string Chave { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string Categoria { get; set; } = "Geral";
    public bool Editavel { get; set; } = true;
}

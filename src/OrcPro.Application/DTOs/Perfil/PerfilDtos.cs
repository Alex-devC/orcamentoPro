namespace OrcPro.Application.DTOs.Perfil;

public class PermissaoDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Modulo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
}

public class PerfilDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public bool Ativo { get; set; }
    public List<PermissaoDto> Permissoes { get; set; } = new();
}

public class SalvarPerfilDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public bool Ativo { get; set; } = true;
    public List<int> PermissaoIds { get; set; } = new();
}

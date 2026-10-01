namespace OrcPro.Application.DTOs.Usuario;

public class UsuarioDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string NomeCompleto { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool Ativo { get; set; }
    public DateTime? UltimoLogin { get; set; }
    public DateTime DataCriacao { get; set; }
    public int PerfilId { get; set; }
    public string PerfilNome { get; set; } = string.Empty;
}

public class CriarUsuarioDto
{
    public string Username { get; set; } = string.Empty;
    public string Senha { get; set; } = string.Empty;
    public string NomeCompleto { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int PerfilId { get; set; }
    public bool Ativo { get; set; } = true;
}

public class AtualizarUsuarioDto
{
    public int Id { get; set; }
    public string NomeCompleto { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int PerfilId { get; set; }
    public bool Ativo { get; set; }

    /// <summary>
    /// Nova senha opcional (edição). Quando informada, é gravada sempre pelo
    /// <c>IPasswordHasher</c> existente; quando vazia, a senha atual é mantida.
    /// </summary>
    public string? NovaSenha { get; set; }
}

public class AlterarSenhaDto
{
    public int UsuarioId { get; set; }
    public string SenhaAtual { get; set; } = string.Empty;
    public string NovaSenha { get; set; } = string.Empty;
}

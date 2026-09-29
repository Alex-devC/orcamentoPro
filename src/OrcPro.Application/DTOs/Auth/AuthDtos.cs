namespace OrcPro.Application.DTOs.Auth;

public class LoginRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Senha { get; set; } = string.Empty;
}

public class UsuarioSessaoDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string NomeCompleto { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int PerfilId { get; set; }
    public string PerfilNome { get; set; } = string.Empty;
    public IReadOnlyList<string> Permissoes { get; set; } = Array.Empty<string>();

    public bool PossuiPermissao(string permissaoCodigo)
    {
        return Permissoes.Contains(permissaoCodigo, StringComparer.OrdinalIgnoreCase);
    }
}

public class LoginResponseDto
{
    public bool Sucesso { get; set; }
    public string? Mensagem { get; set; }
    public UsuarioSessaoDto? Usuario { get; set; }
    public DateTime DataHoraLogin { get; set; } = DateTime.UtcNow;

    public static LoginResponseDto ComSucesso(UsuarioSessaoDto usuario)
        => new() { Sucesso = true, Usuario = usuario };

    public static LoginResponseDto ComFalha(string mensagem)
        => new() { Sucesso = false, Mensagem = mensagem };
}

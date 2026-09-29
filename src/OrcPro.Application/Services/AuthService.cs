using OrcPro.Application.DTOs.Auth;
using OrcPro.Application.Exceptions;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Application.Interfaces.Services;

namespace OrcPro.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IPasswordHasher _passwordHasher;

    public AuthService(IUsuarioRepository usuarioRepository, IPasswordHasher passwordHasher)
    {
        _usuarioRepository = usuarioRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Senha))
            return LoginResponseDto.ComFalha("Nome de usuário e senha são obrigatórios.");

        var usuario = await _usuarioRepository.GetWithPerfilAndPermissoesByUsernameAsync(request.Username.Trim(), cancellationToken);

        if (usuario == null)
            return LoginResponseDto.ComFalha("Usuário ou senha inválidos.");

        if (!usuario.Ativo)
            return LoginResponseDto.ComFalha("Este usuário está inativo no sistema.");

        var senhaValida = _passwordHasher.VerifyPassword(request.Senha, usuario.PasswordHash);
        if (!senhaValida)
            return LoginResponseDto.ComFalha("Usuário ou senha inválidos.");

        // Atualiza timestamp do último login com sucesso
        usuario.UltimoLogin = DateTime.UtcNow;
        await _usuarioRepository.UpdateAsync(usuario, cancellationToken);

        var permissoes = usuario.Perfil?.PerfilPermissoes?
            .Where(pp => pp.Permissao != null && pp.Permissao.Ativo)
            .Select(pp => pp.Permissao!.Codigo)
            .Distinct()
            .ToList() ?? new List<string>();

        var sessao = new UsuarioSessaoDto
        {
            Id = usuario.Id,
            Username = usuario.Username,
            NomeCompleto = usuario.NomeCompleto,
            Email = usuario.Email,
            PerfilId = usuario.PerfilId,
            PerfilNome = usuario.Perfil?.Nome ?? "Sem Perfil",
            Permissoes = permissoes
        };

        return LoginResponseDto.ComSucesso(sessao);
    }

    public async Task<UsuarioSessaoDto?> ObterSessaoAsync(int usuarioId, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarioRepository.GetWithPerfilAndPermissoesAsync(usuarioId, cancellationToken);
        if (usuario == null || !usuario.Ativo)
            return null;

        var permissoes = usuario.Perfil?.PerfilPermissoes?
            .Where(pp => pp.Permissao != null && pp.Permissao.Ativo)
            .Select(pp => pp.Permissao!.Codigo)
            .Distinct()
            .ToList() ?? new List<string>();

        return new UsuarioSessaoDto
        {
            Id = usuario.Id,
            Username = usuario.Username,
            NomeCompleto = usuario.NomeCompleto,
            Email = usuario.Email,
            PerfilId = usuario.PerfilId,
            PerfilNome = usuario.Perfil?.Nome ?? "Sem Perfil",
            Permissoes = permissoes
        };
    }

    public async Task<bool> ValidarPermissaoAsync(int usuarioId, string permissaoCodigo, CancellationToken cancellationToken = default)
    {
        var sessao = await ObterSessaoAsync(usuarioId, cancellationToken);
        return sessao != null && sessao.PossuiPermissao(permissaoCodigo);
    }
}

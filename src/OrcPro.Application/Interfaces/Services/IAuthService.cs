using OrcPro.Application.DTOs.Auth;

namespace OrcPro.Application.Interfaces.Services;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<UsuarioSessaoDto?> ObterSessaoAsync(int usuarioId, CancellationToken cancellationToken = default);
    Task<bool> ValidarPermissaoAsync(int usuarioId, string permissaoCodigo, CancellationToken cancellationToken = default);
}

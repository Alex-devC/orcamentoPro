using OrcPro.Application.DTOs.Common;
using OrcPro.Application.DTOs.Usuario;

namespace OrcPro.Application.Interfaces.Services;

public interface IUsuarioService
{
    Task<UsuarioDto> ObterPorIdAsync(int id, CancellationToken cancellationToken = default);
    Task<UsuarioDto> ObterPorUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<PagedResult<UsuarioDto>> ListarPaginadoAsync(PagedRequest request, CancellationToken cancellationToken = default);
    Task<UsuarioDto> CriarAsync(CriarUsuarioDto dto, CancellationToken cancellationToken = default);
    Task<UsuarioDto> AtualizarAsync(AtualizarUsuarioDto dto, CancellationToken cancellationToken = default);
    Task AlterarSenhaAsync(AlterarSenhaDto dto, CancellationToken cancellationToken = default);
    Task AlterarStatusAtivoAsync(int id, bool ativo, CancellationToken cancellationToken = default);
    Task ExcluirAsync(int id, CancellationToken cancellationToken = default);
}

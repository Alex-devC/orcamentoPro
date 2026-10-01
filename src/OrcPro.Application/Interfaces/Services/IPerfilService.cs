using OrcPro.Application.DTOs.Common;
using OrcPro.Application.DTOs.Perfil;

namespace OrcPro.Application.Interfaces.Services;

/// <summary>
/// Consulta do catálogo de permissões do sistema (tabela <c>Permissoes</c>), usado pela tela
/// de edição de perfil para montar os checkboxes agrupados por módulo.
/// </summary>
public interface IPermissaoService
{
    Task<IReadOnlyList<PermissaoDto>> ListarTodasAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PermissaoDto>> ListarAtivasAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Regras de negócio do cadastro de Perfis (nome, descrição, situação e vínculos).
/// As permissões já fazem parte do modelo: <see cref="SalvarPerfilDto.PermissaoIds"/> é
/// sincronizado pelo serviço, deixando a gestão de permissões pronta para uso futuro.
/// </summary>
public interface IPerfilService
{
    Task<PerfilDto> ObterPorIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PerfilDto>> ListarAtivosAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<PerfilDto>> ListarPaginadoAsync(PagedRequest request, CancellationToken cancellationToken = default);
    Task<PerfilDto> CriarAsync(SalvarPerfilDto dto, CancellationToken cancellationToken = default);
    Task<PerfilDto> AtualizarAsync(SalvarPerfilDto dto, CancellationToken cancellationToken = default);
    Task AlterarStatusAtivoAsync(int id, bool ativo, CancellationToken cancellationToken = default);
    Task ExcluirAsync(int id, CancellationToken cancellationToken = default);
}
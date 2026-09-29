using OrcPro.Application.DTOs.Common;
using OrcPro.Application.DTOs.Tecnico;

namespace OrcPro.Application.Interfaces.Services;

public interface ITecnicoService
{
    Task<TecnicoDto> ObterPorIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PagedResult<TecnicoDto>> ListarPaginadoAsync(PagedRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TecnicoDto>> ListarTodosAtivosAsync(CancellationToken cancellationToken = default);
    Task<TecnicoDto> CriarAsync(CriarTecnicoDto dto, CancellationToken cancellationToken = default);
    Task<TecnicoDto> AtualizarAsync(AtualizarTecnicoDto dto, CancellationToken cancellationToken = default);
    Task InativarAsync(int id, CancellationToken cancellationToken = default);
    Task ExcluirAsync(int id, CancellationToken cancellationToken = default);
}

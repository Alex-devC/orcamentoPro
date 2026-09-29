using OrcPro.Application.DTOs.Common;
using OrcPro.Application.DTOs.Peca;

namespace OrcPro.Application.Interfaces.Services;

public interface IPecaService
{
    Task<PecaDto> ObterPorIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PagedResult<PecaDto>> ListarPaginadoAsync(PagedRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PecaDto>> ListarTodasAtivasAsync(CancellationToken cancellationToken = default);
    Task<PecaDto> CriarAsync(CriarPecaDto dto, CancellationToken cancellationToken = default);
    Task<PecaDto> AtualizarAsync(AtualizarPecaDto dto, CancellationToken cancellationToken = default);
    Task InativarAsync(int id, CancellationToken cancellationToken = default);
    Task ExcluirAsync(int id, CancellationToken cancellationToken = default);
}

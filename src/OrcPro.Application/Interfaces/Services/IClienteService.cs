using OrcPro.Application.DTOs.Cliente;
using OrcPro.Application.DTOs.Common;

namespace OrcPro.Application.Interfaces.Services;

public interface IClienteService
{
    Task<ClienteDto> ObterPorIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ClienteDto?> ObterPorCpfCnpjAsync(string cpfCnpj, CancellationToken cancellationToken = default);
    Task<PagedResult<ClienteDto>> ListarPaginadoAsync(PagedRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClienteDto>> ListarTodosAtivosAsync(CancellationToken cancellationToken = default);
    Task<ClienteDto> CriarAsync(CriarClienteDto dto, CancellationToken cancellationToken = default);
    Task<ClienteDto> AtualizarAsync(AtualizarClienteDto dto, CancellationToken cancellationToken = default);
    Task InativarAsync(int id, CancellationToken cancellationToken = default);
    Task ExcluirAsync(int id, CancellationToken cancellationToken = default);
}

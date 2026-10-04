using OrcPro.Application.DTOs.Common;
using OrcPro.Application.DTOs.Servico;

namespace OrcPro.Application.Interfaces.Services;

public interface IServicoService
{
    Task<ServicoDto> ObterPorIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PagedResult<ServicoDto>> ListarPaginadoAsync(PagedRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Serviços ativos para seleção futura no Orçamento (código, descrição, valor,
    /// unidade e tempo estimado). Mesmo padrão do módulo Peças: devolve só os ativos.
    /// </summary>
    Task<IReadOnlyList<ServicoDto>> ListarTodosAtivosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Próximo código disponível para um novo serviço (ex.: "SRV-0001"). Usado pelo
    /// formulário para preencher o código automaticamente — o usuário não digita.
    /// </summary>
    Task<string> GerarProximoCodigoAsync(CancellationToken cancellationToken = default);

    Task<ServicoDto> CriarAsync(CriarServicoDto dto, CancellationToken cancellationToken = default);
    Task<ServicoDto> AtualizarAsync(AtualizarServicoDto dto, CancellationToken cancellationToken = default);
    Task AtivarAsync(int id, CancellationToken cancellationToken = default);
    Task InativarAsync(int id, CancellationToken cancellationToken = default);
    Task ExcluirAsync(int id, CancellationToken cancellationToken = default);
}
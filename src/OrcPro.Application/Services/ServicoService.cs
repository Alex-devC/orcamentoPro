using OrcPro.Application.DTOs.Common;
using OrcPro.Application.DTOs.Servico;
using OrcPro.Application.Exceptions;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Common.Formatters;
using OrcPro.Domain.Entities.Servico;

namespace OrcPro.Application.Services;

public class ServicoService : IServicoService
{
    private readonly IServicoRepository _servicoRepository;

    public ServicoService(IServicoRepository servicoRepository)
    {
        _servicoRepository = servicoRepository;
    }

    public async Task<ServicoDto> ObterPorIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var servico = await _servicoRepository.GetByIdAsync(id, cancellationToken);
        if (servico == null)
            throw new NotFoundException("Serviço", id);

        return MapearParaDto(servico);
    }

    public async Task<PagedResult<ServicoDto>> ListarPaginadoAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _servicoRepository.GetPagedAsync(request, cancellationToken);
        var dtos = result.Items.Select(MapearParaDto).ToList();
        return new PagedResult<ServicoDto>(dtos, result.TotalCount, result.PageNumber, result.PageSize);
    }

    public async Task<IReadOnlyList<ServicoDto>> ListarTodosAtivosAsync(CancellationToken cancellationToken = default)
    {
        var servicos = await _servicoRepository.GetAllAtivosAsync(cancellationToken);
        return servicos.Select(MapearParaDto).ToList();
    }

    public Task<string> GerarProximoCodigoAsync(CancellationToken cancellationToken = default)
        => _servicoRepository.GerarProximoCodigoAsync(cancellationToken);

    public async Task<ServicoDto> CriarAsync(CriarServicoDto dto, CancellationToken cancellationToken = default)
    {
        ValidarServico(dto);

        if (await _servicoRepository.ExistsCodigoAsync(dto.Codigo.Trim(), null, cancellationToken))
            throw new BusinessException($"Já existe um serviço cadastrado com o código '{dto.Codigo}'.");

        var servico = new Servico
        {
            Codigo = dto.Codigo.Trim().ToUpperInvariant(),
            Descricao = InputFormattingHelper.ToUpperCase(dto.Descricao.Trim()),
            Categoria = InputFormattingHelper.NormalizeText(dto.Categoria),
            Valor = dto.Valor,
            Unidade = dto.Unidade.Trim().ToUpperInvariant(),
            TempoEstimado = dto.TempoEstimado,
            Observacoes = InputFormattingHelper.NormalizeText(dto.Observacoes),
            Ativo = dto.Ativo,
            DataCriacao = DateTime.UtcNow
        };

        var criado = await _servicoRepository.AddAsync(servico, cancellationToken);
        return MapearParaDto(criado);
    }

    public async Task<ServicoDto> AtualizarAsync(AtualizarServicoDto dto, CancellationToken cancellationToken = default)
    {
        ValidarServico(dto);

        var servico = await _servicoRepository.GetByIdAsync(dto.Id, cancellationToken);
        if (servico == null)
            throw new NotFoundException("Serviço", dto.Id);

        if (await _servicoRepository.ExistsCodigoAsync(dto.Codigo.Trim(), dto.Id, cancellationToken))
            throw new BusinessException($"Já existe outro serviço cadastrado com o código '{dto.Codigo}'.");

        // O código é imutável: a edição nunca o altera, apenas o reaproveita.
        servico.Descricao = InputFormattingHelper.ToUpperCase(dto.Descricao.Trim());
        servico.Categoria = InputFormattingHelper.NormalizeText(dto.Categoria);
        servico.Valor = dto.Valor;
        servico.Unidade = dto.Unidade.Trim().ToUpperInvariant();
        servico.TempoEstimado = dto.TempoEstimado;
        servico.Observacoes = InputFormattingHelper.NormalizeText(dto.Observacoes);
        servico.Ativo = dto.Ativo;
        servico.DataAtualizacao = DateTime.UtcNow;

        await _servicoRepository.UpdateAsync(servico, cancellationToken);
        return MapearParaDto(servico);
    }

    public Task AtivarAsync(int id, CancellationToken cancellationToken = default)
        => AlterarSituacaoAsync(id, ativo: true, cancellationToken);

    public Task InativarAsync(int id, CancellationToken cancellationToken = default)
        => AlterarSituacaoAsync(id, ativo: false, cancellationToken);

    private async Task AlterarSituacaoAsync(int id, bool ativo, CancellationToken cancellationToken)
    {
        var servico = await _servicoRepository.GetByIdAsync(id, cancellationToken);
        if (servico == null)
            throw new NotFoundException("Serviço", id);

        servico.Ativo = ativo;
        servico.DataAtualizacao = DateTime.UtcNow;
        await _servicoRepository.UpdateAsync(servico, cancellationToken);
    }

    public async Task ExcluirAsync(int id, CancellationToken cancellationToken = default)
    {
        if (!await _servicoRepository.ExistsAsync(id, cancellationToken))
            throw new NotFoundException("Serviço", id);

        // Serviço já usado em orçamento não pode sumir do histórico: nesse caso a
        // alternativa é apenas a inativação.
        var vinculos = await _servicoRepository.CountVinculosOrcamentoAsync(id, cancellationToken);
        if (vinculos > 0)
            throw new BusinessException(
                $"Este serviço está sendo usado em {vinculos} orçamento(s) e não pode ser excluído. Inative-o para retirar da lista de seleção.");

        await _servicoRepository.DeleteAsync(id, cancellationToken);
    }

    private static void ValidarServico(CriarServicoDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Codigo))
            throw new ValidationException("O código do serviço é obrigatório.");

        if (string.IsNullOrWhiteSpace(dto.Descricao))
            throw new ValidationException("A descrição do serviço é obrigatória.");

        if (dto.Valor < 0)
            throw new ValidationException("O valor do serviço não pode ser negativo.");

        if (dto.TempoEstimado < 0)
            throw new ValidationException("O tempo estimado não pode ser negativo.");
    }

    private static ServicoDto MapearParaDto(Servico s)
    {
        return new ServicoDto
        {
            Id = s.Id,
            Codigo = s.Codigo,
            Descricao = s.Descricao,
            Categoria = s.Categoria,
            Valor = s.Valor,
            Unidade = s.Unidade,
            TempoEstimado = s.TempoEstimado,
            Observacoes = s.Observacoes,
            Ativo = s.Ativo,
            DataCriacao = s.DataCriacao
        };
    }
}
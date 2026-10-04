using OrcPro.Application.DTOs.Common;
using OrcPro.Application.DTOs.Peca;
using OrcPro.Application.Exceptions;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Common;
using OrcPro.Domain.Common.Formatters;
using OrcPro.Domain.Entities.Peca;

namespace OrcPro.Application.Services;

public class PecaService : IPecaService
{
    private readonly IPecaRepository _pecaRepository;

    public PecaService(IPecaRepository pecaRepository)
    {
        _pecaRepository = pecaRepository;
    }

    public async Task<PecaDto> ObterPorIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var peca = await _pecaRepository.GetByIdAsync(id, cancellationToken);
        if (peca == null)
            throw new NotFoundException("Peça", id);

        return MapearParaDto(peca);
    }

    public async Task<PagedResult<PecaDto>> ListarPaginadoAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _pecaRepository.GetPagedAsync(request, cancellationToken);
        var dtos = result.Items.Select(MapearParaDto).ToList();
        return new PagedResult<PecaDto>(dtos, result.TotalCount, result.PageNumber, result.PageSize);
    }

    public async Task<IReadOnlyList<PecaDto>> ListarTodasAtivasAsync(CancellationToken cancellationToken = default)
    {
        var pecas = await _pecaRepository.GetAllAtivosAsync(cancellationToken);
        return pecas.Select(MapearParaDto).ToList();
    }

    public Task<string> GerarProximoCodigoAsync(CancellationToken cancellationToken = default)
        => _pecaRepository.GerarProximoCodigoAsync(cancellationToken);

    public async Task<PecaDto> CriarAsync(CriarPecaDto dto, CancellationToken cancellationToken = default)
    {
        ValidarPeca(dto);

        if (await _pecaRepository.ExistsCodigoAsync(dto.Codigo.Trim(), null, cancellationToken))
            throw new BusinessException($"Já existe uma peça cadastrada com o código '{dto.Codigo}'.");

        var peca = new Peca
        {
            Codigo = dto.Codigo.Trim().ToUpperInvariant(),
            Descricao = InputFormattingHelper.ToUpperCase(dto.Descricao.Trim()),
            Categoria = InputFormattingHelper.NormalizeText(dto.Categoria),
            Marca = InputFormattingHelper.NormalizeText(dto.Marca),
            Modelo = InputFormattingHelper.NormalizeText(dto.Modelo),
            CodigoBarras = InputFormattingHelper.NormalizeText(dto.CodigoBarras),
            UnidadeMedida = dto.UnidadeMedida.Trim().ToUpperInvariant(),
            PrecoCusto = dto.PrecoCusto,
            PrecoVenda = dto.PrecoVenda,
            EstoqueAtual = dto.EstoqueAtual,
            EstoqueMinimo = dto.EstoqueMinimo,
            Observacoes = dto.Observacoes?.Trim(),
            Ativo = dto.Ativo,
            DataCriacao = DateTime.UtcNow
        };

        var criada = await _pecaRepository.AddAsync(peca, cancellationToken);
        return MapearParaDto(criada);
    }

    public async Task<PecaDto> AtualizarAsync(AtualizarPecaDto dto, CancellationToken cancellationToken = default)
    {
        ValidarPeca(dto);

        var peca = await _pecaRepository.GetByIdAsync(dto.Id, cancellationToken);
        if (peca == null)
            throw new NotFoundException("Peça", dto.Id);

        if (await _pecaRepository.ExistsCodigoAsync(dto.Codigo.Trim(), dto.Id, cancellationToken))
            throw new BusinessException($"Já existe outra peça cadastrada com o código '{dto.Codigo}'.");

        peca.Codigo = dto.Codigo.Trim().ToUpperInvariant();
        peca.Descricao = InputFormattingHelper.ToUpperCase(dto.Descricao.Trim());
        peca.Categoria = InputFormattingHelper.NormalizeText(dto.Categoria);
        peca.Marca = InputFormattingHelper.NormalizeText(dto.Marca);
        peca.Modelo = InputFormattingHelper.NormalizeText(dto.Modelo);
        peca.CodigoBarras = InputFormattingHelper.NormalizeText(dto.CodigoBarras);
        peca.UnidadeMedida = dto.UnidadeMedida.Trim().ToUpperInvariant();
        peca.PrecoCusto = dto.PrecoCusto;
        peca.PrecoVenda = dto.PrecoVenda;
        peca.EstoqueAtual = dto.EstoqueAtual;
        peca.EstoqueMinimo = dto.EstoqueMinimo;
        peca.Observacoes = dto.Observacoes?.Trim();
        peca.Ativo = dto.Ativo;
        peca.DataAtualizacao = DateTime.UtcNow;

        await _pecaRepository.UpdateAsync(peca, cancellationToken);
        return MapearParaDto(peca);
    }

    public async Task InativarAsync(int id, CancellationToken cancellationToken = default)
    {
        var peca = await _pecaRepository.GetByIdAsync(id, cancellationToken);
        if (peca == null)
            throw new NotFoundException("Peça", id);

        peca.Ativo = false;
        peca.DataAtualizacao = DateTime.UtcNow;
        await _pecaRepository.UpdateAsync(peca, cancellationToken);
    }

    public async Task ExcluirAsync(int id, CancellationToken cancellationToken = default)
    {
        var existe = await _pecaRepository.ExistsAsync(id, cancellationToken);
        if (!existe)
            throw new NotFoundException("Peça", id);

        await _pecaRepository.DeleteAsync(id, cancellationToken);
    }

    private static void ValidarPeca(CriarPecaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Codigo))
            throw new ValidationException("O código da peça é obrigatório.");

        if (string.IsNullOrWhiteSpace(dto.Descricao))
            throw new ValidationException("A descrição da peça é obrigatória.");

        if (dto.PrecoVenda < 0)
            throw new ValidationException("O preço de venda não pode ser negativo.");
    }

    private static PecaDto MapearParaDto(Peca p)
    {
        return new PecaDto
        {
            Id = p.Id,
            Codigo = p.Codigo,
            Descricao = p.Descricao,
            Categoria = p.Categoria,
            Marca = p.Marca,
            Modelo = p.Modelo,
            CodigoBarras = p.CodigoBarras,
            UnidadeMedida = p.UnidadeMedida,
            PrecoCusto = p.PrecoCusto,
            PrecoVenda = p.PrecoVenda,
            EstoqueAtual = p.EstoqueAtual,
            EstoqueMinimo = p.EstoqueMinimo,
            Observacoes = p.Observacoes,
            Ativo = p.Ativo,
            DataCriacao = p.DataCriacao
        };
    }
}

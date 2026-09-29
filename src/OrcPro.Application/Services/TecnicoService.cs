using OrcPro.Application.DTOs.Common;
using OrcPro.Application.DTOs.Tecnico;
using OrcPro.Application.Exceptions;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Entities.Tecnico;

namespace OrcPro.Application.Services;

public class TecnicoService : ITecnicoService
{
    private readonly ITecnicoRepository _tecnicoRepository;

    public TecnicoService(ITecnicoRepository tecnicoRepository)
    {
        _tecnicoRepository = tecnicoRepository;
    }

    public async Task<TecnicoDto> ObterPorIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var tecnico = await _tecnicoRepository.GetByIdAsync(id, cancellationToken);
        if (tecnico == null)
            throw new NotFoundException("Técnico", id);

        return MapearParaDto(tecnico);
    }

    public async Task<PagedResult<TecnicoDto>> ListarPaginadoAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _tecnicoRepository.GetPagedAsync(request, cancellationToken);
        var dtos = result.Items.Select(MapearParaDto).ToList();
        return new PagedResult<TecnicoDto>(dtos, result.TotalCount, result.PageNumber, result.PageSize);
    }

    public async Task<IReadOnlyList<TecnicoDto>> ListarTodosAtivosAsync(CancellationToken cancellationToken = default)
    {
        var tecnicos = await _tecnicoRepository.GetAllAtivosAsync(cancellationToken);
        return tecnicos.Select(MapearParaDto).ToList();
    }

    public async Task<TecnicoDto> CriarAsync(CriarTecnicoDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome))
            throw new ValidationException("O nome do técnico é obrigatório.");

        var codigo = string.IsNullOrWhiteSpace(dto.Codigo)
            ? await _tecnicoRepository.GerarProximoCodigoAsync(cancellationToken)
            : dto.Codigo.Trim();

        if (await _tecnicoRepository.ExistsCodigoAsync(codigo, null, cancellationToken))
            throw new BusinessException($"Já existe um técnico cadastrado com o código '{codigo}'.");

        var tecnico = new Tecnico
        {
            Codigo = codigo,
            Nome = dto.Nome.Trim(),
            Cpf = dto.Cpf?.Trim(),
            Telefone = dto.Telefone?.Trim(),
            Celular = dto.Celular?.Trim(),
            Email = dto.Email?.Trim(),
            Especialidade = dto.Especialidade?.Trim(),
            RegistroProfissional = dto.RegistroProfissional?.Trim(),
            Observacoes = dto.Observacoes?.Trim(),
            Ativo = dto.Ativo,
            DataCriacao = DateTime.UtcNow
        };

        var criado = await _tecnicoRepository.AddAsync(tecnico, cancellationToken);
        return MapearParaDto(criado);
    }

    public async Task<TecnicoDto> AtualizarAsync(AtualizarTecnicoDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome))
            throw new ValidationException("O nome do técnico é obrigatório.");

        var tecnico = await _tecnicoRepository.GetByIdAsync(dto.Id, cancellationToken);
        if (tecnico == null)
            throw new NotFoundException("Técnico", dto.Id);

        if (!string.IsNullOrWhiteSpace(dto.Codigo) &&
            await _tecnicoRepository.ExistsCodigoAsync(dto.Codigo.Trim(), dto.Id, cancellationToken))
        {
            throw new BusinessException($"Já existe outro técnico cadastrado com o código '{dto.Codigo}'.");
        }

        if (!string.IsNullOrWhiteSpace(dto.Codigo))
            tecnico.Codigo = dto.Codigo.Trim();

        tecnico.Nome = dto.Nome.Trim();
        tecnico.Cpf = dto.Cpf?.Trim();
        tecnico.Telefone = dto.Telefone?.Trim();
        tecnico.Celular = dto.Celular?.Trim();
        tecnico.Email = dto.Email?.Trim();
        tecnico.Especialidade = dto.Especialidade?.Trim();
        tecnico.RegistroProfissional = dto.RegistroProfissional?.Trim();
        tecnico.Observacoes = dto.Observacoes?.Trim();
        tecnico.Ativo = dto.Ativo;
        tecnico.DataAtualizacao = DateTime.UtcNow;

        await _tecnicoRepository.UpdateAsync(tecnico, cancellationToken);
        return MapearParaDto(tecnico);
    }

    public async Task InativarAsync(int id, CancellationToken cancellationToken = default)
    {
        var tecnico = await _tecnicoRepository.GetByIdAsync(id, cancellationToken);
        if (tecnico == null)
            throw new NotFoundException("Técnico", id);

        tecnico.Ativo = false;
        tecnico.DataAtualizacao = DateTime.UtcNow;
        await _tecnicoRepository.UpdateAsync(tecnico, cancellationToken);
    }

    public async Task ExcluirAsync(int id, CancellationToken cancellationToken = default)
    {
        var existe = await _tecnicoRepository.ExistsAsync(id, cancellationToken);
        if (!existe)
            throw new NotFoundException("Técnico", id);

        await _tecnicoRepository.DeleteAsync(id, cancellationToken);
    }

    private static TecnicoDto MapearParaDto(Tecnico t)
    {
        return new TecnicoDto
        {
            Id = t.Id,
            Codigo = t.Codigo,
            Nome = t.Nome,
            Cpf = t.Cpf,
            Telefone = t.Telefone,
            Celular = t.Celular,
            Email = t.Email,
            Especialidade = t.Especialidade,
            RegistroProfissional = t.RegistroProfissional,
            Observacoes = t.Observacoes,
            Ativo = t.Ativo,
            DataCriacao = t.DataCriacao
        };
    }
}

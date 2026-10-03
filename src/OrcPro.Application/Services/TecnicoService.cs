using OrcPro.Application.DTOs.Common;
using OrcPro.Application.DTOs.Tecnico;
using OrcPro.Application.Exceptions;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Common;
using OrcPro.Domain.Common.Formatters;
using OrcPro.Domain.Entities.Tecnico;

// Alias to disambiguate between OrcPro.Domain.Common.CpfCnpjValidator (legacy) and OrcPro.Domain.Common.Formatters.CpfCnpjValidator (new)
using CpfCnpjValidatorEx = OrcPro.Domain.Common.Formatters.CpfCnpjValidator;

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

        return await MapearParaDtoAsync(tecnico, cancellationToken);
    }

    public async Task<PagedResult<TecnicoDto>> ListarPaginadoAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _tecnicoRepository.GetPagedAsync(request, cancellationToken);
        var dtos = new List<TecnicoDto>(result.Items.Count);
        foreach (var item in result.Items)
        {
            dtos.Add(await MapearParaDtoAsync(item, cancellationToken));
        }

        return new PagedResult<TecnicoDto>(dtos, result.TotalCount, result.PageNumber, result.PageSize);
    }

    public async Task<IReadOnlyList<TecnicoDto>> ListarTodosAtivosAsync(CancellationToken cancellationToken = default)
    {
        var tecnicos = await _tecnicoRepository.GetAllAtivosAsync(cancellationToken);
        var dtos = new List<TecnicoDto>(tecnicos.Count);
        foreach (var tecnico in tecnicos)
        {
            dtos.Add(await MapearParaDtoAsync(tecnico, cancellationToken));
        }

        return dtos;
    }

    public async Task<TecnicoDto> CriarAsync(CriarTecnicoDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome))
            throw new ValidationException("O nome do técnico é obrigatório.");

        var codigo = string.IsNullOrWhiteSpace(dto.Codigo)
            ? await _tecnicoRepository.GerarProximoCodigoAsync(cancellationToken)
            : dto.Codigo.Trim().ToUpperInvariant();

        if (await _tecnicoRepository.ExistsCodigoAsync(codigo, null, cancellationToken))
            throw new BusinessException($"Já existe um técnico cadastrado com o código '{codigo}'.");

        var cpf = await NormalizarCpfAsync(dto.Cpf, null, cancellationToken);

        var tecnico = new Tecnico
        {
            Codigo = codigo,
            Nome = dto.Nome.Trim().ToUpperInvariant(),
            Cpf = cpf,
            Rg = NormalizarTexto(dto.Rg),
            Telefone = PhoneMaskHelper.Normalizar(dto.Telefone),
            Celular = PhoneMaskHelper.Normalizar(dto.Celular),
            Email = InputFormattingHelper.NormalizeEmail(dto.Email),
            Especialidade = NormalizarTexto(dto.Especialidade),
            RegistroProfissional = NormalizarTexto(dto.RegistroProfissional),
            Observacoes = NormalizarTexto(dto.Observacoes),
            Ativo = dto.Ativo,
            DataCriacao = DateTime.UtcNow
        };

        AplicarEndereco(tecnico, dto);

        var criado = await _tecnicoRepository.AddAsync(tecnico, cancellationToken);
        return await MapearParaDtoAsync(criado, cancellationToken);
    }

    public async Task<TecnicoDto> AtualizarAsync(AtualizarTecnicoDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome))
            throw new ValidationException("O nome do técnico é obrigatório.");

        var tecnico = await _tecnicoRepository.GetByIdAsync(dto.Id, cancellationToken);
        if (tecnico == null)
            throw new NotFoundException("Técnico", dto.Id);

        if (!string.IsNullOrWhiteSpace(dto.Codigo) &&
            await _tecnicoRepository.ExistsCodigoAsync(dto.Codigo.Trim().ToUpperInvariant(), dto.Id, cancellationToken))
        {
            throw new BusinessException($"Já existe outro técnico cadastrado com o código '{dto.Codigo}'.");
        }

        if (!string.IsNullOrWhiteSpace(dto.Codigo))
            tecnico.Codigo = dto.Codigo.Trim().ToUpperInvariant();

        var cpf = await NormalizarCpfAsync(dto.Cpf, dto.Id, cancellationToken);

        tecnico.Nome = dto.Nome.Trim().ToUpperInvariant();
        tecnico.Cpf = cpf;
        tecnico.Rg = NormalizarTexto(dto.Rg);
        tecnico.Telefone = PhoneMaskHelper.Normalizar(dto.Telefone);
        tecnico.Celular = PhoneMaskHelper.Normalizar(dto.Celular);
        tecnico.Email = InputFormattingHelper.NormalizeEmail(dto.Email);
        tecnico.Especialidade = NormalizarTexto(dto.Especialidade);
        tecnico.RegistroProfissional = NormalizarTexto(dto.RegistroProfissional);
        tecnico.Observacoes = NormalizarTexto(dto.Observacoes);
        tecnico.Ativo = dto.Ativo;
        tecnico.DataAtualizacao = DateTime.UtcNow;

        AplicarEndereco(tecnico, dto);

        await _tecnicoRepository.UpdateAsync(tecnico, cancellationToken);
        return await MapearParaDtoAsync(tecnico, cancellationToken);
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

        var vinculos = await _tecnicoRepository.CountOrcamentosAsync(id, cancellationToken);
        if (vinculos > 0)
        {
            var sufixo = vinculos == 1 ? "1 orçamento" : $"{vinculos} orçamentos";
            throw new BusinessException(
                $"Este técnico está vinculado a {sufixo} e não pode ser excluído. " +
                "Altere o status para Inativo para arquivar o cadastro.");
        }

        await _tecnicoRepository.DeleteAsync(id, cancellationToken);
    }

    /// <summary>
    /// Valida o CPF quando informado, remove a máscara (gravando apenas dígitos, para evitar
    /// duplicidade com/sem máscara) e bloqueia o CPF já usado por outro técnico.
    /// </summary>
    private async Task<string?> NormalizarCpfAsync(string? cpf, int? ignorarId, CancellationToken cancellationToken)
    {
        var normalizado = CpfCnpjValidatorEx.Normalizar(cpf);
        if (string.IsNullOrEmpty(normalizado))
            return null;

        if (!CpfCnpjValidatorEx.EhValido(normalizado))
            throw new ValidationException("O CPF informado é inválido. Confira os dígitos.");

        if (await _tecnicoRepository.ExistsCpfAsync(normalizado, ignorarId, cancellationToken))
        {
            throw new BusinessException("Já existe outro técnico cadastrado com este CPF.");
        }

        return normalizado;
    }

    private static void AplicarEndereco(Tecnico tecnico, CriarTecnicoDto dto)
    {
        tecnico.Cep = CepMaskHelper.Normalizar(NormalizarTexto(dto.Cep));
        tecnico.Logradouro = NormalizarTexto(dto.Logradouro);
        tecnico.Numero = NormalizarTexto(dto.Numero);
        tecnico.Complemento = NormalizarTexto(dto.Complemento);
        tecnico.Bairro = NormalizarTexto(dto.Bairro);
        tecnico.Cidade = NormalizarTexto(dto.Cidade);
        tecnico.Uf = NormalizarTexto(dto.Uf)?.ToUpperInvariant();
    }

    private static string? NormalizarTexto(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim().ToUpperInvariant();

    private async Task<TecnicoDto> MapearParaDtoAsync(Tecnico t, CancellationToken cancellationToken)
    {
        return new TecnicoDto
        {
            Id = t.Id,
            Codigo = t.Codigo,
            Nome = t.Nome,
            Cpf = t.Cpf,
            Rg = t.Rg,
            Telefone = t.Telefone,
            Celular = t.Celular,
            Email = t.Email,
            Especialidade = t.Especialidade,
            RegistroProfissional = t.RegistroProfissional,
            Observacoes = t.Observacoes,
            Ativo = t.Ativo,
            DataCriacao = t.DataCriacao,
            Cep = t.Cep,
            Logradouro = t.Logradouro,
            Numero = t.Numero,
            Complemento = t.Complemento,
            Bairro = t.Bairro,
            Cidade = t.Cidade,
            Uf = t.Uf,
            QuantidadeOrcamentos = await _tecnicoRepository.CountOrcamentosAsync(t.Id, cancellationToken)
        };
    }
}

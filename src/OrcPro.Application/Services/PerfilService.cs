using OrcPro.Application.DTOs.Common;
using OrcPro.Application.DTOs.Perfil;
using OrcPro.Application.Exceptions;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Entities.Seguranca;

namespace OrcPro.Application.Services;

/// <summary>
/// Regras do cadastro de Perfis: nome único, descrição, ativação/inativação e exclusão.
/// Usa apenas os repositórios existentes (Perfil, Usuario e Permissao) e mantém as permissões
/// sincronizadas com <see cref="SalvarPerfilDto.PermissaoIds"/> — estrutura que já alimenta o
/// vínculo de permissões do perfil e fica pronta para a tela de gestão de permissões.
/// </summary>
public class PerfilService : IPerfilService
{
    private const int NomeMaxLength = 80;
    private const int DescricaoMaxLength = 250;

    private readonly IPerfilRepository _perfilRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IPermissaoRepository _permissaoRepository;

    public PerfilService(
        IPerfilRepository perfilRepository,
        IUsuarioRepository usuarioRepository,
        IPermissaoRepository permissaoRepository)
    {
        _perfilRepository = perfilRepository;
        _usuarioRepository = usuarioRepository;
        _permissaoRepository = permissaoRepository;
    }

    public async Task<PerfilDto> ObterPorIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var perfil = await _perfilRepository.GetWithPermissoesAsync(id, cancellationToken)
            ?? throw new NotFoundException("Perfil", id);

        var usuarios = await _usuarioRepository.CountByPerfilIdAsync(id, cancellationToken);
        return MapearParaDto(perfil, usuarios);
    }

    public async Task<IReadOnlyList<PerfilDto>> ListarAtivosAsync(CancellationToken cancellationToken = default)
    {
        var perfis = await _perfilRepository.GetAllAtivosAsync(cancellationToken);
        return perfis.Select(p => MapearParaDto(p, 0)).ToList();
    }

    public async Task<PagedResult<PerfilDto>> ListarPaginadoAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _perfilRepository.GetPagedAsync(request, cancellationToken);

        var dtos = new List<PerfilDto>(result.Items.Count);
        foreach (var perfil in result.Items)
        {
            var usuarios = await _usuarioRepository.CountByPerfilIdAsync(perfil.Id, cancellationToken);
            dtos.Add(MapearParaDto(perfil, usuarios));
        }

        return new PagedResult<PerfilDto>(dtos, result.TotalCount, result.PageNumber, result.PageSize);
    }

    public async Task<PerfilDto> CriarAsync(SalvarPerfilDto dto, CancellationToken cancellationToken = default)
    {
        ValidarCampos(dto);

        var nome = dto.Nome.Trim();
        await GarantirNomeUnicoAsync(nome, null, cancellationToken);

        var permissoesIds = await NormalizarPermissoesAsync(dto.PermissaoIds, cancellationToken);

        var perfil = new Perfil
        {
            Nome = nome,
            Descricao = NormalizarDescricao(dto.Descricao),
            Ativo = dto.Ativo,
            DataCriacao = DateTime.UtcNow
        };

        foreach (var permissaoId in permissoesIds)
        {
            perfil.PerfilPermissoes.Add(new PerfilPermissao { PermissaoId = permissaoId });
        }

        var criado = await _perfilRepository.AddAsync(perfil, cancellationToken);
        return await ObterPorIdAsync(criado.Id, cancellationToken);
    }

    public async Task<PerfilDto> AtualizarAsync(SalvarPerfilDto dto, CancellationToken cancellationToken = default)
    {
        ValidarCampos(dto);

        var perfil = await _perfilRepository.GetWithPermissoesAsync(dto.Id, cancellationToken)
            ?? throw new NotFoundException("Perfil", dto.Id);

        var nome = dto.Nome.Trim();
        await GarantirNomeUnicoAsync(nome, dto.Id, cancellationToken);

        var permissoesIds = await NormalizarPermissoesAsync(dto.PermissaoIds, cancellationToken);

        perfil.Nome = nome;
        perfil.Descricao = NormalizarDescricao(dto.Descricao);
        perfil.Ativo = dto.Ativo;
        perfil.DataAtualizacao = DateTime.UtcNow;

        SincronizarPermissoes(perfil, permissoesIds);

        await _perfilRepository.UpdateAsync(perfil, cancellationToken);
        return await ObterPorIdAsync(perfil.Id, cancellationToken);
    }

    public async Task AlterarStatusAtivoAsync(int id, bool ativo, CancellationToken cancellationToken = default)
    {
        var perfil = await _perfilRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Perfil", id);

        var usuarios = await _usuarioRepository.CountByPerfilIdAsync(id, cancellationToken);
        if (!ativo && usuarios > 0)
            throw new BusinessException(
                $"O perfil '{perfil.Nome}' possui {usuarios} usuário(s) vinculado(s). Desvincule-os antes de inativar.");

        perfil.Ativo = ativo;
        perfil.DataAtualizacao = DateTime.UtcNow;

        await _perfilRepository.UpdateAsync(perfil, cancellationToken);
    }

    public async Task ExcluirAsync(int id, CancellationToken cancellationToken = default)
    {
        var perfil = await _perfilRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Perfil", id);

        var usuarios = await _usuarioRepository.CountByPerfilIdAsync(id, cancellationToken);
        if (usuarios > 0)
            throw new BusinessException(
                $"O perfil '{perfil.Nome}' está vinculado a {usuarios} usuário(s) e não pode ser excluído. Inative-o ou desvincule os usuários.");

        await _perfilRepository.DeleteAsync(id, cancellationToken);
    }

    private static void ValidarCampos(SalvarPerfilDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome))
            throw new ValidationException("O nome do perfil é obrigatório.");

        if (dto.Nome.Trim().Length > NomeMaxLength)
            throw new ValidationException($"O nome do perfil deve ter no máximo {NomeMaxLength} caracteres.");

        if (!string.IsNullOrWhiteSpace(dto.Descricao) && dto.Descricao.Trim().Length > DescricaoMaxLength)
            throw new ValidationException($"A descrição deve ter no máximo {DescricaoMaxLength} caracteres.");
    }

    private async Task GarantirNomeUnicoAsync(string nome, int? ignorarId, CancellationToken cancellationToken)
    {
        var normalizado = nome.ToLowerInvariant();
        var existentes = await _perfilRepository.FindAsync(
            p => p.Nome.ToLower() == normalizado && (!ignorarId.HasValue || p.Id != ignorarId.Value),
            cancellationToken);

        if (existentes.Count > 0)
            throw new BusinessException($"Já existe um perfil chamado '{nome}'.");
    }

    /// <summary>
    /// Garante que os ids informados existam na base antes de salvar o vínculo. A tela de
    /// permissões é um próximo passo; o serviço já valida e persiste o conjunto informado.
    /// </summary>
    private async Task<List<int>> NormalizarPermissoesAsync(List<int> permissaoIds, CancellationToken cancellationToken)
    {
        var ids = permissaoIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
            return ids;

        var encontradas = await _permissaoRepository.FindAsync(p => ids.Contains(p.Id), cancellationToken);
        var encontradasIds = encontradas.Select(p => p.Id).ToHashSet();

        var faltantes = ids.Where(id => !encontradasIds.Contains(id)).ToList();
        if (faltantes.Count > 0)
            throw new ValidationException("Uma ou mais permissões selecionadas não existem na base.");

        return ids;
    }

    private static void SincronizarPermissoes(Perfil perfil, List<int> permissoesIds)
    {
        var atuais = perfil.PerfilPermissoes.ToList();

        foreach (var vinculo in atuais.Where(v => !permissoesIds.Contains(v.PermissaoId)))
        {
            perfil.PerfilPermissoes.Remove(vinculo);
        }

        foreach (var permissaoId in permissoesIds.Where(id => atuais.All(v => v.PermissaoId != id)))
        {
            perfil.PerfilPermissoes.Add(new PerfilPermissao { PerfilId = perfil.Id, PermissaoId = permissaoId });
        }
    }

    private static string? NormalizarDescricao(string? descricao)
        => string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();

    private static PerfilDto MapearParaDto(Perfil perfil, int quantidadeUsuarios)
    {
        return new PerfilDto
        {
            Id = perfil.Id,
            Nome = perfil.Nome,
            Descricao = perfil.Descricao,
            Ativo = perfil.Ativo,
            QuantidadeUsuarios = quantidadeUsuarios,
            Permissoes = perfil.PerfilPermissoes
                .Where(vinculo => vinculo.Permissao != null)
                .Select(vinculo => new PermissaoDto
                {
                    Id = vinculo.Permissao!.Id,
                    Codigo = vinculo.Permissao.Codigo,
                    Nome = vinculo.Permissao.Nome,
                    Modulo = vinculo.Permissao.Modulo,
                    Descricao = vinculo.Permissao.Descricao
                })
                .OrderBy(p => p.Modulo)
                .ThenBy(p => p.Nome)
                .ToList()
        };
    }
}

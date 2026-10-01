using OrcPro.Application.DTOs.Perfil;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Entities.Seguranca;

namespace OrcPro.Application.Services;

/// <summary>Leitura do catálogo de permissões, agrupável por módulo na tela de perfil.</summary>
public class PermissaoService : IPermissaoService
{
    private readonly IPermissaoRepository _permissaoRepository;

    public PermissaoService(IPermissaoRepository permissaoRepository)
    {
        _permissaoRepository = permissaoRepository;
    }

    public async Task<IReadOnlyList<PermissaoDto>> ListarTodasAsync(CancellationToken cancellationToken = default)
    {
        var permissoes = await _permissaoRepository.GetAllAsync(cancellationToken);
        return Mapear(permissoes);
    }

    public async Task<IReadOnlyList<PermissaoDto>> ListarAtivasAsync(CancellationToken cancellationToken = default)
    {
        var permissoes = await _permissaoRepository.FindAsync(p => p.Ativo, cancellationToken);
        return Mapear(permissoes);
    }

    private static List<PermissaoDto> Mapear(IEnumerable<Permissao> permissoes)
        => permissoes
            .Select(p => new PermissaoDto
            {
                Id = p.Id,
                Codigo = p.Codigo,
                Nome = p.Nome,
                Modulo = p.Modulo,
                Descricao = p.Descricao
            })
            .OrderBy(p => p.Modulo, StringComparer.Ordinal)
            .ThenBy(p => p.Codigo, StringComparer.Ordinal)
            .ToList();
}
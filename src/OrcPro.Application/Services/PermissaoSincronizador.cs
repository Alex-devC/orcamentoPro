using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Common;
using OrcPro.Domain.Entities.Seguranca;

namespace OrcPro.Application.Services;

/// <summary>
/// Aplica o catálogo de permissões (<see cref="PermissaoCatalogo"/>) na base:
/// 1. cria as permissões ausentes (sem duplicar códigos já existentes);
/// 2. garante que o perfil Administrador exista e possua todas as permissões.
/// Usado no seed da aplicação e seguro para executar repetidamente.
/// </summary>
public class PermissaoSincronizador : IPermissaoSincronizador
{
    private readonly IPermissaoRepository _permissaoRepository;
    private readonly IPerfilRepository _perfilRepository;

    public PermissaoSincronizador(IPermissaoRepository permissaoRepository, IPerfilRepository perfilRepository)
    {
        _permissaoRepository = permissaoRepository;
        _perfilRepository = perfilRepository;
    }

    public async Task<PermissaoSincronizacaoResultado> AplicarAsync(CancellationToken cancellationToken = default)
    {
        var existentes = await _permissaoRepository.GetAllAsync(cancellationToken);

        var criadas = 0;
        foreach (var definicao in PermissaoCatalogo.Definicoes)
        {
            var jaExiste = existentes.Any(p =>
                string.Equals(p.Codigo, definicao.Codigo, StringComparison.OrdinalIgnoreCase));

            if (jaExiste)
                continue;

            await _permissaoRepository.AddAsync(new Permissao
            {
                Codigo = definicao.Codigo,
                Nome = definicao.Nome,
                Modulo = definicao.Modulo,
                Descricao = $"{definicao.Nome} ({definicao.Codigo})",
                Ativo = true,
                DataCriacao = DateTime.UtcNow
            }, cancellationToken);

            criadas++;
        }

        // Perfil que recebe todas as permissões (criado apenas se ainda não existir).
        var nomeNormalizado = PermissaoCatalogo.PerfilAdministrador.ToLowerInvariant();
        var perfis = await _perfilRepository.FindAsync(p => p.Nome.ToLower() == nomeNormalizado, cancellationToken);

        var perfilCriado = false;
        var perfil = perfis.FirstOrDefault();

        if (perfil == null)
        {
            perfil = await _perfilRepository.AddAsync(new Perfil
            {
                Nome = PermissaoCatalogo.PerfilAdministrador,
                Descricao = "Acesso total ao sistema",
                Ativo = true,
                DataCriacao = DateTime.UtcNow
            }, cancellationToken);

            perfilCriado = true;
        }

        // Recarrega com os vínculos atuais para vincular apenas o que falta.
        var perfilComPermissoes = await _perfilRepository.GetWithPermissoesAsync(perfil.Id, cancellationToken)
            ?? perfil;

        var permissoes = await _permissaoRepository.GetAllAsync(cancellationToken);
        var vinculosAtuais = perfilComPermissoes.PerfilPermissoes
            .Select(v => v.PermissaoId)
            .ToHashSet();

        var vinculos = 0;
        foreach (var permissao in permissoes)
        {
            if (vinculosAtuais.Contains(permissao.Id))
                continue;

            perfilComPermissoes.PerfilPermissoes.Add(new PerfilPermissao
            {
                PerfilId = perfilComPermissoes.Id,
                PermissaoId = permissao.Id
            });

            vinculos++;
        }

        if (vinculos > 0)
        {
            await _perfilRepository.UpdateAsync(perfilComPermissoes, cancellationToken);
        }

        return new PermissaoSincronizacaoResultado(criadas, vinculos, perfilCriado);
    }
}
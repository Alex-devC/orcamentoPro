using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OrcPro.Application.DTOs.Perfil;
using OrcPro.Application.Exceptions;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Application.Services;
using OrcPro.Domain.Common;
using OrcPro.Domain.Entities.Seguranca;
using Xunit;

namespace OrcPro.Tests;

public class PermissaoTests
{
    [Fact]
    public void PermissaoCatalogo_DeveConterOsModulosEAsPermissoesDefinidas()
    {
        var definicoes = PermissaoCatalogo.Definicoes;

        Assert.Equal(33, definicoes.Count);

        var esperados = new[]
        {
            "DASHBOARD.VISUALIZAR",
            "CLIENTES.VISUALIZAR", "CLIENTES.CRIAR", "CLIENTES.EDITAR", "CLIENTES.EXCLUIR", "CLIENTES.ATIVAR_INATIVAR",
            "USUARIOS_PERFIS.VISUALIZAR", "USUARIOS_PERFIS.CRIAR", "USUARIOS_PERFIS.EDITAR", "USUARIOS_PERFIS.EXCLUIR",
            "USUARIOS_PERFIS.GERENCIAR_PERMISSOES",
             "TECNICOS.VISUALIZAR", "TECNICOS.CRIAR", "TECNICOS.EDITAR", "TECNICOS.EXCLUIR", "TECNICOS.ATIVAR_INATIVAR",
             "PECAS.VISUALIZAR", "PECAS.CRIAR", "PECAS.EDITAR", "PECAS.EXCLUIR", "PECAS.ATIVAR_INATIVAR",
            "SERVICOS.VISUALIZAR", "SERVICOS.CRIAR", "SERVICOS.EDITAR", "SERVICOS.EXCLUIR", "SERVICOS.ATIVAR_INATIVAR",
            "EMITENTE.VISUALIZAR", "EMITENTE.EDITAR",
            "ORCAMENTOS.VISUALIZAR", "ORCAMENTOS.CRIAR", "ORCAMENTOS.EDITAR", "ORCAMENTOS.EXCLUIR", "ORCAMENTOS.ALTERAR_STATUS"
        };

        Assert.Equal(esperados.OrderBy(c => c), definicoes.Select(d => d.Codigo).OrderBy(c => c));
    }

    [Fact]
    public void PermissaoCatalogo_NaoPodeTerCodigosDuplicados()
    {
        var codigos = PermissaoCatalogo.Definicoes.Select(d => d.Codigo).ToList();

        Assert.Equal(codigos.Count, codigos.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void PermissaoCatalogo_TodoCodigoDeveSeguirOModuloAcao()
    {
        foreach (var definicao in PermissaoCatalogo.Definicoes)
        {
            var partes = definicao.Codigo.Split('.');

            Assert.Equal(2, partes.Length);
            Assert.Equal(definicao.Modulo, partes[0]);
            Assert.False(string.IsNullOrWhiteSpace(partes[1]));
            Assert.True(PermissaoCatalogo.Existe(definicao.Codigo));
        }
    }

    [Fact]
    public void PermissaoCatalogo_CodigosDoModuloERotuloDevemFuncionar()
    {
        Assert.Equal(5, PermissaoCatalogo.CodigosDoModulo(PermissaoCatalogo.ModuloClientes).Count);
        Assert.Equal(5, PermissaoCatalogo.CodigosDoModulo(PermissaoCatalogo.ModuloUsuariosPerfis).Count);
        Assert.Equal(5, PermissaoCatalogo.CodigosDoModulo(PermissaoCatalogo.ModuloTecnicos).Count);
        Assert.Equal(5, PermissaoCatalogo.CodigosDoModulo(PermissaoCatalogo.ModuloPecas).Count);
        Assert.Equal(5, PermissaoCatalogo.CodigosDoModulo(PermissaoCatalogo.ModuloServicos).Count);
        Assert.Equal(2, PermissaoCatalogo.CodigosDoModulo(PermissaoCatalogo.ModuloEmitente).Count);
        Assert.Equal(5, PermissaoCatalogo.CodigosDoModulo(PermissaoCatalogo.ModuloOrcamentos).Count);
        Assert.Single(PermissaoCatalogo.CodigosDoModulo(PermissaoCatalogo.ModuloDashboard));
        Assert.Contains(PermissaoCatalogo.Codigos.Clientes.Criar, PermissaoCatalogo.CodigosDoModulo(PermissaoCatalogo.ModuloClientes));

        Assert.Equal("Clientes", PermissaoCatalogo.RotuloModulo(PermissaoCatalogo.ModuloClientes));
        Assert.Equal("Usuários e Perfis", PermissaoCatalogo.RotuloModulo(PermissaoCatalogo.ModuloUsuariosPerfis));
        Assert.Equal("Peças e Itens", PermissaoCatalogo.RotuloModulo(PermissaoCatalogo.ModuloPecas));
        Assert.Equal("Serviços", PermissaoCatalogo.RotuloModulo(PermissaoCatalogo.ModuloServicos));
        Assert.Equal("Minha Empresa", PermissaoCatalogo.RotuloModulo(PermissaoCatalogo.ModuloEmitente));
        Assert.Equal("Orçamentos", PermissaoCatalogo.RotuloModulo(PermissaoCatalogo.ModuloOrcamentos));
        Assert.Equal("MODULO_X", PermissaoCatalogo.RotuloModulo("MODULO_X"));
    }

    [Fact]
    public async Task Sincronizador_EmBaseVazia_DeveCriarCatalogoEAdministradorComTodas()
    {
        var permissaoRepo = new InMemoryPermissaoRepository();
        var perfilRepo = new InMemoryPerfilRepository();
        var sincronizador = new PermissaoSincronizador(permissaoRepo, perfilRepo);

        var resultado = await sincronizador.AplicarAsync();

        Assert.Equal(PermissaoCatalogo.Definicoes.Count, resultado.PermissoesCriadas);
        Assert.True(resultado.PerfilCriado);
        Assert.Equal(PermissaoCatalogo.Definicoes.Count, resultado.VinculosCriados);

        var admin = await perfilRepo.GetWithPermissoesAsync(1);
        Assert.NotNull(admin);
        Assert.Equal(PermissaoCatalogo.Definicoes.Count, admin!.PerfilPermissoes.Count);
        Assert.Equal(PermissaoCatalogo.PerfilAdministrador, admin.Nome);
    }

    [Fact]
    public async Task Sincronizador_ExecutarNovamenteNaoDuplicaNemCriaNada()
    {
        var permissaoRepo = new InMemoryPermissaoRepository();
        var perfilRepo = new InMemoryPerfilRepository();
        var sincronizador = new PermissaoSincronizador(permissaoRepo, perfilRepo);

        await sincronizador.AplicarAsync();
        var segunda = await sincronizador.AplicarAsync();

        Assert.Equal(0, segunda.PermissoesCriadas);
        Assert.Equal(0, segunda.VinculosCriados);
        Assert.False(segunda.PerfilCriado);
        Assert.False(segunda.HouveAlteracoes);

        var permissoes = await permissaoRepo.GetAllAsync();
        Assert.Equal(PermissaoCatalogo.Definicoes.Count, permissoes.Count);

        var perfis = await perfilRepo.GetAllAsync();
        Assert.Single(perfis);

        var admin = await perfilRepo.GetWithPermissoesAsync(perfis[0].Id);
        Assert.Equal(PermissaoCatalogo.Definicoes.Count, admin!.PerfilPermissoes.Count);
    }
[Fact]
    public async Task Sincronizador_PermissaoNovaNoCatalogo_DeveVincularAoAdministrador()
    {
        var permissaoRepo = new InMemoryPermissaoRepository();
        var perfilRepo = new InMemoryPerfilRepository();

        // Simula uma base já existente: administrador criado antes do novo código existir.
        await perfilRepo.AddAsync(new Perfil { Id = 1, Nome = PermissaoCatalogo.PerfilAdministrador, Ativo = true });
        await permissaoRepo.AddAsync(new Permissao { Id = 1, Codigo = "MODULO_ANTIGO.ACAO", Nome = "Antiga", Modulo = "MODULO_ANTIGO" });

        var sincronizador = new PermissaoSincronizador(permissaoRepo, perfilRepo);
        var resultado = await sincronizador.AplicarAsync();

        Assert.Equal(PermissaoCatalogo.Definicoes.Count, resultado.PermissoesCriadas);
        Assert.False(resultado.PerfilCriado);

        var admin = await perfilRepo.GetWithPermissoesAsync(1);
        Assert.Equal(PermissaoCatalogo.Definicoes.Count + 1, admin!.PerfilPermissoes.Count);
    }

    [Fact]
    public async Task PermissaoService_DeveListarOCatalogoOrdenadoPorModulo()
    {
        var permissaoRepo = new InMemoryPermissaoRepository();
        await new PermissaoSincronizador(permissaoRepo, new InMemoryPerfilRepository()).AplicarAsync();

        var service = new PermissaoService(permissaoRepo);
        var permissoes = await service.ListarTodasAsync();

        Assert.Equal(PermissaoCatalogo.Definicoes.Count, permissoes.Count);
        Assert.Equal(PermissaoCatalogo.ModuloClientes, permissoes.First().Modulo);
        Assert.All(permissoes, p => Assert.False(string.IsNullOrWhiteSpace(p.Codigo)));
    }

    [Fact]
    public async Task PerfilService_AtualizarComPermissaoDesmarcada_DeveRemoverOVinculo()
    {
        var permissaoRepo = new InMemoryPermissaoRepository();
        var perfilRepo = new InMemoryPerfilRepository();
        await new PermissaoSincronizador(permissaoRepo, perfilRepo).AplicarAsync();

        var service = new PerfilService(perfilRepo, new InMemoryUsuarioRepository(), permissaoRepo);

        var permissoes = await permissaoRepo.GetAllAsync();
        var manter = permissoes
            .Where(p => p.Codigo == PermissaoCatalogo.Codigos.Clientes.Visualizar)
            .Select(p => p.Id)
            .ToList();

        var perfil = await perfilRepo.AddAsync(new Perfil
        {
            Id = 99,
            Nome = "Operador",
            Ativo = true,
            PerfilPermissoes = permissoes.Select(p => new PerfilPermissao { PerfilId = 99, PermissaoId = p.Id }).ToList()
        });

        Assert.Equal(PermissaoCatalogo.Definicoes.Count, perfil.PerfilPermissoes.Count);

        var atualizado = await service.AtualizarAsync(new SalvarPerfilDto
        {
            Id = perfil.Id,
            Nome = "Operador",
            Ativo = true,
            PermissaoIds = manter
        });

        // A regra de negócio: permissões não marcadas devem ser removidas de PerfilPermissao.
        var recarregado = await perfilRepo.GetWithPermissoesAsync(perfil.Id);
        Assert.Single(recarregado!.PerfilPermissoes);
        Assert.Equal(manter[0], recarregado.PerfilPermissoes.First().PermissaoId);
        Assert.Equal(perfil.Id, atualizado.Id);
    }

    [Fact]
    public async Task PerfilService_CriarComPermissaoInexistente_DeveFalhar()
    {
        var permissaoRepo = new InMemoryPermissaoRepository();
        var perfilRepo = new InMemoryPerfilRepository();
        var service = new PerfilService(perfilRepo, new InMemoryUsuarioRepository(), permissaoRepo);

        await Assert.ThrowsAsync<ValidationException>(() => service.CriarAsync(new SalvarPerfilDto
        {
            Nome = "Perfil Teste",
            PermissaoIds = new List<int> { 999 }
        }));
    }
}
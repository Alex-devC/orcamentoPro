using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrcPro.Application.DTOs.Auth;
using OrcPro.Application.DTOs.Cliente;
using OrcPro.Application.DTOs.Common;
using OrcPro.Application.DTOs.Tecnico;
using OrcPro.Application.Interfaces.Services;
using OrcPro.App.ViewModels;
using OrcPro.Domain.Common;
using Xunit;

namespace OrcPro.Tests;

/// <summary>
/// Integração CEP ⇄ validação nos ViewModels: falha de consulta vira erro de campo
/// (borda vermelha + resumo no topo), alterar o CEP limpa o estado anterior e a
/// edição de cadastro existente atualiza o endereço corretamente.
/// </summary>
[Collection("CEP Log Serial")]
public class CepValidationViewModelTests
{
    private static readonly UsuarioSessaoDto AdminSessao = new()
    {
        Id = 1,
        Username = "admin",
        NomeCompleto = "Administrador",
        PerfilId = 1,
        PerfilNome = "Administrador",
        Permissoes = PermissaoCatalogo.Definicoes.Select(p => p.Codigo).ToArray()
    };

    private static readonly FakeCepService CepComDoisEnderecos = CriarCepComDoisEnderecos();

    private static FakeCepService CriarCepComDoisEnderecos()
    {
        var service = new FakeCepService();
        service.AddCepResult("01310-100", "Avenida Paulista", "Bela Vista", "São Paulo", "SP");
        service.AddCepResult("20040-000", "Avenida Rio", "Centro", "Rio de Janeiro", "RJ");
        return service;
    }

    private static ClientesViewModel CriarCliente(ICepService? cepService = null)
        => new(new StubClienteService(), AdminSessao, cepService, null);

    private static ClienteDto ClienteComEndereco() => new()
    {
        Id = 1,
        Codigo = "CLI-0001",
        NomeRazaoSocial = "Cliente Editado",
        Celular = "11999998888",
        Email = "cliente@orcpro.com.br",
        Cep = "01310100",
        Logradouro = "Avenida Paulista",
        Bairro = "Bela Vista",
        Cidade = "São Paulo",
        Uf = "SP",
        Ativo = true
    };

    #region Clientes — erro de CEP vira erro de validação

    [Fact]
    public async Task Clientes_CepNaoEncontrado_DeveMarcarCampoErroEResumo()
    {
        // FakeCepService sem CEPs configurados → toda consulta falha.
        var vm = CriarCliente(new FakeCepService());
        vm.NovoCommand.Execute(null);

        vm.FormCep = "99999999";
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);

        Assert.True(vm.CepTemErro, "CEP não encontrado deveria marcar o campo como inválido.");
        Assert.Contains("Não foi possível localizar o CEP", vm.CepMensagemErro, StringComparison.OrdinalIgnoreCase);
        Assert.True(vm.TemResumoValidacao);
        Assert.Equal(vm.CepMensagemErro, Assert.Single(vm.ResumoValidacao));
        Assert.True(vm.ErrosValidacao.ContainsKey("CEP"));
        Assert.Equal("CEP", vm.PrimeiroCampoInvalido);
        Assert.Equal(vm.CepMensagemErro, vm.EditorMensagem);
    }

    [Fact]
    public async Task Clientes_CepCorrigido_DeveLimparErroDeValidacao()
    {
        var vm = CriarCliente(CriarCepComDoisEnderecos());
        vm.NovoCommand.Execute(null);

        vm.FormCep = "99999999";
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);
        Assert.True(vm.CepTemErro);

        vm.FormCep = "01310100";
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);

        Assert.False(vm.CepTemErro, "Corrigir o CEP deveria limpar o estado de erro.");
        Assert.Equal(string.Empty, vm.CepMensagemErro);
        Assert.False(vm.TemResumoValidacao);
        Assert.Empty(vm.ResumoValidacao);
        Assert.Equal("Avenida Paulista", vm.FormLogradouro);
    }

    [Fact]
    public async Task Clientes_CepIncompletoViaLupa_DeveMarcarErroDe8Digitos()
    {
        var vm = CriarCliente(CriarCepComDoisEnderecos());
        vm.NovoCommand.Execute(null);
        vm.FormCep = "01310";

        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);

        Assert.True(vm.CepTemErro);
        Assert.Equal("O CEP deve ter 8 dígitos.", vm.CepMensagemErro);
        Assert.True(vm.TemResumoValidacao);
        Assert.Contains("O CEP deve ter 8 dígitos.", vm.ResumoValidacao);
    }

    #endregion

    #region Clientes — edição existente (causa raiz: endereço não atualizava)

    [Fact]
    public async Task Clientes_Editar_AlterarCep_DeveAtualizarEndereco()
    {
        var vm = CriarCliente(CriarCepComDoisEnderecos());

        vm.EditarCommand.Execute(ClienteComEndereco());
        Assert.Equal("Avenida Paulista", vm.FormLogradouro);

        vm.FormCep = "20040-000";
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);

        Assert.Equal("Avenida Rio", vm.FormLogradouro);
        Assert.Equal("Centro", vm.FormBairro);
        Assert.Equal("Rio de Janeiro", vm.FormCidade);
        Assert.Equal("RJ", vm.FormUf);
        Assert.False(vm.CepTemErro);
    }

    [Fact]
    public async Task Clientes_Editar_CepNaoEncontrado_NaoDeveManterEnderecoAntigo()
    {
        var vm = CriarCliente(new FakeCepService());

        vm.EditarCommand.Execute(ClienteComEndereco());
        Assert.Equal("Avenida Paulista", vm.FormLogradouro);

        vm.FormCep = "99999999";
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);

        Assert.True(vm.CepTemErro);
        Assert.Empty(vm.FormLogradouro);
        Assert.Empty(vm.FormBairro);
        Assert.Empty(vm.FormCidade);
        Assert.Empty(vm.FormUf);
    }

    [Fact]
    public async Task Clientes_Editar_LogradouroEditadoManualmente_LupaAtualizaEndereco()
    {
        var vm = CriarCliente(CriarCepComDoisEnderecos());

        vm.EditarCommand.Execute(ClienteComEndereco());
        vm.FormLogradouro = "Rua Editada Pelo Usuario";

        vm.FormCep = "20040-000";
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);

        // A lupa sempre limpa e refaz o preenchimento completo
        Assert.Equal("Avenida Rio", vm.FormLogradouro);
        Assert.Equal("Centro", vm.FormBairro);
        Assert.Equal("Rio de Janeiro", vm.FormCidade);
    }

    #endregion

    #region Clientes — resumo de validação e foco no salvamento

    [Fact]
    public async Task Clientes_SalvarInvalido_DevePreencherResumoEFocarPrimeiroCampo()
    {
        var vm = CriarCliente(new FakeCepService());
        vm.NovoCommand.Execute(null);

        var campoFocado = (string?)null;
        vm.FocoCampoSolicitado += (_, campo) => campoFocado = campo;

        vm.SalvarCommand.Execute(null);
        await Task.Delay(50);

        Assert.True(vm.TemResumoValidacao);
        Assert.Contains("Nome", vm.ErrosValidacao.Keys);
        Assert.Contains("Celular", vm.ErrosValidacao.Keys);
        Assert.Contains("Email", vm.ErrosValidacao.Keys);
        Assert.Equal(3, vm.ResumoValidacao.Count);
        Assert.Equal("Nome", vm.PrimeiroCampoInvalido);
        Assert.Equal("Nome", campoFocado);
    }

    [Fact]
    public async Task Clientes_SalvarComErroDeCep_DeveManterCepNoResumo()
    {
        var vm = CriarCliente(new FakeCepService());
        vm.NovoCommand.Execute(null);

        vm.FormCep = "99999999";
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);
        Assert.True(vm.CepTemErro);

        var campoFocado = (string?)null;
        vm.FocoCampoSolicitado += (_, campo) => campoFocado = campo;

        vm.SalvarCommand.Execute(null);
        await Task.Delay(50);

        Assert.True(vm.TemResumoValidacao);
        Assert.Contains(vm.ResumoValidacao, m => m.Contains("Não foi possível localizar o CEP"));
        Assert.Equal("CEP", vm.PrimeiroCampoInvalido);
        Assert.Equal("CEP", campoFocado);
        Assert.Equal(4, vm.ResumoValidacao.Count);
    }

    [Fact]
    public async Task Clientes_AbrirNovo_DeveLimparErrosDeValidacao()
    {
        var vm = CriarCliente(new FakeCepService());
        vm.NovoCommand.Execute(null);

        vm.FormCep = "99999999";
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);
        Assert.True(vm.CepTemErro);

        vm.NovoCommand.Execute(null);

        Assert.False(vm.CepTemErro);
        Assert.False(vm.TemResumoValidacao);
        Assert.Empty(vm.ResumoValidacao);
        Assert.Empty(vm.ErrosValidacao);
    }

    [Fact]
    public async Task Clientes_FecharEditor_DeveLimparErrosDeValidacao()
    {
        var vm = CriarCliente(new FakeCepService());
        vm.NovoCommand.Execute(null);

        vm.FormCep = "99999999";
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);
        Assert.True(vm.CepTemErro);

        vm.CancelarEditorCommand.Execute(null);

        Assert.False(vm.CepTemErro);
        Assert.False(vm.TemResumoValidacao);
        Assert.Empty(vm.ErrosValidacao);
    }

    #endregion

    #region Técnicos — mesmo fluxo de CEP/validação

    [Fact]
    public async Task Tecnicos_CepNaoEncontrado_DeveMarcarCampoErroEResumo()
    {
        var vm = CriarTecnico(new FakeCepService());
        vm.NovoCommand.Execute(null);

        vm.FormCep = "99999999";
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);

        Assert.True(vm.CepTemErro);
        Assert.Contains("Não foi possível localizar o CEP", vm.CepMensagemErro, StringComparison.OrdinalIgnoreCase);
        Assert.True(vm.TemResumoValidacao);
        Assert.Equal(vm.CepMensagemErro, Assert.Single(vm.ResumoValidacao));
        Assert.Equal("CEP", vm.PrimeiroCampoInvalido);
    }

    [Fact]
    public async Task Tecnicos_CepValido_DevePreencherEnderecoESemErro()
    {
        var vm = CriarTecnico(CriarCepComDoisEnderecos());
        vm.NovoCommand.Execute(null);

        vm.FormCep = "01310-100";
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);

        Assert.False(vm.CepTemErro);
        Assert.Equal(string.Empty, vm.CepMensagemErro);
        Assert.False(vm.TemResumoValidacao);
        Assert.Equal("Avenida Paulista", vm.FormLogradouro);
        Assert.Equal("Bela Vista", vm.FormBairro);
        Assert.Equal("São Paulo", vm.FormCidade);
        Assert.Equal("SP", vm.FormUf);
    }

    [Fact]
    public async Task Tecnicos_Editar_AlterarCep_DeveAtualizarEndereco()
    {
        var vm = CriarTecnico(CriarCepComDoisEnderecos());

        vm.EditarCommand.Execute(TecnicoComEndereco());
        Assert.Equal("Avenida Paulista", vm.FormLogradouro);

        vm.FormCep = "20040-000";
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);

        Assert.Equal("Avenida Rio", vm.FormLogradouro);
        Assert.Equal("Centro", vm.FormBairro);
        Assert.Equal("Rio de Janeiro", vm.FormCidade);
        Assert.Equal("RJ", vm.FormUf);
        Assert.False(vm.CepTemErro);
    }

    [Fact]
    public async Task Tecnicos_Editar_CepNaoEncontrado_NaoDeveManterEnderecoAntigo()
    {
        var vm = CriarTecnico(new FakeCepService());

        vm.EditarCommand.Execute(TecnicoComEndereco());
        Assert.Equal("Avenida Paulista", vm.FormLogradouro);

        vm.FormCep = "99999999";
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);

        Assert.True(vm.CepTemErro);
        Assert.Empty(vm.FormLogradouro);
        Assert.Empty(vm.FormBairro);
        Assert.Empty(vm.FormCidade);
        Assert.Empty(vm.FormUf);
    }

    [Fact]
    public async Task Tecnicos_SalvarInvalido_DevePreencherResumoEFocarNome()
    {
        var vm = CriarTecnico(new FakeCepService());
        vm.NovoCommand.Execute(null);

        var campoFocado = (string?)null;
        vm.FocoCampoSolicitado += (_, campo) => campoFocado = campo;

        vm.SalvarCommand.Execute(null);
        await Task.Delay(50);

        Assert.True(vm.TemResumoValidacao);
        Assert.Contains("Nome", vm.ErrosValidacao.Keys);
        Assert.Equal("Nome", vm.PrimeiroCampoInvalido);
        Assert.Equal("Nome", campoFocado);
    }

    [Fact]
    public async Task Tecnicos_AbrirNovo_DeveLimparErrosDeValidacao()
    {
        var vm = CriarTecnico(new FakeCepService());
        vm.NovoCommand.Execute(null);

        vm.FormCep = "99999999";
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);
        Assert.True(vm.CepTemErro);

        vm.NovoCommand.Execute(null);

        Assert.False(vm.CepTemErro);
        Assert.False(vm.TemResumoValidacao);
        Assert.Empty(vm.ErrosValidacao);
    }

    #endregion

    #region Stubs / helpers

    private static TecnicosViewModel CriarTecnico(ICepService? cepService = null)
        => new(new StubTecnicoService(), AdminSessao, cepService, null);

    private static TecnicoDto TecnicoComEndereco() => new()
    {
        Id = 1,
        Codigo = "TEC-0001",
        Nome = "Técnico Editado",
        Celular = "11999997777",
        Email = "tecnico@orcpro.com.br",
        Cep = "01310100",
        Logradouro = "Avenida Paulista",
        Bairro = "Bela Vista",
        Cidade = "São Paulo",
        Uf = "SP",
        Ativo = true
    };

    /// <summary>Stub de IClienteService: não interfere nos testes de CEP.</summary>
    private sealed class StubClienteService : IClienteService
    {
        public Task<ClienteDto> ObterPorIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<ClienteDto>(null!);

        public Task<ClienteDto?> ObterPorCpfCnpjAsync(string cpfCnpj, CancellationToken cancellationToken = default)
            => Task.FromResult<ClienteDto?>(null);

        public Task<PagedResult<ClienteDto>> ListarPaginadoAsync(PagedRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(PagedResult<ClienteDto>.Empty());

        public Task<IReadOnlyList<ClienteDto>> ListarTodosAtivosAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ClienteDto>>(Array.Empty<ClienteDto>());

        public Task<ClienteDto> CriarAsync(CriarClienteDto dto, CancellationToken cancellationToken = default)
            => Task.FromResult<ClienteDto>(null!);

        public Task<ClienteDto> AtualizarAsync(AtualizarClienteDto dto, CancellationToken cancellationToken = default)
            => Task.FromResult<ClienteDto>(null!);

        public Task InativarAsync(int id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ExcluirAsync(int id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    /// <summary>Stub de ITecnicoService: não interfere nos testes de CEP.</summary>
    private sealed class StubTecnicoService : ITecnicoService
    {
        public Task<TecnicoDto> ObterPorIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<TecnicoDto>(null!);

        public Task<PagedResult<TecnicoDto>> ListarPaginadoAsync(PagedRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(PagedResult<TecnicoDto>.Empty());

        public Task<IReadOnlyList<TecnicoDto>> ListarTodosAtivosAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TecnicoDto>>(Array.Empty<TecnicoDto>());

        public Task<TecnicoDto> CriarAsync(CriarTecnicoDto dto, CancellationToken cancellationToken = default)
            => Task.FromResult<TecnicoDto>(null!);

        public Task<TecnicoDto> AtualizarAsync(AtualizarTecnicoDto dto, CancellationToken cancellationToken = default)
            => Task.FromResult<TecnicoDto>(null!);

        public Task InativarAsync(int id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ExcluirAsync(int id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    #endregion
}

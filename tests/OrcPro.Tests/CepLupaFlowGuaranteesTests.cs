using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrcPro.Application.DTOs.Auth;
using OrcPro.Application.DTOs.Cliente;
using OrcPro.Application.DTOs.Common;
using OrcPro.Application.DTOs.Tecnico;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Application.Services;
using OrcPro.App.ViewModels;
using OrcPro.Domain.Common;
using OrcPro.Domain.Common.Formatters;
using Xunit;

namespace OrcPro.Tests;

/// <summary>
/// Garantias do fluxo de CEP: NENHUMA consulta é disparada automaticamente.
/// A única porta de entrada é o clique na lupa.
/// </summary>
public class CepLupaFlowGuaranteesTests
{
    private static readonly UsuarioSessaoDto AdminSessao = new()
    {
        Id = 1,
        Username = "admin",
        NomeCompleto = "Administrador",
        PerfilId = 1,
        Permissoes = PermissaoCatalogo.Definicoes.Select(p => p.Codigo).ToArray()
    };

    private static ContadorCepService CriarServico(string cep, string logradouro, string bairro, string cidade, string uf)
    {
        var service = new ContadorCepService();
        service.AddCepResult(cep, logradouro, bairro, cidade, uf);
        return service;
    }

    private static ClientesViewModel CriarCliente(ICepService service)
        => new(new StubClienteService(), AdminSessao, service, null);

    private static void AbrirFormulario(ClientesViewModel vm) => vm.NovoCommand.Execute(null);

    public CepLupaFlowGuaranteesTests()
    {
    }

    #region Digitar / Apagar não dispara consulta

    [Fact]
    public async Task Digitar_Cep_8Digitos_NaoDisparaConsulta()
    {
        var service = CriarServico("01310-100", "Av. Paulista", "Bela Vista", "São Paulo", "SP");
        var vm = CriarCliente(service);
        AbrirFormulario(vm);

        vm.FormCep = "01310-100";
        await Task.Delay(30);

        Assert.Equal(0, service.NumeroConsultas);
    }

    [Fact]
    public async Task Apagar_Cep_NaoDisparaConsulta()
    {
        var service = CriarServico("01310-100", "Av. Paulista", "Bela Vista", "São Paulo", "SP");
        var vm = CriarCliente(service);
        AbrirFormulario(vm);

        vm.FormCep = "01310-100";
        await Task.Delay(10);

        vm.FormCep = string.Empty;
        await Task.Delay(30);

        Assert.Equal(0, service.NumeroConsultas);
    }

    [Fact]
    public async Task Digitar_Lentamente_NaoDisparaConsulta()
    {
        var service = CriarServico("01310-100", "Av. Paulista", "Bela Vista", "São Paulo", "SP");
        var vm = CriarCliente(service);
        AbrirFormulario(vm);

        vm.FormCep = "01";
        await Task.Delay(10);
        vm.FormCep = "013";
        await Task.Delay(10);
        vm.FormCep = "01310";
        await Task.Delay(10);
        vm.FormCep = "01310-100";
        await Task.Delay(30);

        Assert.Equal(0, service.NumeroConsultas);
    }

    [Fact]
    public async Task Digitar_DigitoPorVez_NaoDisparaConsulta()
    {
        var service = CriarServico("01310100", "Av. Paulista", "Bela Vista", "São Paulo", "SP");
        var vm = CriarCliente(service);
        AbrirFormulario(vm);

        foreach (var d in "01310100")
        {
            vm.FormCep += d;
            await Task.Delay(5);
        }
        await Task.Delay(30);

        Assert.Equal(0, service.NumeroConsultas);
    }

    #endregion

    #region Apenas lupa dispara consulta

    [Fact]
    public async Task Lupa_DisparaExatamenteUmaConsulta()
    {
        var service = CriarServico("01310-100", "Av. Paulista", "Bela Vista", "São Paulo", "SP");
        var vm = CriarCliente(service);
        AbrirFormulario(vm);

        vm.FormCep = "01310-100";
        await Task.Delay(10);

        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);

        Assert.Equal(1, service.NumeroConsultas);
    }

    [Fact]
    public async Task Lupa_DuasVezesMesmoCep_DuasConsultas()
    {
        var service = CriarServico("01310-100", "Av. Paulista", "Bela Vista", "São Paulo", "SP");
        var vm = CriarCliente(service);
        AbrirFormulario(vm);

        vm.FormCep = "01310-100";

        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);

        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);

        Assert.Equal(2, service.NumeroConsultas);
    }

    [Fact]
    public async Task Lupa_CepInexistente_DisparaConsulta()
    {
        var service = new ContadorCepService();
        var vm = CriarCliente(service);
        AbrirFormulario(vm);

        vm.FormCep = "99999999";
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);

        Assert.Equal(1, service.NumeroConsultas);
    }

    #endregion

    #region PropertyChanged verificado para cada propriedade

    [Fact]
    public async Task ConsultaComSucesso_DisparaPropertyChanged_EmTodosOsCampos()
    {
        var service = CriarServico("01310-100", "Av. Paulista", "Bela Vista", "São Paulo", "SP");
        var vm = CriarCliente(service);
        AbrirFormulario(vm);

        var propriedadesNotificadas = new List<string>();
        vm.PropertyChanged += (_, e) => propriedadesNotificadas.Add(e.PropertyName ?? string.Empty);

        vm.FormCep = "01310-100";
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);

        Assert.Contains("FormLogradouro", propriedadesNotificadas);
        Assert.Contains("FormBairro", propriedadesNotificadas);
        Assert.Contains("FormCidade", propriedadesNotificadas);
        Assert.Contains("FormUf", propriedadesNotificadas);
        Assert.Contains("CepPodeConsultarManualmente", propriedadesNotificadas);

        Assert.Equal("Av. Paulista", vm.FormLogradouro);
        Assert.Equal("Bela Vista", vm.FormBairro);
        Assert.Equal("São Paulo", vm.FormCidade);
        Assert.Equal("SP", vm.FormUf);
    }

    [Fact]
    public async Task ConsultaComSucesso_AtualizaImediatamente_TodosOsCampos()
    {
        var service = CriarServico("01310-100", "Av. Paulista", "Bela Vista", "São Paulo", "SP");
        var vm = CriarCliente(service);
        AbrirFormulario(vm);

        // Captura PropertyChanged para confirmar notificação pós-aplicação
        var changes = new List<(string Prop, string Valor)>();
        vm.PropertyChanged += (_, e) =>
        {
                if (e.PropertyName is not null &&
                (e.PropertyName == nameof(ClientesViewModel.FormLogradouro) ||
                 e.PropertyName == nameof(ClientesViewModel.FormBairro) ||
                 e.PropertyName == nameof(ClientesViewModel.FormCidade) ||
                 e.PropertyName == nameof(ClientesViewModel.FormUf)))
            {
                changes.Add((e.PropertyName, vm.GetType()
                    .GetProperty(e.PropertyName)?.GetValue(vm)?.ToString() ?? ""));
            }
        };

        vm.FormCep = "01310-100";
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);

        Assert.Contains(changes, c => c.Prop == "FormLogradouro" && c.Valor == "Av. Paulista");
        Assert.Contains(changes, c => c.Prop == "FormBairro" && c.Valor == "Bela Vista");
        Assert.Contains(changes, c => c.Prop == "FormCidade" && c.Valor == "São Paulo");
        Assert.Contains(changes, c => c.Prop == "FormUf" && c.Valor == "SP");
    }

    #endregion

    #region Campos endereço atualizados imediatamente

    [Fact]
    public async Task ConsultaComSucesso_Endereco_AtualizadoImediatamente()
    {
        var service = CriarServico("01310-100", "Av. Paulista", "Bela Vista", "São Paulo", "SP");
        var vm = CriarCliente(service);
        AbrirFormulario(vm);

        vm.FormCep = "01310-100";
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);

        Assert.Equal("Av. Paulista", vm.FormLogradouro);
    }

    [Fact]
    public async Task ConsultaComSucesso_Bairro_AtualizadoImediatamente()
    {
        var service = CriarServico("01310-100", "Av. Paulista", "Bela Vista", "São Paulo", "SP");
        var vm = CriarCliente(service);
        AbrirFormulario(vm);

        vm.FormCep = "01310-100";
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);

        Assert.Equal("Bela Vista", vm.FormBairro);
    }

    [Fact]
    public async Task ConsultaComSucesso_Cidade_AtualizadoImediatamente()
    {
        var service = CriarServico("01310-100", "Av. Paulista", "Bela Vista", "São Paulo", "SP");
        var vm = CriarCliente(service);
        AbrirFormulario(vm);

        vm.FormCep = "01310-100";
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);

        Assert.Equal("São Paulo", vm.FormCidade);
    }

    [Fact]
    public async Task ConsultaComSucesso_Uf_AtualizadoImediatamente()
    {
        var service = CriarServico("01310-100", "Av. Paulista", "Bela Vista", "São Paulo", "SP");
        var vm = CriarCliente(service);
        AbrirFormulario(vm);

        vm.FormCep = "01310-100";
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);

        Assert.Equal("SP", vm.FormUf);
    }

    [Fact]
    public async Task ConsultaComSucesso_NaoRequerSalvarERabrir_ParaVerResultado()
    {
        var service = CriarServico("01310-100", "Av. Paulista", "Bela Vista", "São Paulo", "SP");
        var vm = CriarCliente(service);
        AbrirFormulario(vm);

        vm.FormCep = "01310-100";
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);

        // Imediatamente após a lupa, os campos já estão preenchidos — sem salvar/reabrir
        Assert.Equal("Av. Paulista", vm.FormLogradouro);
        Assert.Equal("Bela Vista", vm.FormBairro);
        Assert.Equal("São Paulo", vm.FormCidade);
        Assert.Equal("SP", vm.FormUf);
    }

    #endregion

    #region Stubs

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

    /// <summary>ICepService que conta chamadas e retorna resultados configuráveis.</summary>
    private sealed class ContadorCepService : ICepService
    {
        private readonly Dictionary<string, CepAddressResult> _results = new();

        public string ProviderName => "Contador";
        public int NumeroConsultas { get; private set; }

        public void AddCepResult(string cep, string? logradouro, string? bairro, string? cidade, string? uf)
        {
            var normalized = CepMaskHelper.Normalizar(cep);
            _results[normalized] = CepAddressResult.SuccessResult(
                normalized, logradouro, bairro, cidade, uf, source: ProviderName);
        }

        public Task<CepAddressResult> ConsultarAsync(string cep, CancellationToken cancellationToken = default)
        {
            NumeroConsultas++;
            var normalized = CepMaskHelper.Normalizar(cep);

            if (_results.TryGetValue(normalized, out var result))
                return Task.FromResult(result);

            return Task.FromResult(CepAddressResult.FailureResult(
                normalized, "CEP não encontrado", ProviderName));
        }
    }

    #endregion
}

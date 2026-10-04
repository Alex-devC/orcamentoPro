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
using OrcPro.Domain.Common.Formatters;
using Xunit;

namespace OrcPro.Tests;

/// <summary>
/// Fluxo da lupa de CEP (Consulta manual) com os CEPs obrigatórios 15130-010 e 15085-520:
/// 1) clicar na lupa executa o comando; 2) o serviço é chamado; 3) o resultado é recebido;
/// 4) o endereço é aplicado; 5) a lupa SEMPRE executa consulta real (A → B → A), ignorando
/// o cache.
/// </summary>
public class CepLupaManualFlowTests
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

    private static ContadorCepService CriarServicoComOsDoisCeps()
    {
        var service = new ContadorCepService();
        service.AddCepResult("15130-010", "Rua Coronel Paulino", "Centro", "Mirassol", "SP");
        service.AddCepResult("15085-520", "Rua das Acácias", "Jardim Inga", "São José dos Campos", "SP");
        return service;
    }

    private static ClientesViewModel CriarCliente(ICepService service)
        => new(new StubClienteService(), AdminSessao, service, null);

    private static TecnicosViewModel CriarTecnico(ICepService service)
        => new(new StubTecnicoService(), AdminSessao, service, null);

    #region Sequência A → B → A — a lupa sempre consulta de verdade

    [Fact]
    public async Task Clientes_Lupa_A_B_A_SempreExecutaConsultaReal()
    {
        var service = CriarServicoComOsDoisCeps();
        var vm = CriarCliente(service);
        vm.NovoCommand.Execute(null);

        // --- 1) 15130-010: digitar NÃO consulta, lupa consulta (1ª) ---
        vm.FormCep = "15130-010";
        await Task.Delay(50);
        Assert.Equal(0, service.NumeroConsultas); // nada automático

        vm.ConsultarCepCommand.Execute(null);
        await Aguardar(() => service.NumeroConsultas >= 1);
        Assert.Equal(1, service.NumeroConsultas);
        Assert.Equal("Rua Coronel Paulino", vm.FormLogradouro);
        Assert.Equal("Centro", vm.FormBairro);
        Assert.Equal("Mirassol", vm.FormCidade);
        Assert.Equal("SP", vm.FormUf);
        Assert.False(vm.CepTemErro);

        // --- 2) 15085-520: lupa (2ª) ---
        vm.FormCep = "15085-520";
        await Task.Delay(50);
        Assert.Equal(1, service.NumeroConsultas); // nada automático

        vm.ConsultarCepCommand.Execute(null);
        await Aguardar(() => service.NumeroConsultas >= 2);
        Assert.Equal(2, service.NumeroConsultas);
        Assert.Equal("Rua das Acácias", vm.FormLogradouro);
        Assert.Equal("Jardim Inga", vm.FormBairro);
        Assert.Equal("São José dos Campos", vm.FormCidade);
        Assert.Equal("SP", vm.FormUf);
        Assert.False(vm.CepTemErro);

        // --- 3) volta para 15130-010: lupa (3ª) — sempre consulta real ---
        vm.FormCep = "15130-010";
        await Task.Delay(50);
        Assert.Equal(2, service.NumeroConsultas); // nada automático

        vm.ConsultarCepCommand.Execute(null);
        await Aguardar(() => service.NumeroConsultas >= 3);

        // A lupa é uma consulta explícita: 3 consultas ao todo (uma por clique).
        Assert.Equal(3, service.NumeroConsultas);
        Assert.Equal("Rua Coronel Paulino", vm.FormLogradouro);
        Assert.Equal("Mirassol", vm.FormCidade);
        Assert.Equal("SP", vm.FormUf);
        Assert.False(vm.CepTemErro);
        Assert.Equal(string.Empty, vm.CepMensagemErro);
    }

    [Fact]
    public async Task Tecnicos_Lupa_A_B_A_SempreExecutaConsultaReal()
    {
        var service = CriarServicoComOsDoisCeps();
        var vm = CriarTecnico(service);
        vm.NovoCommand.Execute(null);

        vm.FormCep = "15130-010";
        await Task.Delay(50);
        Assert.Equal(0, service.NumeroConsultas); // nada automático

        vm.ConsultarCepCommand.Execute(null);
        await Aguardar(() => service.NumeroConsultas >= 1);
        Assert.Equal(1, service.NumeroConsultas);
        Assert.Equal("Mirassol", vm.FormCidade);

        vm.FormCep = "15085-520";
        await Task.Delay(50);
        Assert.Equal(1, service.NumeroConsultas); // nada automático

        vm.ConsultarCepCommand.Execute(null);
        await Aguardar(() => service.NumeroConsultas >= 2);
        Assert.Equal(2, service.NumeroConsultas);
        Assert.Equal("São José dos Campos", vm.FormCidade);

        vm.FormCep = "15130-010";
        await Task.Delay(50);
        Assert.Equal(2, service.NumeroConsultas); // nada automático

        vm.ConsultarCepCommand.Execute(null);
        await Aguardar(() => service.NumeroConsultas >= 3);
        Assert.Equal(3, service.NumeroConsultas);
        Assert.Equal("Mirassol", vm.FormCidade);
        Assert.Equal("SP", vm.FormUf);
        Assert.False(vm.CepTemErro);
    }

    #endregion

    #region Helpers

    /// <summary>Aguarda (polling) até a condição ser verdadeira ou estourar o tempo.</summary>
    private static async Task Aguardar(Func<bool> condicao, int timeoutMs = 3000)
    {
        var fim = DateTime.Now.AddMilliseconds(timeoutMs);
        while (DateTime.Now < fim)
        {
            if (condicao())
                return;
            await Task.Delay(25);
        }

        Assert.True(condicao(), "Condição não atingida dentro do tempo limite.");
    }

    #endregion

    #region Stubs / fakes

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

    /// <summary>ICepService que conta cada chamada e retorna resultados configuráveis.</summary>
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

using System;
using System.Collections.Generic;
using System.IO;
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
/// o cache; 6) toda a sequência é registrada em logcep.txt (borda vermelha/resumo em
/// CepValidationViewModelTests).
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

        // --- 1) 15130-010: digitação dispara a consulta AUTOMÁTICA (1ª) ---
        vm.FormCep = "15130-010";
        await Aguardar(() => service.NumeroConsultas >= 1);
        Assert.Equal(1, service.NumeroConsultas);
        Assert.Equal("Mirassol", vm.FormCidade);

        // --- lupa: consulta MANUAL real (2ª), ignorando o cache ---
        vm.ConsultarCepCommand.Execute(null);
        await Aguardar(() => service.NumeroConsultas >= 2);
        Assert.Equal(2, service.NumeroConsultas);
        Assert.Equal("Rua Coronel Paulino", vm.FormLogradouro);
        Assert.Equal("Centro", vm.FormBairro);
        Assert.Equal("Mirassol", vm.FormCidade);
        Assert.Equal("SP", vm.FormUf);
        Assert.False(vm.CepTemErro);

        // --- 2) 15085-520: automática (3ª) + lupa (4ª) ---
        vm.FormCep = "15085-520";
        await Aguardar(() => service.NumeroConsultas >= 3);
        Assert.Equal(3, service.NumeroConsultas);
        Assert.Equal("São José dos Campos", vm.FormCidade);

        vm.ConsultarCepCommand.Execute(null);
        await Aguardar(() => service.NumeroConsultas >= 4);
        Assert.Equal(4, service.NumeroConsultas);
        Assert.Equal("Rua das Acácias", vm.FormLogradouro);
        Assert.Equal("Jardim Inga", vm.FormBairro);
        Assert.Equal("São José dos Campos", vm.FormCidade);
        Assert.Equal("SP", vm.FormUf);
        Assert.False(vm.CepTemErro);

        // --- 3) volta para 15130-010: automática (5ª) + lupa (6ª) ---
        vm.FormCep = "15130-010";
        await Aguardar(() => service.NumeroConsultas >= 5);
        Assert.Equal(5, service.NumeroConsultas);
        Assert.Equal("Mirassol", vm.FormCidade);

        vm.ConsultarCepCommand.Execute(null);
        await Aguardar(() => service.NumeroConsultas >= 6);

        // A lupa é uma consulta explícita: 6 consultas ao todo (3 automáticas + 3 manuais).
        Assert.Equal(6, service.NumeroConsultas);
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
        await Aguardar(() => service.NumeroConsultas >= 1);
        vm.ConsultarCepCommand.Execute(null);
        await Aguardar(() => service.NumeroConsultas >= 2);
        Assert.Equal(2, service.NumeroConsultas);
        Assert.Equal("Mirassol", vm.FormCidade);

        vm.FormCep = "15085-520";
        await Aguardar(() => service.NumeroConsultas >= 3);
        vm.ConsultarCepCommand.Execute(null);
        await Aguardar(() => service.NumeroConsultas >= 4);
        Assert.Equal(4, service.NumeroConsultas);
        Assert.Equal("São José dos Campos", vm.FormCidade);

        vm.FormCep = "15130-010";
        await Aguardar(() => service.NumeroConsultas >= 5);
        vm.ConsultarCepCommand.Execute(null);
        await Aguardar(() => service.NumeroConsultas >= 6);
        Assert.Equal(6, service.NumeroConsultas);
        Assert.Equal("Mirassol", vm.FormCidade);
        Assert.Equal("SP", vm.FormUf);
        Assert.False(vm.CepTemErro);
    }

    #endregion

    #region Diagnóstico em logcep.txt

    [Fact]
    public async Task Clientes_Lupa_DeveRegistrarSequenciaCompletaNoLog()
    {
        var service = CriarServicoComOsDoisCeps();
        var vm = CriarCliente(service);
        vm.NovoCommand.Execute(null);

        vm.FormCep = "15130-010";
        await Aguardar(() => service.NumeroConsultas >= 1);
        vm.ConsultarCepCommand.Execute(null);
        await Aguardar(() => service.NumeroConsultas >= 2);

        var log = AguardarConteudoLog("[CEP] BOTÃO LUPA CLICADO");

        // Lupa → comando → serviço (nível ViewModel)
        Assert.Contains("[CEP] BOTÃO LUPA CLICADO", log);
        Assert.Contains("[CEP] Valor atual do campo: '15130-010'", log);
        Assert.Contains("[CEP] ConsultarCepCommand iniciado", log);
        Assert.Contains("[CEP] Serviço disponível: true", log);
        Assert.Contains("[CEP] Origem da consulta: MANUAL", log);
        Assert.Contains("[CEP] Consulta manual — IGNORANDO CACHE.", log);
        Assert.Contains("[CEP] Chamando ICepService.ConsultarAsync", log);

        // Aplicação do resultado (ViewModel → formulário)
        Assert.Contains("[APLICAÇÃO] Origem: MANUAL", log);
        Assert.Contains("[APLICAÇÃO] Success = true", log);
        Assert.Contains("[APLICAÇÃO] CEP retornado: 15130-010", log);
        Assert.Contains("[APLICAÇÃO] Logradouro novo: 'Rua Coronel Paulino'", log);
        Assert.Contains("[APLICAÇÃO] Cidade nova: 'Mirassol'", log);
        Assert.Contains("[APLICAÇÃO] UF nova: 'SP'", log);
        Assert.Contains("[APLICAÇÃO] Endereço aplicado ao formulário.", log);

        // Consulta automática também é registrada com a origem
        Assert.Contains("[CEP] Origem da consulta: AUTOMÁTICA", log);
    }

    [Fact]
    public async Task Clientes_Lupa_CepInexistente_DeveRegistrarFalhaEInvalidarCampo()
    {
        var service = new ContadorCepService(); // sem CEPs configurados → falha
        var vm = CriarCliente(service);
        vm.NovoCommand.Execute(null);

        vm.FormCep = "15130-010";
        vm.ConsultarCepCommand.Execute(null);
        await Aguardar(() => service.NumeroConsultas >= 2);

        Assert.True(vm.CepTemErro, "Falha da lupa deve deixar o CEP em estado inválido (borda vermelha).");
        Assert.Contains("Não foi possível localizar o CEP", vm.CepMensagemErro, StringComparison.OrdinalIgnoreCase);
        Assert.True(vm.TemResumoValidacao);

        var log = AguardarConteudoLog("[APLICAÇÃO] Success = false");
        Assert.Contains("[APLICAÇÃO] Success = false", log);
        Assert.Contains("[APLICAÇÃO] Erro registrado na infraestrutura de validação", log);
    }

    /// <summary>Lê logcep.txt (ao lado do executável de testes) com pequenas novas tentativas.</summary>
    private static string AguardarConteudoLog(string marcador, int timeoutMs = 3000)
    {
        var caminho = Path.Combine(AppContext.BaseDirectory, "logcep.txt");
        var fim = DateTime.Now.AddMilliseconds(timeoutMs);
        string conteudo = string.Empty;

        while (DateTime.Now < fim)
        {
            try
            {
                conteudo = File.ReadAllText(caminho);
                if (conteudo.Contains(marcador, StringComparison.Ordinal))
                    return conteudo;
            }
            catch (IOException)
            {
                // Escrita concorrente ou arquivo ainda não criado: tenta de novo.
            }

            Thread.Sleep(50);
        }

        return conteudo;
    }

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

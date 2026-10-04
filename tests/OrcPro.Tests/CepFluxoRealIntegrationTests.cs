using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrcPro.Application.DTOs.Auth;
using OrcPro.Application.DTOs.Cliente;
using OrcPro.Application.DTOs.Common;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Application.Services;
using OrcPro.App.ViewModels;
using OrcPro.Domain.Common;
using OrcPro.Infrastructure.Services;
using Xunit;
using Xunit.Abstractions;

namespace OrcPro.Tests;

/// <summary> ... </summary>
public class CepFluxoRealIntegrationTests
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

    private readonly ITestOutputHelper _output;

    public CepFluxoRealIntegrationTests(ITestOutputHelper output)
        => _output = output;

    [Fact]
    public async Task Lupa_ComServicoReal_A_B_A_DeveTratarRespostaDoProvedorERegistrarLog()
    {
        // 0) Verifica conectividade com o provedor usando um CEP que EXISTE na base.
        var servico = new ViaCepService();
        var preTeste = await servico.ConsultarAsync("15085-520");
        if (!preTeste.Success && EhFalhaDeConectividade(preTeste.ErrorMessage))
        {
            _output.WriteLine("INCONCLUSIVO: ambiente sem conectividade com o ViaCEP.");
            _output.WriteLine($"Erro: {preTeste.ErrorMessage}");
            return;
        }

        if (!preTeste.Success)
        {
            Assert.Fail($"Provedor falhou sem conectividade no CEP 15085-520: {preTeste.ErrorMessage}.");
        }

        var vm = new ClientesViewModel(new StubClienteService(), AdminSessao, servico, null);
        vm.NovoCommand.Execute(null);

        // 1) A = 15130-010: lupa consulta real e valida EXATAMENTE o que o provedor responder.
        vm.FormCep = "15130-010";
        if (!await ExecutarLupaEConcluir(vm, "15130-010 (lupa)"))
            return;
        ValidarRespostaDe15130_010(vm);

        // 2) B = 15085-520: EXISTE na base do provedor → endereço é OBRIGATÓRIO.
        vm.FormCep = "15085-520";
        if (!await ExecutarLupaEConcluir(vm, "15085-520 (lupa)"))
            return;
        AssertEnderecoAplicado(vm, "15085-520");
        _output.WriteLine($"15085-520 OK → {vm.FormLogradouro} | {vm.FormCidade}/{vm.FormUf}");

        // 3) Volta para A = 15130-010: nova consulta real, mesmo tratamento do passo 1.
        vm.FormCep = "15130-010";
        if (!await ExecutarLupaEConcluir(vm, "15130-010 volta (lupa)"))
            return;
        ValidarRespostaDe15130_010(vm);
    }

    /// <summary>
    /// Valida o tratamento do CEP 15130-010 de acordo com a RESPOSTA OFICIAL do provedor:
    /// - endereço aplicado (se o provedor encontrar); ou
    /// - estado inválido na infraestrutura de validação + endereço antigo descartado
    ///   (se o provedor responder o campo "erro" — estado inválido na UI).
    /// </summary>
    private void ValidarRespostaDe15130_010(ClientesViewModel vm)
    {
        if (vm.CepTemErro)
        {
            _output.WriteLine("15130-010 → provedor respondeu \"não encontrado\": exatamente isso deve estar na UI.");
            Assert.False(string.IsNullOrWhiteSpace(vm.CepMensagemErro), "Mensagem de erro do CEP deveria estar preenchida.");
            Assert.True(vm.TemResumoValidacao, "O erro do CEP deveria aparecer no resumo de validação.");
            Assert.True(string.IsNullOrWhiteSpace(vm.FormLogradouro),
                "Endereço antigo NÃO deve permanecer como pertencente ao CEP rejeitado.");
            _output.WriteLine($"Estado: {vm.CepMensagemErro}");
        }
        else
        {
            _output.WriteLine("15130-010 → provedor ENCONTROU o CEP nesta consulta.");
            AssertEnderecoAplicado(vm, "15130-010");
            _output.WriteLine($"Estado: {vm.FormLogradouro} | {vm.FormCidade}/{vm.FormUf}");
        }
    }

    #region Helpers de espera / diagnóstico

    /// <summary>
    /// Espera a consulta em andamento concluir (via lupa).
    /// Retorna false quando deve abortar o teste por INCONCLUSIVIDADE (sem rede);
    /// lança Assert.Fail quando há falha de implementação com rede disponível.
    /// </summary>
    private async Task<bool> ConcluirOuAbortar(ClientesViewModel vm, string contexto, int timeoutMs = 20000)
    {
        var fim = DateTime.Now.AddMilliseconds(timeoutMs);
        while (DateTime.Now < fim)
        {
            // Concluiu quando parou de carregar E aplicou endereço OU marcou erro.
            if (!vm.CepCarregando && (vm.CepTemErro || !string.IsNullOrWhiteSpace(vm.FormCidade)))
                return true;

            await Task.Delay(100);
        }

        _output.WriteLine($"Timeout em '{contexto}': CepCarregando={vm.CepCarregando}, " +
                          $"CepTemErro={vm.CepTemErro}, Msg='{vm.CepMensagemErro}', Cidade='{vm.FormCidade}'");

        // Distingue "sem rede" de "erro de implementação":
        var diagnostico = await new ViaCepService().ConsultarAsync("15130-010");
        if (!diagnostico.Success && EhFalhaDeConectividade(diagnostico.ErrorMessage))
        {
            _output.WriteLine("INCONCLUSIVO: ambiente sem conectividade com o ViaCEP.");
            return false;
        }

        if (!diagnostico.Success)
            Assert.Fail($"Provedor falhou sem conectividade durante o teste: {diagnostico.ErrorMessage}.");

        Assert.Fail($"Fluxo '{contexto}' não concluiu com a rede disponível.");
        return false; // inalcançável — Assert.Fail lança
    }

    /// <summary>Clica na lupa (comando) e aguarda a consulta manual terminar.</summary>
    private async Task<bool> ExecutarLupaEConcluir(ClientesViewModel vm, string contexto)
    {
        vm.ConsultarCepCommand.Execute(null);
        return await ConcluirOuAbortar(vm, contexto);
    }

    private void AssertEnderecoAplicado(ClientesViewModel vm, string cep)
    {
        Assert.False(vm.CepTemErro, $"CEP {cep} com erro: {vm.CepMensagemErro}");
        Assert.False(string.IsNullOrWhiteSpace(vm.FormLogradouro), $"Logradouro do {cep} deveria ser aplicado.");
        Assert.False(string.IsNullOrWhiteSpace(vm.FormCidade), $"Cidade do {cep} deveria ser aplicada.");
        Assert.False(string.IsNullOrWhiteSpace(vm.FormUf), $"UF do {cep} deveria ser aplicada.");
    }

    /// <summary>
    /// Considera falha de conectividade APENAS mensagens conhecidas de rede/timeout.
    /// Erros inesperados NÃO são tratados como offline (não mascarar implementação).
    /// </summary>
    private static bool EhFalhaDeConectividade(string? mensagem)
    {
        var erro = mensagem ?? string.Empty;
        return erro.Contains("Erro de rede", StringComparison.OrdinalIgnoreCase)
            || erro.Contains("Timeout", StringComparison.OrdinalIgnoreCase)
            || erro.Contains("cancelada", StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Stub

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

    #endregion
}

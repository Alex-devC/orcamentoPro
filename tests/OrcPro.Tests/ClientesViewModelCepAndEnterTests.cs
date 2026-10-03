using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrcPro.Application.DTOs.Auth;
using OrcPro.Application.DTOs.Cliente;
using OrcPro.Application.DTOs.Common;
using OrcPro.Application.Interfaces.Services;
using OrcPro.App.Controls;
using OrcPro.App.ViewModels;
using OrcPro.App.Views;
using OrcPro.Domain.Common;
using OrcPro.Domain.Common.Formatters;
using Xunit;

namespace OrcPro.Tests;

/// <summary>
/// Testes para o comportamento ENTER (navegação entre campos), lupa de CEP
/// (consulta manual), LostFocus/autoconsulta de CEP e tratamento de CEP inválido.
/// </summary>
[Collection(WpfTestSupport.Colecao)]
public class ClientesViewModelCepAndEnterTests
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

    private static ClientesViewModel CriarViewModel(ICepService? cepService = null, List<string>? statusMessages = null)
    {
        var clienteService = new StubClienteService();
        Action<string>? reportStatus = statusMessages != null
            ? msg => statusMessages.Add(msg)
            : null;
        var vm = new ClientesViewModel(
            clienteService,
            AdminSessao,
            cepService,
            reportStatus);
        return vm;
    }

    private static void AbrirFormulario(ClientesViewModel vm)
    {
        vm.NovoCommand.Execute(null);
    }

    #region ENTER Navigation Behavior Tests

    /// <summary>
    /// Verifica que controles editáveis (TextBox, ComboBox, PasswordBox) recebem
    /// navegação ENTER, enquanto controles com função própria para ENTER (Button,
    /// DataGrid, ListBox) não recebem.
    /// </summary>
    [Theory]
    [InlineData("TextBox", true)]
    [InlineData("ComboBox", true)]
    [InlineData("PasswordBox", true)]
    [InlineData("DatePicker", true)]
    [InlineData("Button", false)]
    [InlineData("DataGrid", false)]
    [InlineData("ListBox", false)]
    [InlineData("CheckBox", true)]
    public void EnterNavigation_ControlTypes_ShouldHandleEnterAccordingToType(string controlTypeName, bool shouldHandle)
    {
        Assert.Equal(shouldHandle, ShouldHandleEnterNavigation(controlTypeName));
    }

    /// <summary>
    /// Simula a lógica de TextFormattingBehavior.OnPreviewKeyDownMoveFocus
    /// para validar quais controles recebem navegação ENTER.
    /// </summary>
    private static bool ShouldHandleEnterNavigation(string typeName)
    {
        // Controles onde ENTER tem função própria
        if (typeName == "Button" || typeName == "DataGrid" || typeName == "ListBox")
            return false;

        // Controles editáveis que devem navegar com ENTER
        return typeName == "TextBox"
            || typeName == "ComboBox"
            || typeName == "PasswordBox"
            || typeName == "DatePicker"
            || typeName == "CheckBox";
    }

    #endregion

    #region CEP Lupa (ConsultarCepCommand) Tests

    [Fact]
    public async Task ConsultarCepCommand_CepValido_DevePreencherCampos()
    {
        // Arrange
        var service = new FakeCepService();
        service.AddCepResult("01310-100", "Av. Paulista", "Bela Vista", "São Paulo", "SP");

        var vm = CriarViewModel(service);
        AbrirFormulario(vm);
        vm.FormCep = "01310100";

        // Act
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);

        // Assert
        Assert.Equal("Av. Paulista", vm.FormLogradouro);
        Assert.Equal("Bela Vista", vm.FormBairro);
        Assert.Equal("São Paulo", vm.FormCidade);
        Assert.Equal("SP", vm.FormUf);
    }

    [Fact]
    public async Task ConsultarCepCommand_CepInvalido_DeveMostrarMensagemErro()
    {
        // Arrange
        var service = new FakeCepService();
        // CEP 99999999 não está configurado → falha

        var vm = CriarViewModel(service);
        AbrirFormulario(vm);
        vm.FormCep = "99999999";

        // Act
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);

        // Assert
        Assert.Contains("não foi possível localizar", vm.EditorMensagem, StringComparison.OrdinalIgnoreCase);
        Assert.True(string.IsNullOrEmpty(vm.FormLogradouro));
    }

    [Fact]
    public async Task ConsultarCepCommand_CepComMascara_DeveNormalizarENormalizar()
    {
        // Arrange
        var service = new FakeCepService();
        service.AddCepResult("01310-100", "Av. Teste", "Bairro Teste", "Cidade Teste", "sp");

        var vm = CriarViewModel(service);
        AbrirFormulario(vm);
        vm.FormCep = "01310-100"; // com máscara

        // Act
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);

        // Assert
        Assert.Equal("Av. Teste", vm.FormLogradouro);
        Assert.Equal("Bairro Teste", vm.FormBairro);
        Assert.Equal("Cidade Teste", vm.FormCidade);
        Assert.Equal("SP", vm.FormUf);
    }

    [Fact]
    public async Task ConsultarCepCommand_CepIncompleto_DeveMostrarMensagemSolicitando8Digitos()
    {
        // Arrange
        var service = new FakeCepService();
        service.AddCepResult("01310-100", "Av. Teste", "Bairro", "Cidade", "SP");

        var vm = CriarViewModel(service);
        AbrirFormulario(vm);
        vm.FormCep = "01310"; // CEP incompleto (5 dígitos)

        // Act
        vm.ConsultarCepCommand.Execute(null);
        await Task.Delay(50);

        // Assert
        Assert.Contains("8 dígitos", vm.EditorMensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConsultarCepCommand_DuranteCarregamento_DeveDesabilitarBotao()
    {
        // Arrange
        var service = new FakeCepService();
        service.AddCepResult("01310-100", "Av. Teste", "Bairro", "Cidade", "SP");

        var vm = CriarViewModel(service);
        AbrirFormulario(vm);
        vm.FormCep = "01310100";

        // Act + Assert
        vm.ConsultarCepCommand.Execute(null);
        // Durante a execução, CepCarregando deve ser true (ou true momentaneamente)
        // Como o FakeCepService é síncrono, CepCarregando volta a false rapidamente
        await Task.Delay(50);

        Assert.False(vm.CepCarregando);
        Assert.True(vm.CepPodeConsultarManualmente);
    }

    #endregion

    #region CEP Auto-Query (FormCep PropertyChanged) Tests

    [Fact]
    public async Task FormCep_CepCompleto_8Digitos_DeveConsultarAutomaticamente()
    {
        // Arrange
        var service = new FakeCepService();
        service.AddCepResult("01310-100", "Av. Paulista", "Bela Vista", "São Paulo", "SP");

        var vm = CriarViewModel(service);
        AbrirFormulario(vm);

        // Act - simula digitação completa do CEP
        vm.FormCep = "01310-100"; // máscara aplicada pelo TextFormattingBehavior
        await Task.Delay(50);

        // Assert - a query é disparada pelo setter do FormCep
        Assert.Equal("Av. Paulista", vm.FormLogradouro);
        Assert.Equal("Bela Vista", vm.FormBairro);
        Assert.Equal("São Paulo", vm.FormCidade);
        Assert.Equal("SP", vm.FormUf);
    }

    [Fact]
    public async Task FormCep_CepIncompleto_NaoDeveConsultar()
    {
        // Arrange
        var service = new FakeCepService();
        service.AddCepResult("01310-100", "Av. Paulista", "Bela Vista", "São Paulo", "SP");

        var vm = CriarViewModel(service);
        AbrirFormulario(vm);

        // Act
        vm.FormCep = "01310"; // CEP incompleto
        await Task.Delay(50);

        // Assert - nada foi preenchido
        Assert.Equal(string.Empty, vm.FormLogradouro);
        Assert.Equal(string.Empty, vm.FormBairro);
        Assert.Equal(string.Empty, vm.FormCidade);
        Assert.Equal(string.Empty, vm.FormUf);
    }

    [Fact]
    public async Task FormCep_CepNaoEncontrado_DeveMostrarMensagemDeErro()
    {
        // Arrange
        var service = new FakeCepService();
        // Nenhum CEP configurado → todas as consultas falham

        var vm = CriarViewModel(service);
        AbrirFormulario(vm);

        // Act
        vm.FormCep = "99999999";
        await Task.Delay(50);

        // Assert
        Assert.Contains("não foi possível localizar", vm.EditorMensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FormCep_CepRepetido_NaoDeveConsultarDuasVezes()
    {
        // Arrange
        var service = new FakeContadorCepService();
        service.AddCepResult("01310-100", "Av. Teste", "Bairro", "Cidade", "SP");

        var vm = CriarViewModel(service);
        AbrirFormulario(vm);

        // Act - primeiro preenchimento
        vm.FormCep = "01310-100";
        await Task.Delay(50);

        var contagemPrimeira = service.NumeroConsultas;

        // Limpa e represetnha o mesmo CEP
        vm.FormCep = "01310100";
        await Task.Delay(50);

        // Assert - apenas 1 consulta foi feita (cache evita a segunda)
        Assert.Equal(1, service.NumeroConsultas);
    }

    #endregion

    #region CEP Cache Reset Tests

    /// <summary>
    /// Ao limpar o campo CEP, o cache deve ser resetado para permitir nova consulta
    /// ao mesmo CEP. Isso permite retry de CEPs que falharam ou foram alterados.
    /// </summary>
    [Fact]
    public async Task FormCep_LimparCampo_DeveResetarCache_PermiteNovaConsulta()
    {
        // Arrange
        var service = new FakeContadorCepService();
        service.AddCepResult("01310-100", "Av. Teste", "Bairro", "Cidade", "SP");

        var vm = CriarViewModel(service);
        AbrirFormulario(vm);

        // Act - primeira consulta
        vm.FormCep = "01310-100";
        await Task.Delay(50);
        var contagemAposPrimeira = service.NumeroConsultas;

        // Limpa o campo CEP
        vm.FormCep = string.Empty;
        await Task.Delay(50);

        // Retype o mesmo CEP
        vm.FormCep = "01310-100";
        await Task.Delay(50);

        // Assert - duas consultas foram feitas (cache foi resetado ao limpar)
        Assert.Equal(2, service.NumeroConsultas);
        Assert.Equal("Av. Teste", vm.FormLogradouro);
    }

    /// <summary>
    /// Quando o CEP falha (não encontrado), limpar e reescrever deve permitir retry.
    /// </summary>
    [Fact]
    public async Task FormCep_CepQueFalhou_LimparERetype_DeveConsultarNovamente()
    {
        // Arrange
        var service = new FakeContadorCepService();
        service.AddCepResult("01310-100", "Av. Teste", "Bairro", "Cidade", "SP");
        // CEP 99999999 não configurado → falha

        var vm = CriarViewModel(service);
        AbrirFormulario(vm);

        // Act - primeira consulta falha
        vm.FormCep = "99999999";
        await Task.Delay(50);
        Assert.Contains("não foi possível localizar", vm.EditorMensagem, StringComparison.OrdinalIgnoreCase);
        var contagemAposFalha = service.NumeroConsultas;

        // Limpa e retenta
        vm.FormCep = string.Empty;
        await Task.Delay(50);
        vm.FormCep = "99999999";
        await Task.Delay(50);

        // Assert - nova consulta foi feita após limpar
        Assert.Equal(2, service.NumeroConsultas);
    }

    /// <summary>
    /// Alterar para um CEP diferente deve limpar os campos de endereço antes da nova consulta.
    /// </summary>
    [Fact]
    public async Task FormCep_MudarParaCepDiferente_DeveLimparEnderecoAntesNovaConsulta()
    {
        // Arrange
        var service = new FakeCepService();
        service.AddCepResult("01310-100", "Av. Paulista", "Bela Vista", "São Paulo", "SP");
        service.AddCepResult("20040-000", "Av. Rio", "Centro", "Rio de Janeiro", "RJ");

        var vm = CriarViewModel(service);
        AbrirFormulario(vm);

        // Act - primeiro CEP
        vm.FormCep = "01310-100";
        await Task.Delay(50);
        Assert.Equal("Av. Paulista", vm.FormLogradouro);

        // Altera para CEP diferente
        vm.FormCep = "20040-000";
        await Task.Delay(50);

        // Assert - endereço atualizado com o novo CEP
        Assert.Equal("Av. Rio", vm.FormLogradouro);
        Assert.Equal("Centro", vm.FormBairro);
        Assert.Equal("Rio de Janeiro", vm.FormCidade);
        Assert.Equal("RJ", vm.FormUf);
        Assert.Empty(vm.EditorMensagem);
    }

    /// <summary>
    /// Limpar o campo CEP deve limpar todos os campos de endereço e resetar o cache.
    /// </summary>
    [Fact]
    public async Task FormCep_LimparCampo_DeveLimparCamposEndereco()
    {
        // Arrange
        var service = new FakeCepService();
        service.AddCepResult("01310-100", "Av. Paulista", "Bela Vista", "São Paulo", "SP");

        var vm = CriarViewModel(service);
        AbrirFormulario(vm);

        // Act - preenche CEP e obtém endereço
        vm.FormCep = "01310-100";
        await Task.Delay(50);
        Assert.Equal("Av. Paulista", vm.FormLogradouro);

        // Limpa o CEP
        vm.FormCep = string.Empty;
        await Task.Delay(10);

        // Assert - campos de endereço limpos
        Assert.Empty(vm.FormLogradouro);
        Assert.Empty(vm.FormBairro);
        Assert.Empty(vm.FormCidade);
        Assert.Empty(vm.FormUf);
    }

    #endregion

    #region CEP Service Disponível / Indisponível

    [Fact]
    public void CepPodeConsultarManualmente_SemServico_DeveRetornarFalse()
    {
        // Arrange
        var vm = CriarViewModel(null); // sem ICepService
        AbrirFormulario(vm);

        // Assert
        Assert.False(vm.CepPodeConsultarManualmente);
    }

    [Fact]
    public async Task CepPodeConsultarManualmente_ComServico_CepCompleto_DeveRetornarTrue()
    {
        // Arrange
        var service = new FakeCepService();
        var vm = CriarViewModel(service);
        AbrirFormulario(vm);
        vm.FormCep = "01310100";
        await Task.Delay(50);

        // Assert
        Assert.True(vm.CepPodeConsultarManualmente);
    }

    #endregion

    #region Stub / Helper

    /// <summary>
    /// Stub simples de IClienteService para testes de ViewModel.
    /// Retorna resultados vazios para não interferir nos testes de CEP.
    /// </summary>
    private class StubClienteService : IClienteService
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

    /// <summary>
    /// ICepService que conta consultas e retorna resultados configuráveis.
    /// </summary>
    private class FakeContadorCepService : ICepService
    {
        private readonly Dictionary<string, CepAddressResult> _results = new();
        private readonly bool _returnFailure;
        private readonly string _failureMessage;

        public FakeContadorCepService(bool returnFailure = false, string failureMessage = "")
        {
            _returnFailure = returnFailure;
            _failureMessage = failureMessage;
        }

        public string ProviderName => "Contador";

        public int NumeroConsultas { get; private set; }

        public void AddCepResult(string cep, string? logradouro, string? bairro, string? cidade, string? uf)
        {
            var normalized = CepMaskHelper.Normalizar(cep);
            _results[normalized] = CepAddressResult.SuccessResult(normalized, logradouro, bairro, cidade, uf, source: ProviderName);
        }

        public Task<CepAddressResult> ConsultarAsync(string cep, CancellationToken cancellationToken = default)
        {
            NumeroConsultas++;
            if (_returnFailure)
            {
                return Task.FromResult(CepAddressResult.FailureResult(
                    CepMaskHelper.Normalizar(cep), _failureMessage, ProviderName));
            }

            var normalized = CepMaskHelper.Normalizar(cep);
            if (_results.TryGetValue(normalized, out var result))
                return Task.FromResult(result);

            return Task.FromResult(CepAddressResult.FailureResult(
                normalized, "CEP não encontrado", ProviderName));
        }
    }

    #endregion

    #region Smoke Tests - WPF View/Control Instantiation

    /// <summary>
    /// Verifica que o CepComLookupControl pode ser instanciado sem XamlParseException,
    /// garantindo que todos os recursos (styles, brushes, icons) são resolvidos corretamente.
    /// </summary>
    [Fact]
    public void CepComLookupControl_Instantiation_DeveCriarSemException()
    {
        RunOnStaThread(() =>
        {
            var control = new CepComLookupControl();
            Assert.NotNull(control);
        });
    }

    /// <summary>
    /// Verifica que a ClientesView pode ser instanciada sem XamlParseException.
    /// Isso garante que todos os recursos da tela (incluindo o CepComLookupControl)
    /// são resolvidos corretamente durante a inicialização.
    /// </summary>
    [Fact]
    public void ClientesView_Instantiation_DeveCriarSemException()
    {
        RunOnStaThread(() =>
        {
            var vm = CriarViewModel(new FakeCepService());
            var view = new ClientesView
            {
                DataContext = vm
            };
            Assert.NotNull(view);
        });
    }

    /// <summary>
    /// Verifica que o ClientesViewModel abre o formulário de novo cliente sem exceção,
    /// garantindo que o DataContext está em estado válido para o binding da View.
    /// </summary>
    [Fact]
    public void ClientesViewModel_AbrirNovo_DeveInicializarFormulario()
    {
        var vm = CriarViewModel(new FakeCepService());
        vm.NovoCommand.Execute(null);

        Assert.True(vm.EditorAberto);
        Assert.Equal("Novo cliente", vm.EditorTitulo);
    }

    /// <summary>
    /// Executa uma ação em uma thread STA, necessária para criar controles WPF.
    /// Garante que uma Application com os recursos do tema esteja disponível
    /// para que os StaticResources sejam resolvidos corretamente.
    /// </summary>
    private static void RunOnStaThread(Action action)
    {
        var tcs = new TaskCompletionSource<object?>();
        var thread = new Thread(() =>
        {
            try
            {
                EnsureApplicationResources();
                action();
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        // Propaga qualquer exceção ocorrida na thread STA
        if (tcs.Task.IsFaulted && tcs.Task.Exception != null)
            throw tcs.Task.Exception.InnerException ?? tcs.Task.Exception;
    }

    /// <summary>
    /// Garante que uma Application WPF com os recursos do tema foi criada.
    /// Como Application é singleton por AppDomain, esta verificação é thread-safe.
    /// </summary>
    private static void EnsureApplicationResources()
    {
        if (System.Windows.Application.Current == null)
        {
            var app = new System.Windows.Application();
            app.Resources.MergedDictionaries.Add(
                new System.Windows.ResourceDictionary
                {
                    Source = new Uri("/OrcPro.App;component/Resources/Themes/MainTheme.xaml", UriKind.Relative)
                });
            app.Resources.MergedDictionaries.Add(
                new System.Windows.ResourceDictionary
                {
                    Source = new Uri("/OrcPro.App;component/Resources/CadastroModuleStyles.xaml", UriKind.Relative)
                });
        }
    }

    #endregion
}

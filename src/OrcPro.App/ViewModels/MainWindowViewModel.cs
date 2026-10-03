using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using OrcPro.Application.DTOs.Auth;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Common;

namespace OrcPro.App.ViewModels;

/// <summary>
/// ViewModel do shell principal: barra superior, Ribbon, navegação do módulo e rodapé de status.
/// </summary>
public class MainWindowViewModel : ViewModelBase, IDisposable
{
    private const string ModuleOverviewTitle = "Visão Geral";
    private const string UsuariosPerfisModuleTitle = "Usuários e Perfis";
    private const string ClientesModuleTitle = "Clientes";
    private const string TecnicosModuleTitle = "Técnicos";
    private const string ProductName = "OrcPro";
    private const string CompanyName = "ALEX T.I. Tecnologia e Assistência";
    private const string DatabaseProfileName = "[Base de Dados: Produção]";
    private const string DatabaseEndpointName = "PostgreSQL Local";
    private const string NetworkLatencyLabel = "Latência de rede: 1ms";
    private const string BranchName = "ALEX T.I. Matriz";
    private const string BuildVersion = "v2.6.4";
    private const string ZoomLevelValue = "100%";

    private readonly UsuarioSessaoDto _sessao;
    private readonly IServiceScope _moduloScope;
    private readonly IUsuarioService _usuarioService;
    private readonly IPerfilService _perfilService;
    private readonly IClienteService _clienteService;
    private readonly ITecnicoService _tecnicoService;
    private readonly IPermissaoService _permissaoService;
    private readonly ICepService _cepService;
    private ViewModelBase _currentView;
    private NavigationItemViewModel? _selectedNavigationItem;
    private string _moduleTitle = ModuleOverviewTitle;
    private string _statusMessage = "Pronto";

    /// <summary>
    /// Sessão do usuário autenticado no login; fornece nome e perfil para o shell
    /// (title bar/rodapé) e para o Dashboard.
    /// </summary>
    public MainWindowViewModel(UsuarioSessaoDto sessao, IServiceProvider serviceProvider)
    {
        _sessao = sessao ?? throw new ArgumentNullException(nameof(sessao));

        // Um escopo próprio do shell para os serviços usados pelos módulos (usuarios/perfis).
        _moduloScope = serviceProvider.CreateScope();
        _usuarioService = _moduloScope.ServiceProvider.GetRequiredService<IUsuarioService>();
        _perfilService = _moduloScope.ServiceProvider.GetRequiredService<IPerfilService>();
        _clienteService = _moduloScope.ServiceProvider.GetRequiredService<IClienteService>();
        _tecnicoService = _moduloScope.ServiceProvider.GetRequiredService<ITecnicoService>();
        _permissaoService = _moduloScope.ServiceProvider.GetRequiredService<IPermissaoService>();
        _cepService = _moduloScope.ServiceProvider.GetRequiredService<ICepService>();

        RibbonActionCommand = new RelayCommand(parameter => UpdateStatus(parameter as string));
        OpenUsuariosPerfisCommand = new RelayCommand(parameter => OpenUsuariosPerfis(parameter as string));
        OpenClientesCommand = new RelayCommand(_ => OpenClientes());
        OpenTecnicosCommand = new RelayCommand(_ => OpenTecnicos());

        NavigationItems = new ObservableCollection<NavigationItemViewModel>
        {
            new(ModuleOverviewTitle, "Dashboard",
                new RelayCommand(_ => Navigate(ModuleOverviewTitle, CreateDashboard(),
                    "Painel principal carregado."))),
            new("Orçamentos Ativos", "Orcamentos",
                new RelayCommand(_ => Navigate("Orçamentos Ativos",
                    new PlaceholderViewModel("Orçamentos Ativos",
                        "Acompanhe e gerencie os orçamentos em andamento."),
                    "Módulo de orçamentos em desenvolvimento."))),
            new("Bases Cadastrais", "Cadastros",
                new RelayCommand(_ => Navigate("Bases Cadastrais",
                    new PlaceholderViewModel("Bases Cadastrais",
                        "Clientes, técnicos, peças, itens e serviços."),
                    "Módulo de cadastros em desenvolvimento."))),
            new("Relatórios Gerenciais", "Relatorios",
                new RelayCommand(_ => Navigate("Relatórios Gerenciais",
                    new PlaceholderViewModel("Relatórios Gerenciais",
                        "Indicadores de vendas, produção e faturamento."),
                    "Módulo de relatórios em desenvolvimento."))),
            new("Administração & BD", "Administracao",
                new RelayCommand(_ => Navigate("Administração & BD",
                    new PlaceholderViewModel("Administração & BD",
                        "Usuários, perfis, permissões e base de dados."),
                    "Módulo administrativo em desenvolvimento.")))
        };

        _currentView = CreateDashboard();
        _selectedNavigationItem = NavigationItems[0];
    }

    /// <summary>Comando único das ações do Ribbon sem navegação; o parâmetro traz a mensagem de status.</summary>
    public ICommand RibbonActionCommand { get; }

    /// <summary>
    /// Abre o módulo Usuários e Perfis. O parâmetro seleciona a aba inicial
    /// ("usuarios" ou "perfis"); sem parâmetro, abre em Usuários.
    /// </summary>
    public ICommand OpenUsuariosPerfisCommand { get; }

    /// <summary>Abre o módulo de Clientes (Ribbon Cadastros → Clientes).</summary>
    public ICommand OpenClientesCommand { get; }

    /// <summary>Abre o módulo de Técnicos (Ribbon Cadastros → Técnicos).</summary>
    public ICommand OpenTecnicosCommand { get; }

    /// <summary>Itens do menu lateral "Navegação do Módulo".</summary>
    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }

    /// <summary>Visão atualmente exibida na área de conteúdo.</summary>
    public ViewModelBase CurrentView
    {
        get => _currentView;
        private set => SetField(ref _currentView, value);
    }

    /// <summary>Item selecionado no menu lateral.</summary>
    public NavigationItemViewModel? SelectedNavigationItem
    {
        get => _selectedNavigationItem;
        set
        {
            if (SetField(ref _selectedNavigationItem, value))
                value?.Command.Execute(null);
        }
    }

    /// <summary>Título do módulo ativo, exibido no rodapé e na barra de tarefas.</summary>
    public string ModuleTitle
    {
        get => _moduleTitle;
        private set
        {
            if (SetField(ref _moduleTitle, value))
                OnPropertyChanged(nameof(WindowTitle));
        }
    }

    public string WindowTitle => $"{ProductName} — {ModuleTitle}";

    /// <summary>Mensagem de status exibida no canto esquerdo do rodapé.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    // ============================================================
    // Permissões do usuário autenticado (catálogo MODULO.ACAO). O Ribbon e os módulos
    // respeitam estas flags: sem a permissão de VISUALIZAR o acesso ao módulo é bloqueado
    // e sem as permissões de ação os botões ficam desabilitados. O perfil Administrador
    // recebe todas as permissões automaticamente (PermissaoSincronizador).
    // ============================================================

    /// <summary>Acesso ao módulo Clientes (CLIENTES.VISUALIZAR).</summary>
    public bool PodeAcessarClientes => _sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Clientes.Visualizar);

    /// <summary>Acesso ao módulo Técnicos (TECNICOS.VISUALIZAR).</summary>
    public bool PodeAcessarTecnicos => _sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Tecnicos.Visualizar);

    /// <summary>Acesso ao módulo Usuários e Perfis (USUARIOS_PERFIS.VISUALIZAR).</summary>
    public bool PodeAcessarUsuariosPerfis => _sessao.PossuiPermissao(PermissaoCatalogo.Codigos.UsuariosPerfis.Visualizar);

    /// <summary>Acesso ao painel (DASHBOARD.VISUALIZAR).</summary>
    public bool PodeAcessarDashboard => _sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Dashboard.Visualizar);

    // ---- Barra superior ----
    public string AppTitle => ProductName;

    public string CompanyLine => CompanyName;

    public string DatabaseProfile => DatabaseProfileName;

    public string OperatorCaption => $"{UsuarioLogado} ({_sessao.PerfilNome})";

    /// <summary>Nome exibido do operador logado (nome completo; username como alternativa).</summary>
    public string UsuarioLogado =>
        string.IsNullOrWhiteSpace(_sessao.NomeCompleto) ? _sessao.Username : _sessao.NomeCompleto;

    // ---- Rodapé de status ----
    public string ConnectionSummary => $"Conectado ({DatabaseEndpointName})";

    public string OperatorSummary => $"Usuário: {OperatorCaption}";

    public string FilialSummary => $"Filial: {BranchName}";

    public string ZoomLevel => ZoomLevelValue;

    public string ApplicationVersion => BuildVersion;

    // ---- Menu lateral (rodapé) ----
    public string DatabaseStatusLabel => $"{DatabaseEndpointName} Operacional";

    public string NetworkLatencyText => NetworkLatencyLabel;

    /// <summary>
    /// Cria o Dashboard repassando o nome e o perfil do usuário autenticado.
    /// </summary>
    private DashboardViewModel CreateDashboard() =>
        new(UsuarioLogado, _sessao.PerfilNome);

    /// <summary>
    /// Troca a visão exibida, atualiza o título do módulo e a mensagem de status.
    /// </summary>
    private void Navigate(string moduleTitle, ViewModelBase view, string statusMessage)
    {
        ModuleTitle = moduleTitle;
        CurrentView = view;
        UpdateStatus(statusMessage);

        // Carrega os dados da tela recém-exibida (grids, listas e opções dos formulários).
        _ = view.InitializeAsync();
    }

    /// <summary>
    /// Módulo de Usuários e Perfis (Ribbon Cadastros → Usuários e Perfis). Uma nova instância
    /// é criada a cada abertura para que a listagem sempre reflita a base.
    /// </summary>
    private void OpenUsuariosPerfis(string? aba)
    {
        if (!PodeAcessarUsuariosPerfis)
        {
            UpdateStatus("Você não possui a permissão USUARIOS_PERFIS.VISUALIZAR.");
            return;
        }

        var modulo = new UsuariosPerfisViewModel(_usuarioService, _perfilService, _permissaoService, _sessao, UpdateStatus);
        modulo.AbaSelecionada = string.Equals(aba, "perfis", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        Navigate(UsuariosPerfisModuleTitle, modulo, "Módulo de usuários e perfis carregado.");
    }

    /// <summary>
    /// Módulo de Clientes (Ribbon Cadastros → Clientes). Nova instância a cada abertura para
    /// que a listagem sempre reflita a base.
    /// </summary>
    private void OpenClientes()
    {
        if (!PodeAcessarClientes)
        {
            UpdateStatus("Você não possui a permissão CLIENTES.VISUALIZAR.");
            return;
        }

        var modulo = new ClientesViewModel(_clienteService, _sessao, _cepService, UpdateStatus);
        Navigate(ClientesModuleTitle, modulo, "Módulo de clientes carregado.");
    }

    /// <summary>
    /// Módulo de Técnicos (Ribbon Cadastros → Técnicos). Nova instância a cada abertura para
    /// que a listagem sempre reflita a base.
    /// </summary>
    private void OpenTecnicos()
    {
        if (!PodeAcessarTecnicos)
        {
            UpdateStatus("Você não possui a permissão TECNICOS.VISUALIZAR.");
            return;
        }

        var modulo = new TecnicosViewModel(_tecnicoService, _sessao, _cepService, UpdateStatus);
        Navigate(TecnicosModuleTitle, modulo, "Módulo de técnicos carregado.");
    }

    private void UpdateStatus(string? message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            StatusMessage = message;
    }

    /// <summary>Libera o escopo de serviços usado pelos módulos do shell.</summary>
    public void Dispose()
    {
        _moduloScope.Dispose();
        GC.SuppressFinalize(this);
    }
}

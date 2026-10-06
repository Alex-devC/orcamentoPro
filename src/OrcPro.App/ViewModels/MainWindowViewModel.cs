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
    private const string PecasModuleTitle = "Peças / Itens";
    private const string ServicosModuleTitle = "Serviços";
    private const string EmitenteModuleTitle = "Minha Empresa";
    private const string OrcamentosModuleTitle = "Orçamentos";
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
    private readonly IPecaService _pecaService;
    private readonly IServicoService _servicoService;
    private readonly IEmpresaService _empresaService;
    private readonly IOrcamentoService _orcamentoService;
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
        _pecaService = _moduloScope.ServiceProvider.GetRequiredService<IPecaService>();
        _servicoService = _moduloScope.ServiceProvider.GetRequiredService<IServicoService>();
        _empresaService = _moduloScope.ServiceProvider.GetRequiredService<IEmpresaService>();
        _orcamentoService = _moduloScope.ServiceProvider.GetRequiredService<IOrcamentoService>();
        _clienteService = _moduloScope.ServiceProvider.GetRequiredService<IClienteService>();
        _pecaService = _moduloScope.ServiceProvider.GetRequiredService<IPecaService>();
        _servicoService = _moduloScope.ServiceProvider.GetRequiredService<IServicoService>();
        _tecnicoService = _moduloScope.ServiceProvider.GetRequiredService<ITecnicoService>();
        _permissaoService = _moduloScope.ServiceProvider.GetRequiredService<IPermissaoService>();
        _cepService = _moduloScope.ServiceProvider.GetRequiredService<ICepService>();

        RibbonActionCommand = new RelayCommand(parameter => UpdateStatus(parameter as string));
        OpenUsuariosPerfisCommand = new RelayCommand(parameter => OpenUsuariosPerfis(parameter as string));
        OpenClientesCommand = new RelayCommand(_ => OpenClientes());
        OpenTecnicosCommand = new RelayCommand(_ => OpenTecnicos());
        OpenPecasCommand = new RelayCommand(_ => OpenPecas());
        OpenServicosCommand = new RelayCommand(_ => OpenServicos());
        OpenMinhaEmpresaCommand = new RelayCommand(_ => OpenMinhaEmpresa());
        OpenOrcamentosCommand = new RelayCommand(_ => OpenOrcamentos());
        NovoOrcamentoCommand = new RelayCommand(_ => OpenNovoOrcamento());

        NavigationItems = new ObservableCollection<NavigationItemViewModel>
        {
            new(ModuleOverviewTitle, "Dashboard",
                new RelayCommand(_ => Navigate(ModuleOverviewTitle, CreateDashboard(),
                    "Painel principal carregado."))),
            new("Orçamentos Ativos", "Orcamentos",
                new RelayCommand(_ => OpenOrcamentos())),
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

    /// <summary>Abre o módulo Peças / Itens (Ribbon Cadastros → Peças / Itens).</summary>
    public ICommand OpenPecasCommand { get; }

    /// <summary>Abre o módulo de Serviços / Mão de Obra (Ribbon Cadastros → Serviços).</summary>
    public ICommand OpenServicosCommand { get; }

    /// <summary>Abre o módulo Minha Empresa / Emitente (Ribbon Configurações).</summary>
    public ICommand OpenMinhaEmpresaCommand { get; }

    /// <summary>Abre o módulo de Orçamentos (Ribbon Orçamentos → Abrir).</summary>
    public ICommand OpenOrcamentosCommand { get; }

    /// <summary>Abre o módulo de Orçamentos já com um novo orçamento em edição.</summary>
    public ICommand NovoOrcamentoCommand { get; }

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

    /// <summary>Acesso ao módulo Peças / Itens (PECAS.VISUALIZAR).</summary>
    public bool PodeAcessarPecas => _sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Pecas.Visualizar);

    /// <summary>Acesso ao módulo Serviços (SERVICOS.VISUALIZAR).</summary>
    public bool PodeAcessarServicos => _sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Servicos.Visualizar);

    /// <summary>Acesso ao módulo Minha Empresa (EMITENTE.VISUALIZAR).</summary>
    public bool PodeAcessarEmitente => _sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Emitente.Visualizar);

    /// <summary>Acesso ao módulo Orçamentos (ORCAMENTOS.VISUALIZAR).</summary>
    public bool PodeAcessarOrcamentos => _sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Orcamentos.Visualizar);

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

    /// <summary>
    /// Módulo de Peças / Itens (Ribbon Cadastros → Peças / Itens). Nova instância a cada abertura para
    /// que a listagem sempre reflita a base.
    /// </summary>
    private void OpenPecas()
    {
        if (!PodeAcessarPecas)
        {
            UpdateStatus("Você não possui a permissão PECAS.VISUALIZAR.");
            return;
        }

        var modulo = new PecasViewModel(_pecaService, _sessao, UpdateStatus);
        Navigate(PecasModuleTitle, modulo, "Módulo de peças e itens carregado.");
    }

    /// <summary>
    /// Módulo de Serviços / Mão de Obra (Ribbon Cadastros → Serviços). Nova instância a cada
    /// abertura para que a listagem sempre reflita a base.
    /// </summary>
    private void OpenServicos()
    {
        if (!PodeAcessarServicos)
        {
            UpdateStatus("Você não possui a permissão SERVICOS.VISUALIZAR.");
            return;
        }

        var modulo = new ServicosViewModel(_servicoService, _sessao, UpdateStatus);
        Navigate(ServicosModuleTitle, modulo, "Módulo de serviços carregado.");
    }

    /// <summary>
    /// Módulo Minha Empresa / Emitente (Ribbon Configurações). Nova instância a cada abertura,
    /// seguindo o mesmo padrão dos demais módulos do shell.
    /// </summary>
    private void OpenMinhaEmpresa()
    {
        if (!PodeAcessarEmitente)
        {
            UpdateStatus("Você não possui a permissão EMITENTE.VISUALIZAR.");
            return;
        }

        var modulo = new MinhaEmpresaViewModel(_empresaService, _sessao, _cepService, UpdateStatus);
        Navigate(EmitenteModuleTitle, modulo, "Módulo de minha empresa carregado.");
    }

    /// <summary>
    /// Módulo de Orçamentos (Ribbon Orçamentos). Nova instância a cada abertura, seguindo
    /// o padrão dos demais módulos do shell.
    /// </summary>
    private void OpenOrcamentos()
    {
        if (!PodeAcessarOrcamentos)
        {
            UpdateStatus("Você não possui a permissão ORCAMENTOS.VISUALIZAR.");
            return;
        }

        var modulo = CriarModuloOrcamentos();
        Navigate(OrcamentosModuleTitle, modulo, "Módulo de orçamentos carregado.");
    }

    /// <summary>Mesmo módulo, porém já abrindo o formulário de novo orçamento.</summary>
    private void OpenNovoOrcamento()
    {
        if (!PodeAcessarOrcamentos)
        {
            UpdateStatus("Você não possui a permissão ORCAMENTOS.VISUALIZAR.");
            return;
        }

        var modulo = CriarModuloOrcamentos();
        Navigate(OrcamentosModuleTitle, modulo, "Novo orçamento.");
        _ = modulo.AbrirNovoOrcamentoAsync();
    }

    private OrcamentosViewModel CriarModuloOrcamentos()
        => new(
            _orcamentoService,
            _clienteService,
            _pecaService,
            _servicoService,
            _tecnicoService,
            _empresaService,
            _sessao,
            UpdateStatus);

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

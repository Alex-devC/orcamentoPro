using System.Collections.ObjectModel;
using System.Windows.Input;

namespace OrcPro.App.ViewModels;

/// <summary>
/// ViewModel do shell principal: barra superior, Ribbon, navegação do módulo e rodapé de status.
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    private const string ModuleOverviewTitle = "Visão Geral";
    private const string ProductName = "OrcPro";
    private const string CompanyName = "ALEX T.I. Tecnologia e Assistência";
    private const string DatabaseProfileName = "[Base de Dados: Produção]";
    private const string DatabaseEndpointName = "PostgreSQL Local";
    private const string NetworkLatencyLabel = "Latência de rede: 1ms";
    private const string OperatorName = "Carlos Eduardo";
    private const string OperatorRole = "Administrador";
    private const string BranchName = "ALEX T.I. Matriz";
    private const string BuildVersion = "v2.6.4";
    private const string ZoomLevelValue = "100%";

    private ViewModelBase _currentView;
    private NavigationItemViewModel? _selectedNavigationItem;
    private string _moduleTitle = ModuleOverviewTitle;
    private string _statusMessage = "Pronto";

    public MainWindowViewModel()
    {
        RibbonActionCommand = new RelayCommand(parameter => UpdateStatus(parameter as string));

        NavigationItems = new ObservableCollection<NavigationItemViewModel>
        {
            new(ModuleOverviewTitle, "Dashboard",
                new RelayCommand(_ => Navigate(ModuleOverviewTitle, new DashboardViewModel(),
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

        _currentView = new DashboardViewModel();
        _selectedNavigationItem = NavigationItems[0];
    }

    /// <summary>Comando único das ações do Ribbon; o parâmetro traz a mensagem de status.</summary>
    public ICommand RibbonActionCommand { get; }

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

    // ---- Barra superior ----
    public string AppTitle => ProductName;

    public string CompanyLine => CompanyName;

    public string DatabaseProfile => DatabaseProfileName;

    public string OperatorCaption => $"{OperatorName} ({OperatorRole})";

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
    /// Troca a visão exibida, atualiza o título do módulo e a mensagem de status.
    /// </summary>
    private void Navigate(string moduleTitle, ViewModelBase view, string statusMessage)
    {
        ModuleTitle = moduleTitle;
        CurrentView = view;
        UpdateStatus(statusMessage);
    }

    private void UpdateStatus(string? message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            StatusMessage = message;
    }
}

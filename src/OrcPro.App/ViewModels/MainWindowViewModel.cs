using System.Windows.Input;

namespace OrcPro.App.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private ViewModelBase _currentView;
    private string _statusMessage = "Pronto";
    private string _moduleTitle = "Dashboard";

    public ViewModelBase CurrentView
    {
        get => _currentView;
        set => SetField(ref _currentView, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public string ModuleTitle
    {
        get => _moduleTitle;
        set => SetField(ref _moduleTitle, value);
    }

    // Ribbon navigation commands
    public ICommand ArquivoCommand { get; }
    public ICommand OrcamentosCommand { get; }
    public ICommand CadastrosCommand { get; }
    public ICommand RelatoriosCommand { get; }
    public ICommand SistemaCommand { get; }
    public ICommand DashboardCommand { get; }

    public MainWindowViewModel()
    {
        _currentView = new DashboardViewModel();

        DashboardCommand = new RelayCommand(_ => NavigateToDashboard());

        ArquivoCommand = new RelayCommand(_ => UpdateStatus("Módulo Arquivo ainda não implementado."));
        OrcamentosCommand = new RelayCommand(_ => UpdateStatus("Módulo Orçamentos em desenvolvimento."));
        CadastrosCommand = new RelayCommand(_ => UpdateStatus("Módulo Cadastros em desenvolvimento."));
        RelatoriosCommand = new RelayCommand(_ => UpdateStatus("Módulo Relatórios em desenvolvimento."));
        SistemaCommand = new RelayCommand(_ => UpdateStatus("Módulo Sistema em desenvolvimento."));
    }

    private void NavigateToDashboard()
    {
        CurrentView = new DashboardViewModel();
        ModuleTitle = "Dashboard";
        StatusMessage = "Painel principal carregado.";
    }

    private void UpdateStatus(string message)
    {
        StatusMessage = message;
    }
}

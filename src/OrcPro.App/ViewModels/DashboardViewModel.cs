using System.Collections.ObjectModel;
using System.Windows.Input;

namespace OrcPro.App.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private int _orcamentoCount;
    private int _clienteCount;
    private int _tecnicoCount;
    private int _pecaCount;

    private int _orcamentosAbertos;
    private int _orcamentosAguardandoAprovacao;
    private int _orcamentosAprovados;
    private int _orcamentosFinalizados;

    private string _usuarioLogado = "Carlos Eduardo";
    private string _usuarioCargo = "Administrador";

    public int OrcamentoCount
    {
        get => _orcamentoCount;
        set => SetField(ref _orcamentoCount, value);
    }

    public int ClienteCount
    {
        get => _clienteCount;
        set => SetField(ref _clienteCount, value);
    }

    public int TecnicoCount
    {
        get => _tecnicoCount;
        set => SetField(ref _tecnicoCount, value);
    }

    public int PecaCount
    {
        get => _pecaCount;
        set => SetField(ref _pecaCount, value);
    }

    // Status-based counts
    public int OrcamentosAbertos
    {
        get => _orcamentosAbertos;
        set => SetField(ref _orcamentosAbertos, value);
    }

    public int OrcamentosAguardandoAprovacao
    {
        get => _orcamentosAguardandoAprovacao;
        set => SetField(ref _orcamentosAguardandoAprovacao, value);
    }

    public int OrcamentosAprovados
    {
        get => _orcamentosAprovados;
        set => SetField(ref _orcamentosAprovados, value);
    }

    public int OrcamentosFinalizados
    {
        get => _orcamentosFinalizados;
        set => SetField(ref _orcamentosFinalizados, value);
    }

    // User identification
    public string UsuarioLogado
    {
        get => _usuarioLogado;
        set => SetField(ref _usuarioLogado, value);
    }

    public string UsuarioCargo
    {
        get => _usuarioCargo;
        set => SetField(ref _usuarioCargo, value);
    }

    public string WelcomeMessage => "Bem-vindo ao OrcPro";

    public string SubtitleMessage => "Gerencie orçamentos, clientes e serviços com eficiência.";

    // Orçamentos recentes para o DataGrid
    public ObservableCollection<OrcamentoResumoViewModel> OrcamentosRecentes { get; }

    // Ações rápidas
    public ICommand NovoOrcamentoCommand { get; }
    public ICommand ClientesCommand { get; }
    public ICommand RelatoriosCommand { get; }

    public DashboardViewModel()
    {
        OrcamentosRecentes = new ObservableCollection<OrcamentoResumoViewModel>();
        CarregarDadosMock();

        NovoOrcamentoCommand = new RelayCommand(_ => OnNovoOrcamento());
        ClientesCommand = new RelayCommand(_ => OnClientes());
        RelatoriosCommand = new RelayCommand(_ => OnRelatorios());
    }

    private void CarregarDadosMock()
    {
        // Contagens por status
        OrcamentoCount = 8;
        ClienteCount = 24;
        TecnicoCount = 6;
        PecaCount = 142;

        OrcamentosAbertos = 2;
        OrcamentosAguardandoAprovacao = 1;
        OrcamentosAprovados = 2;
        OrcamentosFinalizados = 3;

        // Orçamentos recentes mockados
        OrcamentosRecentes.Add(new OrcamentoResumoViewModel
        {
            Numero = "ORÇ-000123",
            Cliente = "João Silva",
            Data = new DateTime(2026, 9, 25),
            ValorTotal = 1250.00m,
            Status = "Aprovado",
            StatusColor = "#10B981"
        });

        OrcamentosRecentes.Add(new OrcamentoResumoViewModel
        {
            Numero = "ORÇ-000124",
            Cliente = "Maria Santos",
            Data = new DateTime(2026, 9, 26),
            ValorTotal = 3800.50m,
            Status = "Aguardando Aprovação",
            StatusColor = "#F59E0B"
        });

        OrcamentosRecentes.Add(new OrcamentoResumoViewModel
        {
            Numero = "ORÇ-000122",
            Cliente = "Pedro Oliveira",
            Data = new DateTime(2026, 9, 24),
            ValorTotal = 850.75m,
            Status = "Aberto",
            StatusColor = "#3B82F6"
        });

        OrcamentosRecentes.Add(new OrcamentoResumoViewModel
        {
            Numero = "ORÇ-000121",
            Cliente = "Ana Costa",
            Data = new DateTime(2026, 9, 23),
            ValorTotal = 2150.00m,
            Status = "Finalizado",
            StatusColor = "#6B7280"
        });

        OrcamentosRecentes.Add(new OrcamentoResumoViewModel
        {
            Numero = "ORÇ-000120",
            Cliente = "Carlos Lima",
            Data = new DateTime(2026, 9, 22),
            ValorTotal = 4320.00m,
            Status = "Aberto",
            StatusColor = "#3B82F6"
        });
    }

    private void OnNovoOrcamento()
    {
        // Placeholder — navegação para módulo de novo orçamento será implementada
    }

    private void OnClientes()
    {
        // Placeholder — navegação para módulo de clientes será implementada
    }

    private void OnRelatorios()
    {
        // Placeholder — navegação para módulo de relatórios será implementada
    }
}



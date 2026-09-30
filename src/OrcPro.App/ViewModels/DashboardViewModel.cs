using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;

namespace OrcPro.App.ViewModels;

/// <summary>
/// ViewModel do Dashboard: indicadores por status, últimos orçamentos e totais do mês vigente.
/// </summary>
public class DashboardViewModel : ViewModelBase
{
    private const int TotalOrcamentosBase = 145;

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-BR");

    private int _rascunhosCount;
    private int _orcamentosAguardandoAprovacao;
    private int _orcamentosAprovados;
    private int _orcamentosEmExecucao;
    private int _orcamentosFinalizados;
    private int _orcamentosCancelados;

    private string _filterText = string.Empty;
    private string _usuarioLogado = "Carlos Eduardo";
    private string _usuarioCargo = "Administrador";

    private decimal _faturamentoAprovado;
    private string _faturamentoVariacao = string.Empty;
    private decimal _ticketMedio;
    private int _ticketMedioQuantidade;
    private double _taxaConversao;
    private int _distribuicaoPecas;
    private int _distribuicaoServicos;
    private int _distribuicaoCloud;

    public DashboardViewModel()
    {
        OrcamentosRecentes = new ObservableCollection<OrcamentoResumoViewModel>();
        CarregarDadosMock();

        OrcamentosView = CollectionViewSource.GetDefaultView(OrcamentosRecentes);
        OrcamentosView.Filter = FiltrarOrcamento;

        NovoOrcamentoCommand = new RelayCommand(_ => OnNovoOrcamento());
        ClientesCommand = new RelayCommand(_ => OnClientes());
        NovaPecaCommand = new RelayCommand(_ => OnNovaPeca());
        RelatoriosCommand = new RelayCommand(_ => OnRelatorios());
        AtualizarCommand = new RelayCommand(_ => OnAtualizar());
    }

    // ------------------------------------------------------------- Cabeçalho
    public string PageTitle => "Dashboard";

    public string PageSubtitle => "Visão geral dos seus orçamentos";

    /// <summary>Data e hora correntes, exibidas no chip do cabeçalho.</summary>
    public string HeaderDateText =>
        $"Hoje, {DateTime.Now.ToString("dd 'de' MMMM 'de' yyyy", Culture)} — {DateTime.Now.ToString("HH:mm", Culture)}";

    /// <summary>Mês vigente no formato SETEMBRO/2026.</summary>
    public string MesVigenteLabel => DateTime.Now.ToString("MMMM/yyyy", Culture).ToUpper(Culture);

    // ---------------------------------------------------------- Indicadores
    public int RascunhosCount
    {
        get => _rascunhosCount;
        set => SetField(ref _rascunhosCount, value);
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

    public int OrcamentosEmExecucao
    {
        get => _orcamentosEmExecucao;
        set => SetField(ref _orcamentosEmExecucao, value);
    }

    public int OrcamentosFinalizados
    {
        get => _orcamentosFinalizados;
        set => SetField(ref _orcamentosFinalizados, value);
    }

    public int OrcamentosCancelados
    {
        get => _orcamentosCancelados;
        set => SetField(ref _orcamentosCancelados, value);
    }

    // ---------------------------------------------------- Grade de orçamentos
    /// <summary>Coleção base dos últimos orçamentos exibidos no Dashboard.</summary>
    public ObservableCollection<OrcamentoResumoViewModel> OrcamentosRecentes { get; }

    /// <summary>Visão filtrada consumida pela grade central.</summary>
    public ICollectionView OrcamentosView { get; }

    /// <summary>Texto do filtro rápido (busca instantânea na grade).</summary>
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetField(ref _filterText, value))
                OrcamentosView.Refresh();
        }
    }

    public int TotalOrcamentosCount => TotalOrcamentosBase;

    public string TotalOrcamentosLabel => $"{TotalOrcamentosBase} total";

    public int ExibindoCount => OrcamentosRecentes.Count;

    // ------------------------------------ Usuário e totais do mês vigente
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

    public string FaturamentoAprovadoFormatado => $"R$ {_faturamentoAprovado.ToString("N2", Culture)}";

    public string FaturamentoVariacao
    {
        get => _faturamentoVariacao;
        private set => SetField(ref _faturamentoVariacao, value);
    }

    public string TicketMedioFormatado => $"R$ {_ticketMedio.ToString("N2", Culture)}";

    public string TicketMedioQuantidadeLabel => $"{_ticketMedioQuantidade} orçamentos";

    public double TaxaConversao
    {
        get => _taxaConversao;
        private set => SetField(ref _taxaConversao, value);
    }

    public string TaxaConversaoLabel => $"{_taxaConversao:0}%";

    public string DistribuicaoPecasLabel => $"Peças ({_distribuicaoPecas}%)";

    public string DistribuicaoServicosLabel => $"Serviços ({_distribuicaoServicos}%)";

    public string DistribuicaoCloudLabel => $"Cloud ({_distribuicaoCloud}%)";

    // ---------------------------------------------------------------- Comandos
    public ICommand NovoOrcamentoCommand { get; }

    public ICommand ClientesCommand { get; }

    public ICommand NovaPecaCommand { get; }

    public ICommand RelatoriosCommand { get; }

    public ICommand AtualizarCommand { get; }

    // ------------------------------------------------------------------- Mock
    private void CarregarDadosMock()
    {
        RascunhosCount = 4;
        OrcamentosAguardandoAprovacao = 12;
        OrcamentosAprovados = 28;
        OrcamentosEmExecucao = 7;
        OrcamentosFinalizados = 89;
        OrcamentosCancelados = 5;

        _faturamentoAprovado = 48720.00m;
        FaturamentoVariacao = "+14%";
        _ticketMedio = 1740.00m;
        _ticketMedioQuantidade = 28;
        TaxaConversao = 72;
        _distribuicaoPecas = 40;
        _distribuicaoServicos = 33;
        _distribuicaoCloud = 27;

        // Últimos orçamentos (dados de demonstração)
        AddOrcamento("0754/2026", new DateTime(2026, 9, 22), "Cond. Clube Moradia Jardim do Cedro",
            "Aguardando Aprovação", 1803.00m, "Carlos");
        AddOrcamento("0753/2026", new DateTime(2026, 9, 20), "Clínica Odonto Vida Ltda",
            "Aprovado", 850.00m, "Carlos");
        AddOrcamento("0752/2026", new DateTime(2026, 9, 18), "Logística Silva & Filhos",
            "Finalizado", 2450.00m, "Carlos");
        AddOrcamento("0751/2026", new DateTime(2026, 9, 17), "Supermercado Bom Preço",
            "Em Execução", 3120.00m, "Marcos");
        AddOrcamento("0750/2026", new DateTime(2026, 9, 15), "Condomínio Edifício Solar",
            "Aprovado", 940.00m, "Carlos");
        AddOrcamento("0749/2026", new DateTime(2026, 9, 14), "Tech Solutions Informática",
            "Cancelado", 620.00m, "Carlos");
        AddOrcamento("0748/2026", new DateTime(2026, 9, 12), "Padaria Pão de Ouro",
            "Finalizado", 1580.00m, "Marcos");
        AddOrcamento("0747/2026", new DateTime(2026, 9, 10), "Auto Posto Alvorada",
            "Rascunho", 450.00m, "Carlos");
    }

    private void AddOrcamento(string numero, DateTime data, string cliente, string status, decimal valor,
        string responsavel)
    {
        OrcamentosRecentes.Add(new OrcamentoResumoViewModel
        {
            Numero = numero,
            Data = data,
            Cliente = cliente,
            Status = status,
            ValorTotal = valor,
            Responsavel = responsavel
        });
    }

    private bool FiltrarOrcamento(object item)
    {
        if (string.IsNullOrWhiteSpace(_filterText))
            return true;

        if (item is not OrcamentoResumoViewModel resumo)
            return false;

        return resumo.Numero.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
            || resumo.Cliente.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
            || resumo.Status.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
            || resumo.Responsavel.Contains(_filterText, StringComparison.OrdinalIgnoreCase);
    }

    private void OnNovoOrcamento()
    {
        // Placeholder — a abertura do módulo de novo orçamento será implementada futuramente.
    }

    private void OnClientes()
    {
        // Placeholder — a abertura do cadastro de clientes será implementada futuramente.
    }

    private void OnNovaPeca()
    {
        // Placeholder — a abertura do cadastro de peças e itens será implementada futuramente.
    }

    private void OnRelatorios()
    {
        // Placeholder — a abertura da central de relatórios será implementada futuramente.
    }

    private void OnAtualizar()
    {
        OrcamentosView.Refresh();
    }
}

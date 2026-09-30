namespace OrcPro.App.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private int _orcamentoCount;
    private int _clienteCount;
    private int _tecnicoCount;
    private int _pecaCount;

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

    public string WelcomeMessage => "Bem-vindo ao OrcPro";

    public string SubtitleMessage => "Gerencie orçamentos, clientes e serviços com eficiência.";
}


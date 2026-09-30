using System;
using System.Globalization;

namespace OrcPro.App.ViewModels;

/// <summary>
/// Resumo de orçamento exibido na grade "Últimos orçamentos" do Dashboard.
/// </summary>
public class OrcamentoResumoViewModel : ViewModelBase
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-BR");

    private string _numero = string.Empty;
    private DateTime _data;
    private string _cliente = string.Empty;
    private string _responsavel = string.Empty;
    private string _status = string.Empty;
    private decimal _valorTotal;

    private string _statusColor = "#475569";
    private string _statusBackgroundColor = "#ECEFF3";
    private string _statusBorderColor = "#CBD5E1";
    private string _statusForegroundColor = "#475569";

    public string Numero
    {
        get => _numero;
        set => SetField(ref _numero, value);
    }

    public DateTime Data
    {
        get => _data;
        set
        {
            if (SetField(ref _data, value))
                OnPropertyChanged(nameof(DataFormatada));
        }
    }

    public string Cliente
    {
        get => _cliente;
        set => SetField(ref _cliente, value);
    }

    public string Responsavel
    {
        get => _responsavel;
        set => SetField(ref _responsavel, value);
    }

    public string Status
    {
        get => _status;
        set
        {
            if (SetField(ref _status, value))
                AplicarEstiloStatus(value);
        }
    }

    public decimal ValorTotal
    {
        get => _valorTotal;
        set
        {
            if (SetField(ref _valorTotal, value))
                OnPropertyChanged(nameof(ValorTotalFormatado));
        }
    }

    /// <summary>Data no formato curto brasileiro (dd/MM/yyyy).</summary>
    public string DataFormatada => _data.ToString("dd/MM/yyyy", Culture);

    /// <summary>Valor total formatado no padrão brasileiro (1.803,00).</summary>
    public string ValorTotalFormatado => _valorTotal.ToString("N2", Culture);

    /// <summary>Cor do indicador (bolinha) da linha.</summary>
    public string StatusColor
    {
        get => _statusColor;
        private set => SetField(ref _statusColor, value);
    }

    /// <summary>Cor de fundo do badge de status.</summary>
    public string StatusBackgroundColor
    {
        get => _statusBackgroundColor;
        private set => SetField(ref _statusBackgroundColor, value);
    }

    /// <summary>Cor da borda do badge de status.</summary>
    public string StatusBorderColor
    {
        get => _statusBorderColor;
        private set => SetField(ref _statusBorderColor, value);
    }

    /// <summary>Cor do texto do badge de status.</summary>
    public string StatusForegroundColor
    {
        get => _statusForegroundColor;
        private set => SetField(ref _statusForegroundColor, value);
    }

    /// <summary>
    /// Aplica a paleta de badges definida em DESIGN.md conforme o status do orçamento.
    /// </summary>
    private void AplicarEstiloStatus(string status)
    {
        var (background, border, foreground) = status switch
        {
            "Aguardando Aprovação" => ("#FEF3C7", "#FCD34D", "#92400E"),
            "Aprovado" => ("#E0F2FE", "#7DD3FC", "#0369A1"),
            "Em Execução" => ("#E0E7FF", "#A5B4FC", "#3730A3"),
            "Finalizado" => ("#DCFCE7", "#86EFAC", "#166534"),
            "Cancelado" => ("#FEE2E2", "#FCA5A5", "#991B1B"),
            _ => ("#ECEFF3", "#CBD5E1", "#475569")
        };

        StatusBackgroundColor = background;
        StatusBorderColor = border;
        StatusForegroundColor = foreground;
        StatusColor = foreground;
    }
}

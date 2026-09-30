using System;

namespace OrcPro.App.ViewModels;

/// <summary>
/// Representa um resumo de orçamento para exibição em listas e DataGrids.
/// </summary>
public class OrcamentoResumoViewModel : ViewModelBase
{
    private string _numero = string.Empty;
    private string _cliente = string.Empty;
    private DateTime _data;
    private decimal _valorTotal;
    private string _status = string.Empty;
    private string _statusColor = "#0078D7";

    public string Numero
    {
        get => _numero;
        set => SetField(ref _numero, value);
    }

    public string Cliente
    {
        get => _cliente;
        set => SetField(ref _cliente, value);
    }

    public DateTime Data
    {
        get => _data;
        set => SetField(ref _data, value);
    }

    public decimal ValorTotal
    {
        get => _valorTotal;
        set => SetField(ref _valorTotal, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    /// <summary>
    /// Cor hexadecimal para exibição visual do status no DataGrid.
    /// </summary>
    public string StatusColor
    {
        get => _statusColor;
        set => SetField(ref _statusColor, value);
    }
}

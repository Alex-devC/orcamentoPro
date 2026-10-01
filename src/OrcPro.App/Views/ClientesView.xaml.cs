using System.Windows.Controls;
using System.Windows.Input;
using OrcPro.App.ViewModels;

namespace OrcPro.App.Views;

/// <summary>
/// Tela do cadastro de Clientes. O code-behind resolve apenas os eventos do DataGrid
/// (ordenação por cabeçalho e duplo clique para editar) — a lógica está nos ViewModels.
/// </summary>
public partial class ClientesView : UserControl
{
    public ClientesView()
    {
        InitializeComponent();
    }

    /// <summary>Ordena a listagem de clientes pelo cabeçalho clicado (no banco, não só na página).</summary>
    private async void OnGridClientesSorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;

        if (e.Column.SortMemberPath is not string propriedade || string.IsNullOrWhiteSpace(propriedade))
            return;

        if (DataContext is ClientesViewModel modelo)
        {
            await modelo.AplicarOrdenacaoAsync(propriedade);
        }
    }

    /// <summary>Duplo clique na linha abre a edição do cliente.</summary>
    private void OnClientesGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ClientesViewModel modelo)
            return;

        var cliente = modelo.ClienteSelecionado;
        if (cliente != null)
        {
            modelo.EditarCommand.Execute(cliente);
        }
    }
}
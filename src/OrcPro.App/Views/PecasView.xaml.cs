using System.Windows.Controls;
using System.Windows.Input;
using OrcPro.App.ViewModels;

namespace OrcPro.App.Views;

/// <summary>
/// Tela do cadastro de Peças / Itens. O code-behind resolve apenas os eventos do DataGrid
/// (ordenação por cabeçalho e duplo clique para editar) — a lógica está nos ViewModels.
/// </summary>
public partial class PecasView : UserControl
{
    public PecasView()
    {
        InitializeComponent();
    }

    private async void OnGridPecasSorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;

        if (e.Column.SortMemberPath is not string propriedade || string.IsNullOrWhiteSpace(propriedade))
            return;

        if (DataContext is PecasViewModel modelo)
        {
            await modelo.AplicarOrdenacaoAsync(propriedade);
        }
    }

    private void OnPecasGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not PecasViewModel modelo)
            return;

        var peca = modelo.PecaSelecionada;
        if (peca != null)
        {
            modelo.EditarCommand.Execute(peca);
        }
    }
}

using System.Windows.Controls;
using System.Windows.Input;
using OrcPro.App.ViewModels;

namespace OrcPro.App.Views;

/// <summary>
/// Tela do cadastro de Técnicos. O code-behind resolve apenas os eventos do DataGrid
/// (ordenação por cabeçalho e duplo clique para editar) — a lógica está nos ViewModels.
/// </summary>
public partial class TecnicosView : UserControl
{
    public TecnicosView()
    {
        InitializeComponent();
    }

    /// <summary>Ordena a listagem de técnicos pelo cabeçalho clicado (no banco, não só na página).</summary>
    private async void OnGridTecnicosSorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;

        if (e.Column.SortMemberPath is not string propriedade || string.IsNullOrWhiteSpace(propriedade))
            return;

        if (DataContext is TecnicosViewModel modelo)
        {
            await modelo.AplicarOrdenacaoAsync(propriedade);
        }
    }

    /// <summary>Duplo clique na linha abre a edição do técnico.</summary>
    private void OnTecnicosGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not TecnicosViewModel modelo)
            return;

        var tecnico = modelo.TecnicoSelecionado;
        if (tecnico != null)
        {
            modelo.EditarCommand.Execute(tecnico);
        }
    }
}

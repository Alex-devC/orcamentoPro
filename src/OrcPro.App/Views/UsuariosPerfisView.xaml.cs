using System.Windows.Controls;
using System.Windows.Input;
using OrcPro.App.ViewModels;

namespace OrcPro.App.Views;

/// <summary>
/// Tela do módulo Usuários e Perfis. O code-behind só resolve os eventos do DataGrid
/// (ordenação por cabeçalho e duplo clique para editar) — a lógica está nos ViewModels.
/// </summary>
public partial class UsuariosPerfisView : UserControl
{
    public UsuariosPerfisView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Ordena a listagem de usuários pelo cabeçalho clicado. O evento é tratado aqui para que a
    /// ordenação ocorra no banco (página atual) em vez de apenas reordenar a página exibida.
    /// </summary>
    private async void OnGridUsuariosSorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;

        if (e.Column.SortMemberPath is not string propriedade || string.IsNullOrWhiteSpace(propriedade))
            return;

        if (DataContext is UsuariosPerfisViewModel modulo)
        {
            await modulo.Usuarios.AplicarOrdenacaoAsync(propriedade);
        }
    }

    /// <summary>Duplo clique na linha abre a edição do usuário.</summary>
    private void OnUsuariosGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not UsuariosPerfisViewModel modulo)
            return;

        var usuario = modulo.Usuarios.UsuarioSelecionado;
        if (usuario != null)
        {
            modulo.Usuarios.EditarCommand.Execute(usuario);
        }
    }

    /// <summary>Ordena a listagem de perfis pelo cabeçalho clicado.</summary>
    private async void OnGridPerfisSorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;

        if (e.Column.SortMemberPath is not string propriedade || string.IsNullOrWhiteSpace(propriedade))
            return;

        if (DataContext is UsuariosPerfisViewModel modulo)
        {
            await modulo.Perfis.AplicarOrdenacaoAsync(propriedade);
        }
    }

    /// <summary>Duplo clique na linha abre a edição do perfil.</summary>
    private void OnPerfisGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not UsuariosPerfisViewModel modulo)
            return;

        var perfil = modulo.Perfis.PerfilSelecionado;
        if (perfil != null)
        {
            modulo.Perfis.EditarCommand.Execute(perfil);
        }
    }
}
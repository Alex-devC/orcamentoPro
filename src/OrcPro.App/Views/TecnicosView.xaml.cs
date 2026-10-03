using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using OrcPro.App.Behaviors;
using OrcPro.App.ViewModels;

namespace OrcPro.App.Views;

/// <summary>
/// Tela do cadastro de Técnicos. O code-behind resolve apenas os eventos do DataGrid
/// (ordenação por cabeçalho e duplo clique para editar) e o foco no primeiro campo
/// inválido solicitado pelo ViewModel — a lógica está nos ViewModels.
/// </summary>
public partial class TecnicosView : UserControl
{
    private TecnicosViewModel? _modeloComFoco;

    public TecnicosView()
    {
        InitializeComponent();
        DataContextChanged += OnViewDataContextChanged;
    }

    /// <summary>Assina o evento de foco do ViewModel ao trocar o DataContext (navegação entre módulos).</summary>
    private void OnViewDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_modeloComFoco is not null)
            _modeloComFoco.FocoCampoSolicitado -= OnFocoCampoSolicitado;

        _modeloComFoco = e.NewValue as TecnicosViewModel;

        if (_modeloComFoco is not null)
            _modeloComFoco.FocoCampoSolicitado += OnFocoCampoSolicitado;
    }

    private void OnFocoCampoSolicitado(object? sender, string campo)
    {
        Dispatcher.BeginInvoke(new Action(() => FormFocusHelper.FocarCampo(this, campo)), DispatcherPriority.Input);
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

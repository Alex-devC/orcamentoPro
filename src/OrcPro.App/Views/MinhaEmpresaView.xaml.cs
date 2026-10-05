using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using OrcPro.App.Behaviors;
using OrcPro.App.ViewModels;

namespace OrcPro.App.Views;

/// <summary>
/// Tela de Minha Empresa / Emitente. O code-behind resolve apenas o foco no primeiro campo
/// inválido solicitado pelo ViewModel — a lógica está no ViewModel.
/// </summary>
public partial class MinhaEmpresaView : UserControl
{
    private MinhaEmpresaViewModel? _modeloComFoco;

    public MinhaEmpresaView()
    {
        InitializeComponent();
        DataContextChanged += OnViewDataContextChanged;
    }

    private void OnViewDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_modeloComFoco is not null)
            _modeloComFoco.FocoCampoSolicitado -= OnFocoCampoSolicitado;

        _modeloComFoco = e.NewValue as MinhaEmpresaViewModel;

        if (_modeloComFoco is not null)
            _modeloComFoco.FocoCampoSolicitado += OnFocoCampoSolicitado;
    }

    private void OnFocoCampoSolicitado(object? sender, string campo)
    {
        Dispatcher.BeginInvoke(new Action(() => FormFocusHelper.FocarCampo(this, campo)), DispatcherPriority.Input);
    }
}
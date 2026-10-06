using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using OrcPro.Application.DTOs.Peca;
using OrcPro.Application.DTOs.Servico;
using OrcPro.Application.DTOs.Tecnico;
using OrcPro.App.Behaviors;
using OrcPro.App.ViewModels;

namespace OrcPro.App.Views;

/// <summary>
/// Tela de Orçamentos. O code-behind resolve apenas os eventos de UI que exigem o
/// controle concreto (adicionar/remover linhas, preencher a linha ao escolher peça ou
/// serviço, foco no primeiro campo inválido) — as regras de negócio estão no ViewModel.
/// </summary>
public partial class OrcamentosView : UserControl
{
    private OrcamentosViewModel? _modelo;

    public OrcamentosView()
    {
        InitializeComponent();
        DataContextChanged += OnViewDataContextChanged;
    }

    private void OnViewDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_modelo is not null)
            _modelo.FocoCampoSolicitado -= OnFocoCampoSolicitado;

        _modelo = e.NewValue as OrcamentosViewModel;

        if (_modelo is not null)
            _modelo.FocoCampoSolicitado += OnFocoCampoSolicitado;
    }

    private void OnFocoCampoSolicitado(object? sender, string campo)
    {
        Dispatcher.BeginInvoke(new Action(() => FormFocusHelper.FocarCampo(this, campo)), DispatcherPriority.Input);
    }

    // ---------- Linhas de peça ----------

    private void OnAdicionarItem(object sender, RoutedEventArgs e) => _modelo?.AdicionarLinhaItem();

    private void OnRemoverItem(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: OrcamentosViewModel.LinhaItem linha })
            _modelo?.RemoverLinhaItem(linha);
    }

    /// <summary>
    /// Ao escolher a peça no combo, preenche descrição, unidade e preço de venda atual.
    /// O preço é COPIADO: depois o cadastro pode mudar sem afetar este orçamento.
    /// </summary>
    private void OnSelecionarPeca(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox combo || combo.DataContext is not OrcamentosViewModel.LinhaItem linha)
            return;

        if (combo.SelectedItem is not PecaDto peca)
            return;

        linha.AplicarPeca(peca);
    }

    // ---------- Linhas de serviço ----------

    private void OnAdicionarServico(object sender, RoutedEventArgs e) => _modelo?.AdicionarLinhaServico();

    private void OnRemoverServico(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: OrcamentosViewModel.LinhaServico linha })
            _modelo?.RemoverLinhaServico(linha);
    }

    private void OnSelecionarServico(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox combo || combo.DataContext is not OrcamentosViewModel.LinhaServico linha)
            return;

        if (combo.SelectedItem is not ServicoDto servico)
            return;

        linha.AplicarServico(servico);
    }

    // ---------- Técnicos ----------

    private void OnSelecionarTecnico(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.SelectedItem is TecnicoDto tecnico)
            _modelo?.AdicionarTecnico(tecnico);
    }

    /// <summary>Associa o técnico escolhido à LINHA de mão de obra (coluna TECNICOS).</summary>
    private void OnSelecionarTecnicoLinha(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo
            && combo.DataContext is OrcamentosViewModel.LinhaServico linha
            && combo.SelectedItem is OrcamentosViewModel.TecnicoOrcamentoDto tecnico)
        {
            linha.AdicionarTecnicoLinha(tecnico);
        }
    }

    /// <summary>Remove o técnico da linha de mão de obra (chip "x" na célula).</summary>
    private void OnRemoverTecnicoLinha(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: OrcamentosViewModel.TecnicoOrcamentoDto tecnico })
            return;

        // Sobe a árvore visual até a célula cujo DataContext é a própria LinhaServico.
        DependencyObject? atual = sender as DependencyObject;
        while (atual is not null)
        {
            if (atual is FrameworkElement fe && fe.DataContext is OrcamentosViewModel.LinhaServico linha)
            {
                linha.RemoverTecnicoLinha(tecnico);
                return;
            }

            atual = VisualTreeHelper.GetParent(atual);
        }
    }

    private void OnRemoverTecnico(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: OrcamentosViewModel.TecnicoOrcamentoDto tecnico })
            _modelo?.RemoverTecnico(tecnico);
    }

    private void OnFecharStatus(object sender, RoutedEventArgs e)
        => _modelo?.CancelarAlteracaoStatus();

    // ---------- DataGrid ----------

    private async void OnGridOrcamentosSorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;

        if (e.Column.SortMemberPath is not string propriedade || string.IsNullOrWhiteSpace(propriedade))
            return;

        if (_modelo is not null)
            await _modelo.AplicarOrdenacaoAsync(propriedade);
    }

    private void OnOrcamentosGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_modelo is null || _modelo.OrcamentoSelecionado is null)
            return;

        // Sem ORCAMENTOS.EDITAR o duplo clique abre a visualização, como nos demais módulos.
        if (_modelo.PodeEditar)
            _modelo.EditarCommand.Execute(_modelo.OrcamentoSelecionado);
        else
            _modelo.VisualizarCommand.Execute(_modelo.OrcamentoSelecionado);
    }
}
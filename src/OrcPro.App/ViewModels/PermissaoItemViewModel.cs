using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace OrcPro.App.ViewModels;

/// <summary>
/// Uma permissão (código MODULO.ACAO) exibida como checkbox na tela de edição de perfil.
/// Notifica o módulo pai quando o usuário marca/desmarca.
/// </summary>
public sealed class PermissaoItemViewModel : ViewModelBase
{
    private bool _selecionada;

    public PermissaoItemViewModel(int id, string codigo, string nome, bool selecionada, bool habilitada)
    {
        Id = id;
        Codigo = codigo;
        Nome = nome;
        Habilitada = habilitada;
        _selecionada = selecionada;
    }

    public int Id { get; }

    /// <summary>Código da permissão no padrão MODULO.ACAO.</summary>
    public string Codigo { get; }

    /// <summary>Rótulo amigável exibido no checkbox.</summary>
    public string Nome { get; }

    /// <summary>Falso quando o perfil em edição não pode ser gerenciado pelo usuário atual.</summary>
    public bool Habilitada { get; }

    public bool Selecionada
    {
        get => _selecionada;
        set
        {
            if (!Habilitada || !SetField(ref _selecionada, value))
                return;

            Alterado?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Usado por "Selecionar todas" do módulo para alterar o item sem recursão.</summary>
    internal void DefinirSelecionada(bool valor)
    {
        if (SetField(ref _selecionada, valor))
            Alterado?.Invoke(this, EventArgs.Empty);
    }

    internal event EventHandler? Alterado;
}

/// <summary>
/// Permissões de um módulo, com a opção "Selecionar todas" do próprio módulo.
/// </summary>
public sealed class ModuloPermissoesViewModel : ViewModelBase
{
    public ModuloPermissoesViewModel(string modulo, string rotulo, bool habilitada)
    {
        Modulo = modulo;
        Rotulo = rotulo;
        Habilitada = habilitada;
    }

    /// <summary>Código do módulo (ex.: <c>CLIENTES</c>).</summary>
    public string Modulo { get; }

    /// <summary>Rótulo amigável do módulo (ex.: "Clientes").</summary>
    public string Rotulo { get; }

    public bool Habilitada { get; }

    public ObservableCollection<PermissaoItemViewModel> Itens { get; } = new();

    public int Total => Itens.Count;

    public int Selecionadas => Itens.Count(i => i.Selecionada);

    public bool TodasMarcadas
    {
        get => Itens.Count > 0 && Itens.All(i => i.Selecionada);
        set
        {
            if (!Habilitada || Itens.Count == 0)
                return;

            var alvo = value;

            // Só sai quando TODOS os itens já estão no estado pedido. Comparar apenas o
            // agregado erraria em módulos parcialmente marcados (ex.: 4 de 5 marcados):
            // o "Desmarcar todas" precisa limpá-los mesmo assim.
            if (Itens.All(i => i.Selecionada == alvo))
                return;

            foreach (var item in Itens)
            {
                item.DefinirSelecionada(alvo);
            }

            OnPropertyChanged(nameof(TodasMarcadas));
            OnPropertyChanged(nameof(Indeterminado));
            OnPropertyChanged(nameof(Selecionadas));
            OnPropertyChanged(nameof(Resumo));
        }
    }

    /// <summary>Indica estado parcial (nada ou parte das permissões marcadas).</summary>
    public bool Indeterminado => Selecionadas > 0 && !TodasMarcadas;

    public string Resumo => $"{Selecionadas} de {Total} selecionada(s)";

    internal void RegistrarAlteracao()
    {
        foreach (var item in Itens)
        {
            item.Alterado += OnItemAlterado;
        }
    }

    private void OnItemAlterado(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(TodasMarcadas));
        OnPropertyChanged(nameof(Indeterminado));
        OnPropertyChanged(nameof(Selecionadas));
        OnPropertyChanged(nameof(Resumo));
    }
}
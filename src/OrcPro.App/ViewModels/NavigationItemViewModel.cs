using System.Windows.Input;

namespace OrcPro.App.ViewModels;

/// <summary>
/// Item do menu lateral "Navegação do Módulo" do shell principal.
/// </summary>
public sealed class NavigationItemViewModel
{
    public NavigationItemViewModel(string title, string iconKey, ICommand command)
    {
        Title = title;
        IconKey = iconKey;
        Command = command;
    }

    /// <summary>Rótulo exibido no menu lateral.</summary>
    public string Title { get; }

    /// <summary>Chave do ícone vetorial usada pelo DataTemplate do menu.</summary>
    public string IconKey { get; }

    /// <summary>Comando executado quando o item é selecionado.</summary>
    public ICommand Command { get; }
}

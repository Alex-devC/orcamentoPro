using System;
using System.Windows;
using System.Windows.Media;
using OrcPro.App.Controls;

namespace OrcPro.App.Behaviors;

/// <summary>
/// Utilitário de foco reutilizável pelos formulários de cadastro: localiza, na árvore
/// visual, o campo marcado com <c>Tag</c> igual ao nome lógico do campo (ex.: "CEP",
/// "Nome") e move o foco para ele. Usado quando a validação aponta o primeiro campo inválido.
/// </summary>
public static class FormFocusHelper
{
    /// <summary>Move o foco para o campo identificado por <paramref name="campo"/>. Retorna true se encontrado.</summary>
    public static bool FocarCampo(DependencyObject? raiz, string? campo)
    {
        if (raiz is null || string.IsNullOrWhiteSpace(campo))
            return false;

        var alvo = LocalizarPorTag(raiz, campo);
        if (alvo is null)
            return false;

        if (alvo is CepComLookupControl cep)
        {
            cep.FocusCep();
            return true;
        }

        if (alvo is UIElement element)
            return element.Focus();

        return false;
    }

    private static DependencyObject? LocalizarPorTag(DependencyObject raiz, string tag)
    {
        var total = VisualTreeHelper.GetChildrenCount(raiz);
        for (var i = 0; i < total; i++)
        {
            var filho = VisualTreeHelper.GetChild(raiz, i);

            if (filho is FrameworkElement fe && string.Equals(fe.Tag as string, tag, StringComparison.Ordinal))
                return filho;

            var encontrado = LocalizarPorTag(filho, tag);
            if (encontrado is not null)
                return encontrado;
        }

        return null;
    }
}

using System.Windows;
using System.Windows.Controls;

namespace OrcPro.App.Controls;

/// <summary>
/// Permite ligar a senha do <see cref="PasswordBox"/> a uma propriedade do ViewModel.
/// O PasswordBox não expõe DependencyProperty de Password, então este anexo faz a
/// ponte em duas vias (padrão de MVVM, sem code-behind na View).
/// Uso: controls:PasswordBoxAssistant.BindPassword="{Binding Senha, UpdateSourceTrigger=PropertyChanged}"
/// </summary>
public static class PasswordBoxAssistant
{
    private static readonly DependencyProperty IsUpdatingProperty =
        DependencyProperty.RegisterAttached(
            "IsUpdating",
            typeof(bool),
            typeof(PasswordBoxAssistant),
            new PropertyMetadata(false));

    public static readonly DependencyProperty BindPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BindPassword",
            typeof(string),
            typeof(PasswordBoxAssistant),
            // Default null (não string.Empty): a primeira avaliação do binding envia ""
            // e a mudança null -> "" dispara o callback, que assina o PasswordChanged.
            // Com default string.Empty nunca haveria mudança e a assinatura nunca ocorreria.
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnBindPasswordChanged));

    public static string GetBindPassword(DependencyObject target) =>
        (string)target.GetValue(BindPasswordProperty);

    public static void SetBindPassword(DependencyObject target, string value) =>
        target.SetValue(BindPasswordProperty, value);

    private static bool GetIsUpdating(DependencyObject target) =>
        (bool)target.GetValue(IsUpdatingProperty);

    private static void SetIsUpdating(DependencyObject target, bool value) =>
        target.SetValue(IsUpdatingProperty, value);

    private static void OnBindPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox passwordBox)
        {
            return;
        }

        passwordBox.PasswordChanged -= OnPasswordBoxPasswordChanged;

        if (!GetIsUpdating(passwordBox))
        {
            passwordBox.Password = e.NewValue as string ?? string.Empty;
        }

        passwordBox.PasswordChanged += OnPasswordBoxPasswordChanged;
    }

    private static void OnPasswordBoxPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox passwordBox)
        {
            return;
        }

        SetIsUpdating(passwordBox, true);
        // SetCurrentValue preserva o Binding de origem (não o substitui por um valor local).
        passwordBox.SetCurrentValue(BindPasswordProperty, passwordBox.Password);
        SetIsUpdating(passwordBox, false);
    }
}

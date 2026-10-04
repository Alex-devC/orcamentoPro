using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Common.Formatters;

namespace OrcPro.App.Behaviors;

/// <summary>
/// Attached properties para formatação automática de campos de texto em TextBox.
/// Uso:
///   <TextBox local:TextFormattingBehavior.FormatMode="UpperCase" ... />
///   <TextBox local:TextFormattingBehavior.FormatMode="Email" ... />
///   <TextBox local:TextFormattingBehavior.FormatMode="CpfCnpj" ... />
///   <TextBox local:TextFormattingBehavior.FormatMode="Phone" ... />
///   <TextBox local:TextFormattingBehavior.FormatMode="Cep" ... />
/// </summary>
public static class TextFormattingBehavior
{
    public enum FormatMode
    {
        /// <summary>Nenhuma formatação automática.</summary>
        None,

        /// <summary>Converte para MAIÚSCULO (padrão do sistema).</summary>
        UpperCase,

        /// <summary>Converte para minúsculo (e-mail).</summary>
        Email,

        /// <summary>Formata CPF/CNPJ com máscara durante digitação e validação.</summary>
        CpfCnpj,

        /// <summary>Formata telefone/celular com máscara durante digitação.</summary>
        Phone,

        /// <summary>Formata CEP com máscara durante digitação.</summary>
        Cep
    }

    #region FormatMode

    public static readonly DependencyProperty FormatModeProperty =
        DependencyProperty.RegisterAttached(
            "FormatMode",
            typeof(FormatMode),
            typeof(TextFormattingBehavior),
            new PropertyMetadata(FormatMode.None, OnFormatModeChanged));

    public static FormatMode GetFormatMode(DependencyObject obj) => (FormatMode)obj.GetValue(FormatModeProperty);
    public static void SetFormatMode(DependencyObject obj, FormatMode value) => obj.SetValue(FormatModeProperty, value);

    private static void OnFormatModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox)
            return;

        // Remover handlers antigos
        textBox.TextChanged -= OnTextChanged;
        textBox.PreviewTextInput -= OnPreviewTextInput;
        DataObject.RemovePastingHandler(textBox, OnPasting);

        var mode = (FormatMode)e.NewValue;
        if (mode == FormatMode.None)
            return;

        // Adicionar handlers conforme o modo
        textBox.TextChanged += OnTextChanged;
        DataObject.AddPastingHandler(textBox, OnPasting);

        // Para CPF/CNPJ e Phone, restringir entrada a caracteres válidos
        if (mode is FormatMode.CpfCnpj or FormatMode.Phone or FormatMode.Cep)
        {
            textBox.PreviewTextInput += OnPreviewTextInput;
        }

        // Para CEP, o LostFocus é tratado via CepAutoQueryOnLostFocus property.
        // Atende a controles TextBox que não delegam a consulta automática ao ViewModel.
        // O cache por TextBox evita consultas duplicadas ao mesmo CEP, mas reseta
        // quando o texto é alterado (permitindo nova consulta após edição).
    }

    #endregion

    #region CampoInvalido (estado visual de erro de validação)

    /// <summary>
    /// Marca o campo como inválido para que o estilo (SearchTextBoxStyle/FormInputStyle)
    /// desenhe a borda em vermelho. Reutilizável por qualquer campo de texto do sistema.
    /// </summary>
    public static readonly DependencyProperty CampoInvalidoProperty =
        DependencyProperty.RegisterAttached(
            "CampoInvalido",
            typeof(bool),
            typeof(TextFormattingBehavior),
            new PropertyMetadata(false));

    public static bool GetCampoInvalido(DependencyObject obj) => (bool)obj.GetValue(CampoInvalidoProperty);
    public static void SetCampoInvalido(DependencyObject obj, bool value) => obj.SetValue(CampoInvalidoProperty, value);

    #endregion


    #region EnterBehavior (mover foco com ENTER seguindo TabIndex)

    /// <summary>
    /// Quando true, ENTER em um controle move o foco para o próximo controle
    /// com base no TabIndex e TabNavigation, sem submeter o formulário.
    /// </summary>
    public static readonly DependencyProperty EnterNavigationProperty =
        DependencyProperty.RegisterAttached(
            "EnterNavigation",
            typeof(bool),
            typeof(TextFormattingBehavior),
            new PropertyMetadata(false, OnEnterNavigationChanged));

    public static bool GetEnterNavigation(DependencyObject obj) => (bool)obj.GetValue(EnterNavigationProperty);
    public static void SetEnterNavigation(DependencyObject obj, bool value) => obj.SetValue(EnterNavigationProperty, value);

    private static void OnEnterNavigationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
            return;

        if ((bool)e.NewValue)
        {
            element.PreviewKeyDown += OnPreviewKeyDownMoveFocus;
        }
        else
        {
            element.PreviewKeyDown -= OnPreviewKeyDownMoveFocus;
        }
    }

    private static void OnPreviewKeyDownMoveFocus(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        if (sender is not UIElement element)
            return;

        // Não aplicar a controles onde ENTER tem função própria
        if (element is Button or DataGrid or ListBox)
        {
            return;
        }

        // Não aplicar a DataGrid cell editing etc.
        // Let the natural tab navigation handle focus movement
        e.Handled = true;

        var request = new TraversalRequest(FocusNavigationDirection.Next);
        element.MoveFocus(request);
    }

    #endregion

    #region CepAutoQuery (REMOVIDO DEFINITIVAMENTE — lupa é a única porta de consulta)

    /// <summary>
    /// REMOVIDO: consulta automática de CEP via LostFocus/TextChanged/8 dígitos.
    /// Digitar, apagar, sair do campo ou ENTER NUNCA consulta.
    /// A lupa (ConsultarCepCommand) é a única porta de entrada.
    /// Propriedade mantida apenas para compatibilidade de compilação/XAML legado;
    /// definir este valor NÃO anexa handlers e NUNCA executa consulta.
    /// </summary>
    [Obsolete("Consulta automática de CEP removida. A lupa é a única porta de consulta. Esta propriedade não tem efeito.")]
    public static readonly DependencyProperty CepAutoQueryOnLostFocusProperty =
        DependencyProperty.RegisterAttached(
            "CepAutoQueryOnLostFocus",
            typeof(bool),
            typeof(TextFormattingBehavior),
            new PropertyMetadata(false));

    [Obsolete("Consulta automática de CEP removida. A lupa é a única porta de consulta. Esta propriedade não tem efeito.")]
    public static bool GetCepAutoQueryOnLostFocus(DependencyObject obj) => (bool)obj.GetValue(CepAutoQueryOnLostFocusProperty);

    [Obsolete("Consulta automática de CEP removida. A lupa é a única porta de consulta. Esta propriedade não tem efeito.")]
    public static void SetCepAutoQueryOnLostFocus(DependencyObject obj, bool value) => obj.SetValue(CepAutoQueryOnLostFocusProperty, value);

    #endregion

    private static void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        var mode = GetFormatMode(textBox);
        var caretIndex = textBox.CaretIndex;
        var oldText = textBox.Text;

        string newText = mode switch
        {
            FormatMode.UpperCase => InputFormattingHelper.ToUpperCase(oldText),
            FormatMode.Email => InputFormattingHelper.ToLowerCaseEmail(oldText),
            FormatMode.CpfCnpj => FormatCpfCnpjDuringInput(oldText),
            FormatMode.Phone => PhoneMaskHelper.AplicarMascaraDigitacao(oldText),
            FormatMode.Cep => CepMaskHelper.AplicarMascaraDigitacao(oldText),
            _ => oldText
        };

        if (newText != oldText)
        {
            textBox.Text = newText;
            // Ajustar caret position
            textBox.CaretIndex = Math.Min(caretIndex + (newText.Length - oldText.Length), newText.Length);
        }
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        var mode = GetFormatMode(textBox);
        var proposedText = textBox.Text + e.Text;

        bool allow = mode switch
        {
            FormatMode.CpfCnpj => IsValidCpfCnpjInput(proposedText),
            FormatMode.Phone => IsValidPhoneInput(proposedText),
            FormatMode.Cep => IsValidCepInput(proposedText),
            _ => true
        };

        e.Handled = !allow;
    }

    private static void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        var mode = GetFormatMode(textBox);
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            var pastedText = (string)e.DataObject.GetData(typeof(string))!;
            var proposedText = textBox.Text + pastedText;

            bool allow = mode switch
            {
                FormatMode.CpfCnpj => IsValidCpfCnpjInput(proposedText),
                FormatMode.Phone => IsValidPhoneInput(proposedText),
                FormatMode.Cep => IsValidCepInput(proposedText),
                _ => true
            };

            if (!allow)
            {
                e.CancelCommand();
            }
            else
            {
                // Para UpperCase/Email, deixar o TextChanged tratar
                if (mode is FormatMode.UpperCase or FormatMode.Email)
                    return;

                // Para máscaras, formatar o texto colado
                e.CancelCommand();
                string formatted = mode switch
                {
                    FormatMode.CpfCnpj => FormatCpfCnpjDuringInput(pastedText),
                    FormatMode.Phone => PhoneMaskHelper.AplicarMascaraDigitacao(pastedText),
                    FormatMode.Cep => CepMaskHelper.AplicarMascaraDigitacao(pastedText),
                    _ => pastedText
                };
                textBox.Text = formatted;
                textBox.CaretIndex = formatted.Length;
            }
        }
    }

    private static string FormatCpfCnpjDuringInput(string text)
    {
        var normalizado = CpfCnpjValidator.Normalizar(text);
        var tipo = CpfCnpjValidator.IdentificarTipo(normalizado);

        return tipo switch
        {
            DocumentoTipo.Cpf when normalizado.Length <= 11 => FormatCpfProgressive(normalizado),
            DocumentoTipo.CnpjNumerico when normalizado.Length <= 14 => FormatCnpjNumericoProgressive(normalizado),
            DocumentoTipo.CnpjAlfanumerico when normalizado.Length <= 14 => FormatCnpjAlfanumericoProgressive(normalizado),
            _ => text
        };
    }

    private static string FormatCpfProgressive(string digitos)
    {
        if (digitos.Length <= 3) return digitos;
        if (digitos.Length <= 6) return $"{digitos[..3]}.{digitos[3..]}";
        if (digitos.Length <= 9) return $"{digitos[..3]}.{digitos[3..6]}.{digitos[6..]}";
        return $"{digitos[..3]}.{digitos[3..6]}.{digitos[6..9]}-{digitos[9..]}";
    }

    private static string FormatCnpjNumericoProgressive(string digitos)
    {
        if (digitos.Length <= 2) return digitos;
        if (digitos.Length <= 5) return $"{digitos[..2]}.{digitos[2..]}";
        if (digitos.Length <= 8) return $"{digitos[..2]}.{digitos[2..5]}.{digitos[5..]}";
        if (digitos.Length <= 12) return $"{digitos[..2]}.{digitos[2..5]}.{digitos[5..8]}/{digitos[8..]}";
        return $"{digitos[..2]}.{digitos[2..5]}.{digitos[5..8]}/{digitos[8..12]}-{digitos[12..]}";
    }

    private static string FormatCnpjAlfanumericoProgressive(string valor)
    {
        if (valor.Length <= 2) return valor;
        if (valor.Length <= 5) return $"{valor[..2]}.{valor[2..]}";
        if (valor.Length <= 8) return $"{valor[..2]}.{valor[2..5]}.{valor[5..]}";
        if (valor.Length <= 12) return $"{valor[..2]}.{valor[2..5]}.{valor[5..8]}/{valor[8..]}";
        return $"{valor[..2]}.{valor[2..5]}.{valor[5..8]}/{valor[8..12]}-{valor[12..]}";
    }

    private static bool IsValidCpfCnpjInput(string text)
    {
        // Permitir dígitos, letras (para CNPJ alfanumérico), e caracteres de máscara
        return text.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '/' || c == '-');
    }

    private static bool IsValidPhoneInput(string text)
    {
        // Permitir apenas dígitos e caracteres de máscara
        return text.All(c => char.IsDigit(c) || c == '(' || c == ')' || c == ' ' || c == '-');
    }

    private static bool IsValidCepInput(string text)
    {
        // Permitir apenas dígitos e hífen
        return text.All(c => char.IsDigit(c) || c == '-');
    }
}
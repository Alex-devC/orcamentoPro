using System;
using System.Windows;
using System.Windows.Controls;

namespace OrcPro.App.Controls;

/// <summary>
/// UserControl reutilizável que combina um TextBox de CEP (com máscara) e um
/// botão de lupa para consulta manual.
///
/// Uso em XAML:
/// <uc:CepComLookupControl CepText="{Binding FormCep}"
///                         CepCommand="{Binding ConsultarCepCommand}"
///                         CanLookup="{Binding CepPodeConsultarManualmente}"
///                         TabIndex="10"
///                         AutomationId="ClienteCepInput"
///                         LupaAutomationId="ClienteCepLupa" />
/// </summary>
public partial class CepComLookupControl : UserControl
{
    public CepComLookupControl()
    {
        InitializeComponent();
    }

    #region Propriedades de Dependency

    public static readonly DependencyProperty CepTextProperty =
        DependencyProperty.Register(
            nameof(CepText),
            typeof(string),
            typeof(CepComLookupControl),
            new PropertyMetadata(string.Empty));

    public string CepText
    {
        get => (string)GetValue(CepTextProperty);
        set => SetValue(CepTextProperty, value);
    }

    public static readonly DependencyProperty CepCommandProperty =
        DependencyProperty.Register(
            nameof(CepCommand),
            typeof(System.Windows.Input.ICommand),
            typeof(CepComLookupControl),
            new PropertyMetadata(null));

    public System.Windows.Input.ICommand? CepCommand
    {
        get => (System.Windows.Input.ICommand?)GetValue(CepCommandProperty);
        set => SetValue(CepCommandProperty, value);
    }

    public static readonly DependencyProperty CanLookupProperty =
        DependencyProperty.Register(
            nameof(CanLookup),
            typeof(bool),
            typeof(CepComLookupControl),
            new PropertyMetadata(true));

    public bool CanLookup
    {
        get => (bool)GetValue(CanLookupProperty);
        set => SetValue(CanLookupProperty, value);
    }

    public static readonly DependencyProperty AutomationIdProperty =
        DependencyProperty.Register(
            nameof(AutomationId),
            typeof(string),
            typeof(CepComLookupControl),
            new PropertyMetadata(string.Empty));

    public string AutomationId
    {
        get => (string)GetValue(AutomationIdProperty);
        set => SetValue(AutomationIdProperty, value);
    }

    public static readonly DependencyProperty LupaAutomationIdProperty =
        DependencyProperty.Register(
            nameof(LupaAutomationId),
            typeof(string),
            typeof(CepComLookupControl),
            new PropertyMetadata("CepLupa"));

    public string LupaAutomationId
    {
        get => (string)GetValue(LupaAutomationIdProperty);
        set => SetValue(LupaAutomationIdProperty, value);
    }

    /// <summary>Indica que o CEP atual está em estado de erro (borda vermelha + tooltip).</summary>
    public static readonly DependencyProperty TemErroProperty =
        DependencyProperty.Register(
            nameof(TemErro),
            typeof(bool),
            typeof(CepComLookupControl),
            new PropertyMetadata(false));

    public bool TemErro
    {
        get => (bool)GetValue(TemErroProperty);
        set => SetValue(TemErroProperty, value);
    }

    /// <summary>Mensagem de erro associada ao campo (usada no tooltip).</summary>
    public static readonly DependencyProperty MensagemErroProperty =
        DependencyProperty.Register(
            nameof(MensagemErro),
            typeof(string),
            typeof(CepComLookupControl),
            new PropertyMetadata(string.Empty));

    public string MensagemErro
    {
        get => (string)GetValue(MensagemErroProperty);
        set => SetValue(MensagemErroProperty, value);
    }

    private void OnLupaClick(object sender, RoutedEventArgs e)
    {
        // Diagnóstico do fluxo de CEP: registra o clique no nível da UI, ANTES de
        // chegar ao comando do ViewModel. Se esta linha não aparecer em logcep.txt,
        // o problema está no clique/binding; se aparecer mas não chegar ao serviço,
        // o problema está no comando — ver logcep.txt ao lado do executável.
        OrcPro.Application.Services.CepDiagnosticLogger.Linha(
            $"[CEP] BOTÃO LUPA CLICADO (UI) — campo: '{CepText}'");
    }

    /// <summary>Move o foco para o TextBox interno de CEP (usado pela navegação/foco do formulário).</summary>
    public void FocusCep()
    {
        CepTextBox.Focus();
        CepTextBox.CaretIndex = CepTextBox.Text.Length;
    }

    #endregion
}

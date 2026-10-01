using System.Windows;
using System.Windows.Input;
using OrcPro.Application.DTOs.Auth;
using OrcPro.Application.Interfaces.Services;
using OrcPro.App.ViewModels;

namespace OrcPro.App;

/// <summary>
/// Janela de login real do OrcPro. A View apenas compõe a UI e repassa o
/// <see cref="IAuthService"/> (registrado no DI) ao <see cref="LoginViewModel"/>;
/// toda a decisão de autenticação fica no ViewModel/serviço (MVVM).
/// </summary>
public partial class LoginWindow : Window
{
    /// <summary>
    /// Disparado quando o login é validado; o payload é a sessão do usuário autenticado.
    /// O assinante (App) abre a MainWindow e então esta janela se fecha.
    /// </summary>
    public event Action<UsuarioSessaoDto>? LoginRealizado;

    public LoginWindow(IAuthService authService)
    {
        InitializeComponent();

        var viewModel = new LoginViewModel(authService);
        viewModel.LoginRealizado += sessao =>
        {
            LoginRealizado?.Invoke(sessao);
            Close();
        };
        DataContext = viewModel;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Foco inicial no campo de usuário para digitação imediata.
        UsernameBox.Focus();
        Keyboard.Focus(UsernameBox);
    }

    /// <summary>Minimiza a janela (handlers code-behind, mesmo padrão da MainWindow).</summary>
    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    /// <summary>
    /// Exibe/oculta o watermark da senha. O PasswordBox.Password não é DependencyProperty,
    /// então não há Trigger possível no template — o estado é tratado aqui.
    /// </summary>
    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (PasswordInput.Template?.FindName("Watermark", PasswordInput) is FrameworkElement watermark)
        {
            watermark.Visibility = string.IsNullOrEmpty(PasswordInput.Password)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    /// <summary>Fecha a janela; o App encerra o processo se o login ainda não concluiu.</summary>
    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

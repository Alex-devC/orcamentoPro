using System.IO;
using System.Windows;
using OrcPro.Application.DTOs.Auth;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Infrastructure.DependencyInjection;
using OrcPro.Infrastructure.Persistence.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace OrcPro.App;

/// <summary>
/// Ponto de composição do aplicativo: cria o contêiner DI (serviços de Infraestrutura
/// já registrados via <c>AddInfrastructure</c>), inicializa a base de dados e inicia o
/// fluxo real de login (LoginWindow → MainWindow). O ShutdownMode inicial é
/// OnExplicitShutdown para que o fechamento do LoginWindow não encerre o processo
/// antes da MainWindow ser exibida.
/// </summary>
public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;
    private bool _loginConcluido;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1) Contêiner DI — infraestrutura já vem registrada via AddInfrastructure.
        try
        {
            var services = new ServiceCollection();
            services.AddInfrastructure(DatabaseConnectionOptions.DefaultSqlite(
                Path.Combine(AppContext.BaseDirectory, "OrcPro.db")));
            _serviceProvider = services.BuildServiceProvider();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Não foi possível montar os serviços do aplicativo:\n\n{ex.Message}",
                "OrcPro — Inicialização",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }

        // 2) Base de dados (criação do schema + usuário inicial). Falha não é fatal:
        //    o login tentará normalmente e a mensagem aparece na própria janela.
        try
        {
            await DatabaseInitializer.InitializeAsync(_serviceProvider!);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Aviso: não foi possível preparar a base de dados:\n\n{ex.Message}\n\n" +
                "O login será exibido; caso a base permaneça indisponível, o erro aparecerá ao autenticar.",
                "OrcPro — Banco de dados",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        // 3) Janela de login. Um escopo por janela: o IAuthService (scoped) vive até o login terminar.
        try
        {
            IServiceScope scope = _serviceProvider!.CreateScope();
            var loginWindow = new LoginWindow(scope.ServiceProvider.GetRequiredService<IAuthService>());
            loginWindow.LoginRealizado += OnLoginRealizado;
            loginWindow.Closed += (_, _) =>
            {
                scope.Dispose();
                if (!_loginConcluido)
                {
                    Shutdown();
                }
            };
            loginWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Não foi possível abrir a janela de login:\n\n{ex.Message}",
                "OrcPro — Inicialização",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    /// <summary>
    /// Chamado pelo LoginWindow após autenticação válida: abre a MainWindow com a sessão
    /// do usuário, transfere o shutdown para a janela principal e fecha o login.
    /// </summary>
    private void OnLoginRealizado(UsuarioSessaoDto sessao)
    {
        _loginConcluido = true;

        var mainWindow = new MainWindow(sessao, _serviceProvider!);
        MainWindow = mainWindow;
        mainWindow.Show();

        // A partir daqui, fechar a MainWindow encerra o aplicativo.
        ShutdownMode = ShutdownMode.OnMainWindowClose;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}


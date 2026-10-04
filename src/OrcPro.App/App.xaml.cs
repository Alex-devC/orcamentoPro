using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using OrcPro.Application.DTOs.Auth;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Infrastructure.DependencyInjection;
using OrcPro.Infrastructure.Persistence.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace OrcPro.App;

/// <summary>
/// Registro mínimo de erros fatais do aplicativo. Grava a exceção (com stack trace) em
/// <c>logs/orcpro-erros.log</c>, ao lado do executável, para que a causa possa ser
/// diagnosticada depois — inclusive quando o erro ocorre durante a criação de uma tela.
/// </summary>
internal static class AppErrorLog
{
    private static readonly object Lock = new();

    /// <summary>Nome do arquivo de log usado pelo <see cref="Registrar"/>.</summary>
    public const string Arquivo = "orcpro-erros.log";

    /// <summary>
    /// Acrescenta a exceção ao log. Falhas de escrita são ignoradas: o log é um
    /// diagnóstico auxiliar e nunca pode ser o motivo de uma nova falha.
    /// </summary>
    public static void Registrar(Exception excecao, string origem)
    {
        try
        {
            var pasta = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(pasta);

            var caminho = Path.Combine(pasta, Arquivo);
            var entrada = new StringBuilder()
                .AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {origem}")
                .AppendLine(excecao.ToString())
                .AppendLine(new string('-', 72))
                .ToString();

            lock (Lock)
            {
                File.AppendAllText(caminho, entrada, Encoding.UTF8);
            }
        }
        catch
        {
            // Log é acessório: nunca propagar falha de logging.
        }
    }
}

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

        // Proteção adicional: sem isto, qualquer exceção não tratada na thread de UI
        // encerra o processo sem mensagem. O erro continua sendo registrado em log
        // (a causa raiz é tratada no código, não aqui).
        DispatcherUnhandledException += OnDispatcherUnhandledException;

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
    /// Última linha de defesa: registra a exceção não tratada e informa o usuário em vez
    /// de encerrar o aplicativo em silêncio. Não substitui a correção da causa — serve
    /// para que uma falha isolada não derrube a sessão de trabalho do operador.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppErrorLog.Registrar(e.Exception, "Exceção não tratada na interface");

        MessageBox.Show(
            "Ocorreu um erro inesperado ao abrir ou usar a tela e a operação foi cancelada.\n\n" +
            $"Detalhes técnicos:\n{e.Exception.Message}\n\n" +
            $"O erro foi registrado em: logs\\{AppErrorLog.Arquivo}",
            "OrcPro — Erro inesperado",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
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


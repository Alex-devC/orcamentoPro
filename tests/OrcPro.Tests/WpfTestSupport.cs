using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Xunit;

namespace OrcPro.Tests;

/// <summary>
/// Infra compartilhada para testes que instanciam telas/controles WPF:
/// execução em thread STA e criação garantida de uma única <see cref="System.Windows.Application"/>
/// com os dicionários de tema.
///
/// Todas as classes que criam views devem declarar <c>[Collection(WpfTestSupport.Colecao)]</c>
/// para que rodem sequencialmente — dois métodos criando Application em paralelo
/// resultariam em <see cref="InvalidOperationException"/>.
/// </summary>
public static class WpfTestSupport
{
    public const string Colecao = "WpfViews";

    public static void RunOnStaThread(Action action)
    {
        var tcs = new TaskCompletionSource<object?>();
        var thread = new Thread(() =>
        {
            try
            {
                EnsureApplicationResources();
                action();
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        // Propaga qualquer exceção ocorrida na thread STA
        if (tcs.Task.IsFaulted && tcs.Task.Exception != null)
            throw tcs.Task.Exception.InnerException ?? tcs.Task.Exception;
    }

    /// <summary>
    /// Executa a fábrica na thread STA e devolve o valor produzido. Usado para criar
    /// ViewModels (que dependem do <c>Dispatcher</c>) diretamente nos testes.
    /// </summary>
    public static T RunOnStaThread<T>(Func<T> func)
    {
        T? resultado = default;
        Exception? erro = null;

        RunOnStaThread(() =>
        {
            try
            {
                EnsureApplicationResources();
                resultado = func();
            }
            catch (Exception ex)
            {
                erro = ex;
            }
        });

        if (erro != null)
            throw erro;

        return resultado!;
    }

    /// <summary>
    /// Executa a ação na thread STA e mantém a fila do <see cref="Dispatcher"/> ativa até a
    /// condição informada ser satisfeita. Necessário para os comandos assíncronos dos
    /// ViewModels (<c>AsyncRelayCommand.Execute</c> é <c>async void</c>): sem bombear a fila
    /// do Dispatcher as continuações após <c>await</c> nunca executam.
    /// </summary>
    public static void RunOnStaThreadUntil(Action action, Func<bool> condicao, int timeoutMs = 15000)
    {
        Exception? erro = null;
        bool concluida = false;

        RunOnStaThread(() =>
        {
            try
            {
                action();

                var limite = Environment.TickCount64 + timeoutMs;
                while (!condicao() && Environment.TickCount64 < limite)
                {
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
                    Thread.Sleep(10);
                }

                concluida = condicao();
            }
            catch (Exception ex)
            {
                erro = ex;
            }
        });

        if (erro != null)
            throw erro;

        if (!concluida)
            throw new TimeoutException(
                $"A condição não foi satisfeita em {timeoutMs}ms na thread de UI.");
    }

    /// <summary>Application é singleton por AppDomain: a criação é feita sob lock.</summary>
    private static readonly object ApplicationLock = new();

    /// <summary>
    /// Garante que uma Application WPF com os recursos do tema foi criada.
    /// Application é singleton por AppDomain: a verificação é feita sob lock.
    /// </summary>
    public static void EnsureApplicationResources()
    {
        lock (ApplicationLock)
        {
            if (System.Windows.Application.Current is not null)
                return;

            var app = new System.Windows.Application();
            app.Resources.MergedDictionaries.Add(
                new System.Windows.ResourceDictionary
                {
                    Source = new Uri("/OrcPro.App;component/Resources/Themes/MainTheme.xaml", UriKind.Relative)
                });
            app.Resources.MergedDictionaries.Add(
                new System.Windows.ResourceDictionary
                {
                    Source = new Uri("/OrcPro.App;component/Resources/CadastroModuleStyles.xaml", UriKind.Relative)
                });

            // Mesmo dicionário do App.xaml: permite validar que o DataTemplate do
            // ViewModel do módulo resolve e instancia a View correspondente.
            app.Resources.MergedDictionaries.Add(
                new System.Windows.ResourceDictionary
                {
                    Source = new Uri("/OrcPro.App;component/Resources/DataTemplates.xaml", UriKind.Relative)
                });
        }
    }
}

/// <summary>Define a coleção de testes de views WPF (executada de forma serializada).</summary>
[CollectionDefinition(WpfTestSupport.Colecao)]
public class WpfViewsCollectionDefinition
{
}

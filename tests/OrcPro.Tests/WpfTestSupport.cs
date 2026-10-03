using System;
using System.Threading;
using System.Threading.Tasks;
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
        }
    }
}

/// <summary>Define a coleção de testes de views WPF (executada de forma serializada).</summary>
[CollectionDefinition(WpfTestSupport.Colecao)]
public class WpfViewsCollectionDefinition
{
}

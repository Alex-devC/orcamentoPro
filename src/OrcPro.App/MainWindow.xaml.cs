using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using OrcPro.Application.DTOs.Auth;
using OrcPro.App.ViewModels;

namespace OrcPro.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const int WmGetMinMaxInfo = 0x0024;
    private const uint MonitorDefaultToNearest = 2;

    /// <summary>
    /// Recebe a sessão do usuário autenticado no login e o provedor de serviços do aplicativo,
    /// repassados ao shell (title bar, rodapé, Dashboard e módulos como Usuários e Perfis).
    /// </summary>
    public MainWindow(UsuarioSessaoDto sessao, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel(
            sessao ?? throw new ArgumentNullException(nameof(sessao)),
            serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider)));

        Closed += (_, _) => (DataContext as IDisposable)?.Dispose();
    }

    /// <summary>
    /// Anexa o hook de <c>WM_GETMINMAXINFO</c> no momento em que o HWND existe, antes de a
    /// janela ser exibida/maximizada (o <see cref="Window.WindowState"/> inicial é Maximized).
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (HwndSource.FromHwnd(hwnd) is HwndSource source)
        {
            source.AddHook(WindowMessageHook);
        }
    }

    /// <summary>
    /// Minimiza a janela. O comportamento é tratado aqui (e não via <c>SystemCommands</c>) porque
    /// esses comandos são <see cref="System.Windows.Input.RoutedCommand"/> sem CommandBinding
    /// registrado na janela: o CanExecute fica falso e o botão permanece desabilitado.
    /// </summary>
    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    /// <summary>Alterna entre maximizado e restaurado; o ícone do botão segue a propriedade WindowState.</summary>
    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    /// <summary>Fecha a janela; como o aplicativo usa o ShutdownMode padrão, o processo é encerrado.</summary>
    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Processa mensagens da janela. Enquanto <c>handled</c> permanece falso, o processamento
    /// padrão do Windows (DefWndProc) continua encadeado normalmente.
    /// </summary>
    private IntPtr WindowMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmGetMinMaxInfo)
        {
            ApplyWorkAreaMaximizeBounds(hwnd, lParam);
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Janelas sem <c>WS_CAPTION</c> (<c>WindowStyle="None"</c>) são maximizadas por padrão sobre
    /// o retângulo inteiro do monitor, cobrindo a taskbar (a barra de status e o rodapé do
    /// aplicativo ficariam escondidos atrás dela). Este método reescreve os limites da maximização
    /// para usar a área de trabalho (rcWork) do monitor onde a janela está, preservando arrasto,
    /// snap e redimensionamento nativos e o suporte a múltiplos monitores.
    /// </summary>
    private static void ApplyWorkAreaMaximizeBounds(IntPtr hwnd, IntPtr lParam)
    {
        IntPtr monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return;
        }

        var monitorInfo = new NativeMonitorInfo { cbSize = Marshal.SizeOf<NativeMonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return;
        }

        NativeMinMaxInfo minMaxInfo = Marshal.PtrToStructure<NativeMinMaxInfo>(lParam);
        minMaxInfo.ptMaxPosition.x = monitorInfo.rcWork.left - monitorInfo.rcMonitor.left;
        minMaxInfo.ptMaxPosition.y = monitorInfo.rcWork.top - monitorInfo.rcMonitor.top;
        minMaxInfo.ptMaxSize.x = monitorInfo.rcWork.right - monitorInfo.rcWork.left;
        minMaxInfo.ptMaxSize.y = monitorInfo.rcWork.bottom - monitorInfo.rcWork.top;
        Marshal.StructureToPtr(minMaxInfo, lParam, false);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMinMaxInfo
    {
        public NativePoint ptReserved;
        public NativePoint ptMaxSize;
        public NativePoint ptMaxPosition;
        public NativePoint ptMinTrackSize;
        public NativePoint ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMonitorInfo
    {
        public int cbSize;
        public NativeRect rcMonitor;
        public NativeRect rcWork;
        public int dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref NativeMonitorInfo lpmi);
}

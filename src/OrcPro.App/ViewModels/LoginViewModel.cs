using System;
using System.Threading.Tasks;
using System.Windows.Input;
using OrcPro.Application.DTOs.Auth;
using OrcPro.Application.Interfaces.Services;
using OrcPro.App.Services;

namespace OrcPro.App.ViewModels;

/// <summary>
/// ViewModel da janela de login: valida os campos, autentica via <see cref="IAuthService"/>
/// existente (que já valida usuário ativo e conferência do hash de senha) e comunica o
/// sucesso através do evento <see cref="LoginRealizado"/>. Não há segundo sistema de
/// autenticação aqui — apenas apresentação + orquestração.
/// </summary>
public class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _authService;

    private string _username = string.Empty;
    private string _senha = string.Empty;
    private bool _lembrarUsuario;
    private string? _mensagemErro;
    private bool _isBusy;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));

        var lembrado = RememberedUserStore.Load();
        if (!string.IsNullOrWhiteSpace(lembrado))
        {
            _username = lembrado;
            _lembrarUsuario = true;
        }

        EntrarCommand = new AsyncRelayCommand(_ => EntrarAsync());
    }

    /// <summary>Disparado após autenticação válida; entrega a sessão do usuário logado.</summary>
    public event Action<UsuarioSessaoDto>? LoginRealizado;

    /// <summary>Comando "Entrar"; desabilitado enquanto a autenticação está em andamento.</summary>
    public ICommand EntrarCommand { get; }

    public string Username
    {
        get => _username;
        set => SetField(ref _username, value);
    }

    public string Senha
    {
        get => _senha;
        set => SetField(ref _senha, value);
    }

    public bool LembrarUsuario
    {
        get => _lembrarUsuario;
        set => SetField(ref _lembrarUsuario, value);
    }

    /// <summary>Mensagem de erro exibida na janela (credenciais inválidas, campos vazios etc.).</summary>
    public string? MensagemErro
    {
        get => _mensagemErro;
        private set
        {
            if (SetField(ref _mensagemErro, value))
            {
                OnPropertyChanged(nameof(TemErro));
            }
        }
    }

    /// <summary>True quando há mensagem de erro (usado pelos gatilhos visuais da View).</summary>
    public bool TemErro => !string.IsNullOrEmpty(_mensagemErro);

    /// <summary>True enquanto a autenticação está em andamento (troca o rótulo do botão).</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    private async Task EntrarAsync()
    {
        MensagemErro = null;

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Senha))
        {
            MensagemErro = "Informe o usuário e a senha.";
            return;
        }

        IsBusy = true;
        try
        {
            var response = await _authService.LoginAsync(new LoginRequestDto
            {
                Username = Username.Trim(),
                Senha = Senha
            });

            if (!response.Sucesso || response.Usuario is null)
            {
                MensagemErro = response.Mensagem ?? "Usuário ou senha inválidos.";
                return;
            }

            if (LembrarUsuario)
            {
                RememberedUserStore.Save(Username.Trim());
            }
            else
            {
                RememberedUserStore.Clear();
            }

            LoginRealizado?.Invoke(response.Usuario);
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível autenticar: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

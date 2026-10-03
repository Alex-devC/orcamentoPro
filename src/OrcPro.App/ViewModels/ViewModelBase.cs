using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OrcPro.App.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public virtual Task InitializeAsync() => Task.CompletedTask;

    // ===================== Validação de formulário (infraestrutura compartilhada) =====================
    //
    // Mecanismo único e reutilizável por todos os cadastros (Clientes, Técnicos, Peças e futuros).
    // Mantém o estado de erro por campo (chave = nome lógico do campo, ex.: "CEP") e o resumo
    // exibido no topo do formulário. Os ViewModels derivados expõem propriedades visuais
    // (ex.: CepTemErro) apoiadas em CampoInvalido("<Campo>").
    // ==================================================================================================

    private readonly List<string> _ordemCampos = new();
    private readonly Dictionary<string, string> _errosValidacao = new(StringComparer.Ordinal);

    /// <summary>Mensagens de erro exibidas no resumo de validação (topo do formulário).</summary>
    public ObservableCollection<string> ResumoValidacao { get; } = new();

    /// <summary>Dicionário de erros atuais por campo lógico (somente leitura).</summary>
    public IReadOnlyDictionary<string, string> ErrosValidacao => _errosValidacao;

    /// <summary>Indica se há algum erro de validação a ser exibido.</summary>
    public bool TemResumoValidacao => _errosValidacao.Count > 0;

    /// <summary>Primeiro campo inválido, na ordem em que o erro foi registrado.</summary>
    public string? PrimeiroCampoInvalido => _ordemCampos.Count > 0 ? _ordemCampos[0] : null;

    /// <summary>Indica se o campo lógico informado está atualmente com erro.</summary>
    public bool CampoInvalido(string campo) => _errosValidacao.ContainsKey(campo);

    /// <summary>
    /// Solicitado quando o formulário precisa levar o foco ao campo informado
    /// (ex.: primeiro campo inválido). A View escuta e move o foco.
    /// </summary>
    public event EventHandler<string>? FocoCampoSolicitado;

    /// <summary>Registra (ou substitui) o erro de um campo e atualiza o resumo.</summary>
    protected void DefinirErroValidacao(string campo, string mensagem)
    {
        if (string.IsNullOrWhiteSpace(campo) || string.IsNullOrWhiteSpace(mensagem))
            return;

        if (!_errosValidacao.ContainsKey(campo))
            _ordemCampos.Add(campo);

        _errosValidacao[campo] = mensagem;
        SincronizarResumoValidacao();
        AoAlterarValidacao(campo);
    }

    /// <summary>Remove o erro de um campo (se existir) e atualiza o resumo.</summary>
    protected void LimparErroValidacao(string campo)
    {
        if (string.IsNullOrWhiteSpace(campo) || !_errosValidacao.Remove(campo))
            return;

        _ordemCampos.Remove(campo);
        SincronizarResumoValidacao();
        AoAlterarValidacao(campo);
    }

    /// <summary>Remove todos os erros de validação e limpa o resumo.</summary>
    protected void LimparErrosValidacao()
    {
        if (_errosValidacao.Count == 0)
            return;

        var campos = _ordemCampos.ToArray();
        _errosValidacao.Clear();
        _ordemCampos.Clear();
        SincronizarResumoValidacao();

        foreach (var campo in campos)
            AoAlterarValidacao(campo);
    }

    private void SincronizarResumoValidacao()
    {
        ResumoValidacao.Clear();
        foreach (var campo in _ordemCampos)
        {
            if (_errosValidacao.TryGetValue(campo, out var mensagem))
                ResumoValidacao.Add(mensagem);
        }

        OnPropertyChanged(nameof(TemResumoValidacao));
        OnPropertyChanged(nameof(PrimeiroCampoInvalido));
    }

    /// <summary>Solicita que a View mova o foco para o primeiro campo inválido.</summary>
    protected void SolicitarFocoPrimeiroCampoInvalido()
    {
        if (PrimeiroCampoInvalido is { } campo)
            FocoCampoSolicitado?.Invoke(this, campo);
    }

    /// <summary>
    /// Hook invocado sempre que o estado de validação de um campo muda.
    /// ViewModels derivados sobrescrevem para notificar propriedades visuais (ex.: CepTemErro).
    /// </summary>
    protected virtual void AoAlterarValidacao(string campo)
    {
    }
}

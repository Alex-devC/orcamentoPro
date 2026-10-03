using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using OrcPro.Application.DTOs.Auth;
using OrcPro.Application.DTOs.Common;
using OrcPro.Application.DTOs.Perfil;
using OrcPro.Application.DTOs.Usuario;
using OrcPro.Application.Exceptions;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Common;
using OrcPro.Domain.Common.Formatters;

namespace OrcPro.App.ViewModels;

/// <summary>Opção do filtro de perfil da listagem de usuários (Id 0 = "Todos os perfis").</summary>
public sealed class FiltroPerfilItem
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
}

/// <summary>
/// Tela de Usuários: DataGrid com pesquisa, filtros, paginação e ações (novo, editar,
/// ativar/inativar e excluir). O formulário de cadastro é um modal no padrão de UX do OrcPro.
/// O login continua usando o campo Usuário — a senha nunca é exibida, apenas trocada.
/// </summary>
public class UsuariosViewModel : ViewModelBase
{
    private const int PageSize = 20;
    private const int DebounceBuscaMilissegundos = 350;

    private readonly IUsuarioService _usuarioService;
    private readonly IPerfilService _perfilService;
    private readonly int _usuarioLogadoId;
    private readonly Action<string>? _reportStatus;
    private readonly DispatcherTimer _buscaTimer;

    private readonly bool _podeCriar;
    private readonly bool _podeEditar;
    private readonly bool _podeExcluir;
    private readonly bool _podeAtivarInativar;

    private UsuarioDto? _usuarioSelecionado;
    private UsuarioDto? _usuarioParaExcluir;

    private string _busca = string.Empty;
    private int _situacaoFiltro;
    private int _perfilFiltroId;
    private int _pagina = 1;
    private int _totalPaginas = 1;
    private int _totalRegistros;
    private SortRequest? _ordenacao;

    private bool _editorAberto;
    private bool _editorNovo;
    private int _formId;
    private string _editorTitulo = string.Empty;
    private string _formNome = string.Empty;
    private string _formUsuario = string.Empty;
    private string _formSenha = string.Empty;
    private string _formEmail = string.Empty;
    private PerfilDto? _formPerfil;
    private bool _formAtivo = true;
    private string _editorMensagem = string.Empty;

    private bool _confirmacaoAberta;

    public UsuariosViewModel(
        IUsuarioService usuarioService,
        IPerfilService perfilService,
        UsuarioSessaoDto sessao,
        Action<string>? reportStatus = null)
    {
        _usuarioService = usuarioService;
        _perfilService = perfilService;
        _usuarioLogadoId = sessao.Id;
        _reportStatus = reportStatus;

        // Permissões do módulo (USUARIOS_PERFIS.*) — o perfil Administrador recebe todas
        // automaticamente pelo sincronizador do catálogo.
        _podeCriar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.UsuariosPerfis.Criar);
        _podeEditar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.UsuariosPerfis.Editar);
        _podeExcluir = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.UsuariosPerfis.Excluir);
        _podeAtivarInativar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.UsuariosPerfis.Editar);

        _buscaTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceBuscaMilissegundos) };
        _buscaTimer.Tick += async (_, _) =>
        {
            _buscaTimer.Stop();
            await CarregarAsync();
        };

        NovoCommand = new RelayCommand(_ => _ = AbrirNovoAsync(), _ => _podeCriar);
        EditarCommand = new RelayCommand(p => _ = AbrirEdicaoAsync(p as UsuarioDto ?? UsuarioSelecionado), _ => _podeEditar);
        SalvarCommand = new AsyncRelayCommand(_ => SalvarAsync());
        CancelarEditorCommand = new RelayCommand(_ => FecharEditor());
        AlternarSituacaoCommand = new AsyncRelayCommand(p => AlternarSituacaoAsync(p as UsuarioDto), _ => _podeAtivarInativar);
        ExcluirCommand = new RelayCommand(p => AbrirConfirmacaoExclusao(p as UsuarioDto ?? UsuarioSelecionado), _ => _podeExcluir);
        ConfirmarExclusaoCommand = new AsyncRelayCommand(_ => ExcluirAsync());
        CancelarExclusaoCommand = new RelayCommand(_ => FecharConfirmacao());
        AtualizarListaCommand = new AsyncRelayCommand(_ => CarregarAsync());
        LimparBuscaCommand = new RelayCommand(_ => Busca = string.Empty);
        PaginaAnteriorCommand = new AsyncRelayCommand(_ => IrParaPagina(Pagina - 1), _ => PodePaginaAnterior);
        PaginaProximaCommand = new AsyncRelayCommand(_ => IrParaPagina(Pagina + 1), _ => PodePaginaProxima);
    }

    // ---------- Permissões do módulo ----------

    public bool PodeCriar => _podeCriar;
    public bool PodeEditar => _podeEditar;
    public bool PodeExcluir => _podeExcluir;
    public bool PodeAtivarInativar => _podeAtivarInativar;

    public ObservableCollection<UsuarioDto> Usuarios { get; } = new();

    /// <summary>Perfis ativos exibidos no formulário (e nas opções de filtro).</summary>
    public ObservableCollection<PerfilDto> PerfisDisponiveis { get; } = new();

    public ObservableCollection<FiltroPerfilItem> FiltrosPerfil { get; } = new();
// ---------- Listagem, pesquisa e filtros ----------

    public string Busca
    {
        get => _busca;
        set
        {
            if (!SetField(ref _busca, value)) return;
            _buscaTimer.Stop();
            _buscaTimer.Start();
        }
    }

    /// <summary>0 = todos, 1 = somente ativos, 2 = somente inativos.</summary>
    public int SituacaoFiltro
    {
        get => _situacaoFiltro;
        set
        {
            if (!SetField(ref _situacaoFiltro, value)) return;
            Pagina = 1;
            _ = CarregarAsync();
        }
    }

    public int PerfilFiltroId
    {
        get => _perfilFiltroId;
        set
        {
            if (!SetField(ref _perfilFiltroId, value)) return;
            Pagina = 1;
            _ = CarregarAsync();
        }
    }

    public UsuarioDto? UsuarioSelecionado
    {
        get => _usuarioSelecionado;
        set => SetField(ref _usuarioSelecionado, value);
    }

    public int Pagina
    {
        get => _pagina;
        private set => SetField(ref _pagina, value);
    }

    public int TotalPaginas
    {
        get => _totalPaginas;
        private set => SetField(ref _totalPaginas, value);
    }

    public int TotalRegistros
    {
        get => _totalRegistros;
        private set => SetField(ref _totalRegistros, value);
    }

    public bool PodePaginaAnterior => Pagina > 1;

    public bool PodePaginaProxima => Pagina < TotalPaginas;

    public bool TemRegistros => Usuarios.Count > 0;

    /// <summary>Resumo do rodapé do grid: página atual, total de páginas e registros.</summary>
    public string ResumoPaginacao
    {
        get
        {
            var registro = TotalRegistros == 1 ? "registro" : "registros";
            return $"Página {Pagina} de {TotalPaginas} · {TotalRegistros} {registro}";
        }
    }

    // ---------- Formulário (modal) ----------

    public bool EditorAberto
    {
        get => _editorAberto;
        private set => SetField(ref _editorAberto, value);
    }

    public bool EditorNovo => _editorNovo;

    /// <summary>Na edição o campo Usuário fica bloqueado: é a chave do login.</summary>
    public bool UsuarioBloqueado => !_editorNovo;

    public string EditorTitulo
    {
        get => _editorTitulo;
        private set => SetField(ref _editorTitulo, value);
    }

    public string FormNome
    {
        get => _formNome;
        set => SetField(ref _formNome, value);
    }

    public string FormUsuario
    {
        get => _formUsuario;
        set => SetField(ref _formUsuario, value);
    }

    public string FormSenha
    {
        get => _formSenha;
        set => SetField(ref _formSenha, value);
    }

    public string FormEmail
    {
        get => _formEmail;
        set => SetField(ref _formEmail, value);
    }

    public PerfilDto? FormPerfil
    {
        get => _formPerfil;
        set => SetField(ref _formPerfil, value);
    }

    public bool FormAtivo
    {
        get => _formAtivo;
        set => SetField(ref _formAtivo, value);
    }

    public string EditorMensagem
    {
        get => _editorMensagem;
        private set
        {
            // EditorMensagemVisivel deriva deste valor: precisa ser notificado junto para
            // que o DataTrigger do modal mostre/esconda a faixa de erro.
            if (SetField(ref _editorMensagem, value))
                OnPropertyChanged(nameof(EditorMensagemVisivel));
        }
    }

    public bool EditorMensagemVisivel => !string.IsNullOrWhiteSpace(EditorMensagem);

    /// <summary>Texto de ajuda do campo senha (diferente entre novo e edição).</summary>
    public string DicaSenha => _editorNovo
        ? "A senha será gravada com hash (nunca em texto puro)."
        : "Deixe em branco para manter a senha atual.";

    // ---------- Confirmação de exclusão ----------

    public bool ConfirmacaoAberta
    {
        get => _confirmacaoAberta;
        private set => SetField(ref _confirmacaoAberta, value);
    }

    public string ConfirmacaoTitulo => "Excluir usuário";

    public string ConfirmacaoMensagem => _usuarioParaExcluir is null
        ? string.Empty
        : $"Deseja excluir o usuário \"{_usuarioParaExcluir.Username}\" ({_usuarioParaExcluir.NomeCompleto})? Esta ação não pode ser desfeita.";

    // ---------- Comandos ----------

    public RelayCommand NovoCommand { get; }
    public RelayCommand EditarCommand { get; }
    public AsyncRelayCommand SalvarCommand { get; }
    public RelayCommand CancelarEditorCommand { get; }
    public AsyncRelayCommand AlternarSituacaoCommand { get; }
    public RelayCommand ExcluirCommand { get; }
    public AsyncRelayCommand ConfirmarExclusaoCommand { get; }
    public RelayCommand CancelarExclusaoCommand { get; }
    public AsyncRelayCommand AtualizarListaCommand { get; }
    public RelayCommand LimparBuscaCommand { get; }
    public AsyncRelayCommand PaginaAnteriorCommand { get; }
    public AsyncRelayCommand PaginaProximaCommand { get; }
// ---------- Carga de dados ----------

    public override async Task InitializeAsync()
    {
        await CarregarPerfisAsync();
        await CarregarAsync();
    }

    /// <summary>Recarrega a lista respeitando busca, filtros, ordenação e página.</summary>
    public async Task CarregarAsync()
    {
        var request = new PagedRequest
        {
            PageNumber = Pagina,
            PageSize = PageSize,
            SearchTerm = string.IsNullOrWhiteSpace(Busca) ? null : Busca
        };

        if (SituacaoFiltro == 1)
            request.Filters.Add(new FilterRequest { PropertyName = "Ativo", Value = bool.TrueString, Operation = "Equals" });
        else if (SituacaoFiltro == 2)
            request.Filters.Add(new FilterRequest { PropertyName = "Ativo", Value = bool.FalseString, Operation = "Equals" });

        if (PerfilFiltroId > 0)
            request.Filters.Add(new FilterRequest { PropertyName = "PerfilId", Value = PerfilFiltroId.ToString(), Operation = "Equals" });

        if (_ordenacao != null)
            request.Sorts.Add(_ordenacao);

        try
        {
            var result = await _usuarioService.ListarPaginadoAsync(request);

            Usuarios.Clear();
            foreach (var usuario in result.Items)
            {
                Usuarios.Add(usuario);
            }

            TotalRegistros = result.TotalCount;
            TotalPaginas = result.TotalPages > 0 ? result.TotalPages : 1;
            Pagina = Math.Min(Math.Max(Pagina, 1), TotalPaginas);

            OnPropertyChanged(nameof(ResumoPaginacao));
            OnPropertyChanged(nameof(TemRegistros));
            OnPropertyChanged(nameof(PodePaginaAnterior));
            OnPropertyChanged(nameof(PodePaginaProxima));
        }
        catch (Exception ex)
        {
            _reportStatus?.Invoke($"Falha ao carregar usuários: {ex.Message}");
        }
    }

    /// <summary>Ordenação disparada pelo clique no cabeçalho da coluna do DataGrid.</summary>
    public async Task AplicarOrdenacaoAsync(string propriedade)
    {
        if (string.IsNullOrWhiteSpace(propriedade))
            return;

        _ordenacao = _ordenacao != null && string.Equals(_ordenacao.PropertyName, propriedade, StringComparison.Ordinal)
            ? new SortRequest(propriedade, !_ordenacao.IsDescending)
            : new SortRequest(propriedade);

        await CarregarAsync();
    }

    private async Task IrParaPagina(int pagina)
    {
        if (pagina < 1 || pagina > TotalPaginas)
            return;

        Pagina = pagina;
        await CarregarAsync();
    }

    /// <summary>Perfis ativos: opções do formulário e do filtro da listagem.</summary>
    public async Task CarregarPerfisAsync()
    {
        try
        {
            var ativos = await _perfilService.ListarAtivosAsync();

            PerfisDisponiveis.Clear();
            foreach (var perfil in ativos)
            {
                PerfisDisponiveis.Add(perfil);
            }

            FiltrosPerfil.Clear();
            FiltrosPerfil.Add(new FiltroPerfilItem { Id = 0, Nome = "Todos os perfis" });
            foreach (var perfil in PerfisDisponiveis)
            {
                FiltrosPerfil.Add(new FiltroPerfilItem { Id = perfil.Id, Nome = perfil.Nome });
            }

            if (PerfilFiltroId != 0 && PerfisDisponiveis.All(p => p.Id != PerfilFiltroId))
            {
                PerfilFiltroId = 0;
            }
        }
        catch (Exception ex)
        {
            _reportStatus?.Invoke($"Falha ao carregar perfis: {ex.Message}");
        }
    }
// ---------- Formulário ----------

    private async Task AbrirNovoAsync()
    {
        if (PerfisDisponiveis.Count == 0)
        {
            await CarregarPerfisAsync();
        }

        if (PerfisDisponiveis.Count == 0)
        {
            _reportStatus?.Invoke("Cadastre um perfil na aba Perfis antes de cadastrar usuários.");
            return;
        }

        _editorNovo = true;
        _formId = 0;
        EditorTitulo = "Novo usuário";
        FormNome = string.Empty;
        FormUsuario = string.Empty;
        FormSenha = string.Empty;
        FormEmail = string.Empty;
        FormAtivo = true;
        FormPerfil = PerfisDisponiveis.FirstOrDefault();
        EditorMensagem = string.Empty;
        EditorAberto = true;

        OnPropertyChanged(nameof(EditorNovo));
        OnPropertyChanged(nameof(UsuarioBloqueado));
        OnPropertyChanged(nameof(DicaSenha));
    }

    private Task AbrirEdicaoAsync(UsuarioDto? usuario)
    {
        if (usuario == null)
            return Task.CompletedTask;

        if (PerfisDisponiveis.All(p => p.Id != usuario.PerfilId))
        {
            // Inclui o perfil atual (mesmo inativo) para não perder a informação do cadastro.
            PerfisDisponiveis.Add(new PerfilDto { Id = usuario.PerfilId, Nome = usuario.PerfilNome, Ativo = true });
        }

        _editorNovo = false;
        _formId = usuario.Id;
        EditorTitulo = "Editar usuário";
        FormNome = usuario.NomeCompleto;
        FormUsuario = usuario.Username;
        FormSenha = string.Empty;
        FormEmail = usuario.Email ?? string.Empty;
        FormAtivo = usuario.Ativo;
        FormPerfil = PerfisDisponiveis.FirstOrDefault(p => p.Id == usuario.PerfilId);
        EditorMensagem = string.Empty;
        EditorAberto = true;

        OnPropertyChanged(nameof(EditorNovo));
        OnPropertyChanged(nameof(UsuarioBloqueado));
        OnPropertyChanged(nameof(DicaSenha));

        return Task.CompletedTask;
    }

    private void FecharEditor()
    {
        EditorAberto = false;
        EditorMensagem = string.Empty;
        FormSenha = string.Empty;
    }

    private async Task SalvarAsync()
    {
        if (string.IsNullOrWhiteSpace(FormNome))
        {
            EditorMensagem = "Informe o nome do usuário.";
            return;
        }

        if (EditorNovo && string.IsNullOrWhiteSpace(FormUsuario))
        {
            EditorMensagem = "Informe o usuário de acesso (login).";
            return;
        }

        if (EditorNovo && string.IsNullOrWhiteSpace(FormSenha))
        {
            EditorMensagem = "Informe a senha inicial do usuário.";
            return;
        }

        if (FormPerfil == null)
        {
            EditorMensagem = "Selecione o perfil do usuário.";
            return;
        }

        var nome = FormNome.Trim();

        try
        {
            if (EditorNovo)
            {
                await _usuarioService.CriarAsync(new CriarUsuarioDto
                {
                    Username = FormUsuario.Trim(),
                    NomeCompleto = nome,
                    Senha = FormSenha,
                    Email = FormEmail,
                    PerfilId = FormPerfil.Id,
                    Ativo = FormAtivo
                });
            }
            else
            {
                await _usuarioService.AtualizarAsync(new AtualizarUsuarioDto
                {
                    Id = _formId,
                    NomeCompleto = nome,
                    Email = FormEmail,
                    PerfilId = FormPerfil.Id,
                    Ativo = FormAtivo,
                    NovaSenha = string.IsNullOrWhiteSpace(FormSenha) ? null : FormSenha
                });
            }

            var edicao = !EditorNovo;
            FecharEditor();
            await CarregarAsync();
            _reportStatus?.Invoke(edicao ? $"Usuário \"{nome}\" atualizado." : $"Usuário \"{nome}\" cadastrado.");
        }
        catch (AppException ex)
        {
            EditorMensagem = ex.Message;
        }
        catch (Exception ex)
        {
            EditorMensagem = $"Falha ao salvar o usuário: {ex.Message}";
        }
    }
private async Task AlternarSituacaoAsync(UsuarioDto? usuario)
    {
        if (usuario == null)
            return;

        if (usuario.Id == _usuarioLogadoId)
        {
            _reportStatus?.Invoke(usuario.Ativo
                ? "Não é possível inativar o usuário com a sessão aberta."
                : "Não é possível alterar o usuário com a sessão aberta.");
            return;
        }

        var novaSituacao = !usuario.Ativo;
        var nome = usuario.Username;

        try
        {
            await _usuarioService.AlterarStatusAtivoAsync(usuario.Id, novaSituacao);
            await CarregarAsync();
            _reportStatus?.Invoke($"Usuário \"{nome}\" {(novaSituacao ? "ativado" : "inativado")}.");
        }
        catch (Exception ex)
        {
            _reportStatus?.Invoke($"Falha ao alterar a situação: {ex.Message}");
        }
    }

    // ---------- Exclusão ----------

    private void AbrirConfirmacaoExclusao(UsuarioDto? usuario)
    {
        if (usuario == null)
            return;

        if (usuario.Id == _usuarioLogadoId)
        {
            _reportStatus?.Invoke("Não é possível excluir o usuário com a sessão aberta.");
            return;
        }

        _usuarioParaExcluir = usuario;
        ConfirmacaoAberta = true;
    }

    private void FecharConfirmacao()
    {
        ConfirmacaoAberta = false;
        _usuarioParaExcluir = null;
    }

    private async Task ExcluirAsync()
    {
        if (_usuarioParaExcluir == null)
            return;

        var usuario = _usuarioParaExcluir;

        try
        {
            await _usuarioService.ExcluirAsync(usuario.Id);
            FecharConfirmacao();
            await CarregarAsync();
            _reportStatus?.Invoke($"Usuário \"{usuario.Username}\" excluído.");
        }
        catch (Exception ex)
        {
            FecharConfirmacao();
            _reportStatus?.Invoke(ex.Message);
        }
    }
}

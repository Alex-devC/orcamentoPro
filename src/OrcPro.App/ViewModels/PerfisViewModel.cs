using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using OrcPro.Application.DTOs.Auth;
using OrcPro.Application.DTOs.Common;
using OrcPro.Application.DTOs.Perfil;
using OrcPro.Application.Exceptions;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Common;

namespace OrcPro.App.ViewModels;

/// <summary>
/// Tela de Perfis: DataGrid com pesquisa, filtro de situação, paginação e ações.
/// Nome, descrição e situação são validados no <c>IPerfilService</c>; o vínculo de permissões já
/// é persistido pelo serviço (estrutura pronta para a tela de permissões).
/// </summary>
public class PerfisViewModel : ViewModelBase
{
    private const int PageSize = 20;
    private const int DebounceBuscaMilissegundos = 350;

    private readonly IPerfilService _perfilService;
    private readonly IPermissaoService _permissaoService;
    private readonly UsuarioSessaoDto _sessao;
    private readonly Action<string>? _reportStatus;
    private readonly DispatcherTimer _buscaTimer;

    private readonly bool _podeCriar;
    private readonly bool _podeEditar;
    private readonly bool _podeExcluir;
    private readonly bool _podeAtivarInativar;
    private readonly bool _podeGerenciarPermissoes;
    private IReadOnlyList<PermissaoDto> _catalogoPermissoes = Array.Empty<PermissaoDto>();

    private PerfilDto? _perfilSelecionado;
    private PerfilDto? _perfilParaExcluir;

    private string _busca = string.Empty;
    private int _situacaoFiltro;
    private int _pagina = 1;
    private int _totalPaginas = 1;
    private int _totalRegistros;
    private SortRequest? _ordenacao;

    private bool _editorAberto;
    private bool _editorNovo;
    private int _formId;
    private string _editorTitulo = string.Empty;
    private string _formNome = string.Empty;
    private string _formDescricao = string.Empty;
    private bool _formAtivo = true;
    private string _editorMensagem = string.Empty;
    private List<int> _permissoesAtuais = new();

    private bool _confirmacaoAberta;

    public PerfisViewModel(
        IPerfilService perfilService,
        IPermissaoService permissaoService,
        UsuarioSessaoDto sessao,
        Action<string>? reportStatus = null)
    {
        _perfilService = perfilService;
        _permissaoService = permissaoService;
        _sessao = sessao;
        _reportStatus = reportStatus;

        _podeCriar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.UsuariosPerfis.Criar);
        _podeEditar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.UsuariosPerfis.Editar);
        _podeExcluir = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.UsuariosPerfis.Excluir);
        _podeAtivarInativar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.UsuariosPerfis.Editar);
        _podeGerenciarPermissoes = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.UsuariosPerfis.GerenciarPermissoes);

        _buscaTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceBuscaMilissegundos) };
        _buscaTimer.Tick += async (_, _) =>
        {
            _buscaTimer.Stop();
            await CarregarAsync();
        };

        NovoCommand = new RelayCommand(_ => AbrirNovo(), _ => _podeCriar);
        EditarCommand = new RelayCommand(p => AbrirEdicao(p as PerfilDto ?? PerfilSelecionado), _ => _podeEditar);
        SalvarCommand = new AsyncRelayCommand(_ => SalvarAsync());
        CancelarEditorCommand = new RelayCommand(_ => FecharEditor());
        AlternarSituacaoCommand = new AsyncRelayCommand(p => AlternarSituacaoAsync(p as PerfilDto), _ => _podeAtivarInativar);
        ExcluirCommand = new RelayCommand(p => AbrirConfirmacaoExclusao(p as PerfilDto ?? PerfilSelecionado), _ => _podeExcluir);
        ConfirmarExclusaoCommand = new AsyncRelayCommand(_ => ExcluirAsync());
        CancelarExclusaoCommand = new RelayCommand(_ => FecharConfirmacao());
        AtualizarListaCommand = new AsyncRelayCommand(_ => CarregarAsync());
        LimparBuscaCommand = new RelayCommand(_ => Busca = string.Empty);
        PaginaAnteriorCommand = new AsyncRelayCommand(_ => IrParaPagina(Pagina - 1), _ => PodePaginaAnterior);
        PaginaProximaCommand = new AsyncRelayCommand(_ => IrParaPagina(Pagina + 1), _ => PodePaginaProxima);
        SelecionarTodasCommand = new RelayCommand(_ => SelecionarTodas(true), _ => _podeGerenciarPermissoes);
        DesmarcarTodasCommand = new RelayCommand(_ => SelecionarTodas(false), _ => _podeGerenciarPermissoes);
    }

    public ObservableCollection<PerfilDto> Perfis { get; } = new();

    /// <summary>Permissões agrupadas por módulo exibidas no formulário de perfil.</summary>
    public ObservableCollection<ModuloPermissoesViewModel> Modulos { get; } = new();

    /// <summary>Notifica o módulo quando um perfil muda (a aba Usuários recarrega as opções).</summary>
    public event EventHandler? PerfisAlterados;

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

    public PerfilDto? PerfilSelecionado
    {
        get => _perfilSelecionado;
        set => SetField(ref _perfilSelecionado, value);
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

    public bool TemRegistros => Perfis.Count > 0;

    public string ResumoPaginacao
    {
        get
        {
            var registro = TotalRegistros == 1 ? "registro" : "registros";
            return $"Página {Pagina} de {TotalPaginas} · {TotalRegistros} {registro}";
        }
    }
public bool EditorAberto
    {
        get => _editorAberto;
        private set => SetField(ref _editorAberto, value);
    }

    public bool EditorNovo => _editorNovo;

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

    public string FormDescricao
    {
        get => _formDescricao;
        set => SetField(ref _formDescricao, value);
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

    /// <summary>Resumo das permissões já vinculadas — a edição por perfil entra em versão futura.</summary>
    // ---------- Permissões do perfil ----------

    /// <summary>Permite cadastrar perfis (USUARIOS_PERFIS.CRIAR).</summary>
    public bool PodeCriar => _podeCriar;

    /// <summary>Permite editar perfis (USUARIOS_PERFIS.EDITAR).</summary>
    public bool PodeEditar => _podeEditar;

    /// <summary>Permite excluir perfis (USUARIOS_PERFIS.EXCLUIR).</summary>
    public bool PodeExcluir => _podeExcluir;

    /// <summary>Permite ativar/inativar perfis (USUARIOS_PERFIS.EDITAR).</summary>
    public bool PodeAtivarInativar => _podeAtivarInativar;

    /// <summary>
    /// Permite marcar/desmarcar permissões (USUARIOS_PERFIS.GERENCIAR_PERMISSOES).
    /// Sem essa permissão o usuário ainda vê o que o perfil possui, mas não pode alterar.
    /// </summary>
    public bool PodeGerenciarPermissoes => _podeGerenciarPermissoes;

    public bool CatalogoCarregado => _catalogoPermissoes.Count > 0;

    public int TotalPermissoesCatalogo => _catalogoPermissoes.Count;

    public int PermissoesSelecionadas => Modulos.Sum(m => m.Selecionadas);

    public string ResumoPermissoesSelecionadas => $"{PermissoesSelecionadas} de {TotalPermissoesCatalogo} permissão(ões) selecionada(s)";

    /// <summary>Estado geral das permissões (checkbox "Selecionar todas" do formulário).</summary>
    public bool TodasPermissoesMarcadas
    {
        get => Modulos.Count > 0 && Modulos.All(m => m.TodasMarcadas);
        set => SelecionarTodas(value);
    }

    public RelayCommand SelecionarTodasCommand { get; }
    public RelayCommand DesmarcarTodasCommand { get; }

    public string ResumoPermissoes => _permissoesAtuais.Count == 0
        ? "Nenhuma permissão vinculada."
        : $"{_permissoesAtuais.Count} permissão(ões) vinculada(s).";

    public bool ConfirmacaoAberta
    {
        get => _confirmacaoAberta;
        private set => SetField(ref _confirmacaoAberta, value);
    }

    public string ConfirmacaoMensagem => _perfilParaExcluir is null
        ? string.Empty
        : $"Deseja excluir o perfil \"{_perfilParaExcluir.Nome}\"? Esta ação não pode ser desfeita.";

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
        await CarregarCatalogoPermissoesAsync();
        await CarregarAsync();
    }

    /// <summary>
    /// Carrega o catálogo de permissões uma única vez por abertura do módulo. A base é
    /// populada pelo <c>PermissaoSincronizador</c> na inicialização do aplicativo.
    /// </summary>
    private async Task CarregarCatalogoPermissoesAsync()
    {
        if (_catalogoPermissoes.Count > 0)
            return;

        try
        {
            _catalogoPermissoes = await _permissaoService.ListarTodasAsync();
        }
        catch (Exception ex)
        {
            _reportStatus?.Invoke($"Falha ao carregar as permissões do catálogo: {ex.Message}");
        }
    }

    /// <summary>
    /// Monta os checkboxes agrupados por módulo, marcando as permissões informadas
    /// (as já salvas no perfil).
    /// </summary>
    private void MontarModulosPermissoes(IEnumerable<int> permissaoIdsSelecionadas)
    {
        var selecionadas = permissaoIdsSelecionadas?.ToHashSet() ?? new HashSet<int>();

        Modulos.Clear();

        foreach (var grupo in _catalogoPermissoes
                     .GroupBy(p => p.Modulo, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var modulo = new ModuloPermissoesViewModel(grupo.Key, PermissaoCatalogo.RotuloModulo(grupo.Key), _podeGerenciarPermissoes);

            foreach (var permissao in grupo.OrderBy(p => p.Codigo, StringComparer.Ordinal))
            {
                modulo.Itens.Add(new PermissaoItemViewModel(
                    permissao.Id,
                    permissao.Codigo,
                    permissao.Nome,
                    selecionadas.Contains(permissao.Id),
                    _podeGerenciarPermissoes));
            }

            modulo.RegistrarAlteracao();
            Modulos.Add(modulo);
        }

        NotificarAlteracaoPermissoes();
    }

    private void SelecionarTodas(bool marcadas)
    {
        if (!_podeGerenciarPermissoes)
            return;

        foreach (var modulo in Modulos)
        {
            modulo.TodasMarcadas = marcadas;
        }

        NotificarAlteracaoPermissoes();
    }

    /// <summary>Ids marcados no formulário (o que será gravado em PerfilPermissao).</summary>
    private List<int> ObterPermissaoIdsSelecionadas()
        => Modulos.SelectMany(m => m.Itens)
            .Where(i => i.Selecionada)
            .Select(i => i.Id)
            .ToList();

    private void NotificarAlteracaoPermissoes()
    {
        OnPropertyChanged(nameof(PermissoesSelecionadas));
        OnPropertyChanged(nameof(ResumoPermissoesSelecionadas));
        OnPropertyChanged(nameof(TodasPermissoesMarcadas));
        OnPropertyChanged(nameof(CatalogoCarregado));
        OnPropertyChanged(nameof(TotalPermissoesCatalogo));
    }

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

        if (_ordenacao != null)
            request.Sorts.Add(_ordenacao);

        try
        {
            var result = await _perfilService.ListarPaginadoAsync(request);

            Perfis.Clear();
            foreach (var perfil in result.Items)
            {
                Perfis.Add(perfil);
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
            _reportStatus?.Invoke($"Falha ao carregar perfis: {ex.Message}");
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
// ---------- Formulário ----------

    private void AbrirNovo()
    {
        _editorNovo = true;
        _formId = 0;
        EditorTitulo = "Novo perfil";
        FormNome = string.Empty;
        FormDescricao = string.Empty;
        FormAtivo = true;
        _permissoesAtuais = new List<int>();
        MontarModulosPermissoes(Array.Empty<int>());
        EditorMensagem = string.Empty;
        EditorAberto = true;

        OnPropertyChanged(nameof(EditorNovo));
        OnPropertyChanged(nameof(ResumoPermissoes));
    }

    private void AbrirEdicao(PerfilDto? perfil)
    {
        if (perfil == null)
            return;

        _editorNovo = false;
        _formId = perfil.Id;
        EditorTitulo = "Editar perfil";
        FormNome = perfil.Nome;
        FormDescricao = perfil.Descricao ?? string.Empty;
        FormAtivo = perfil.Ativo;
        _permissoesAtuais = perfil.Permissoes.Select(p => p.Id).ToList();

        // Carrega as permissões já salvas no perfil (marcadas) e remove as desmarcadas ao salvar.
        MontarModulosPermissoes(_permissoesAtuais);
        EditorMensagem = string.Empty;
        EditorAberto = true;

        OnPropertyChanged(nameof(EditorNovo));
        OnPropertyChanged(nameof(ResumoPermissoes));
    }

    private void FecharEditor()
    {
        EditorAberto = false;
        EditorMensagem = string.Empty;
    }

    private async Task SalvarAsync()
    {
        if (string.IsNullOrWhiteSpace(FormNome))
        {
            EditorMensagem = "Informe o nome do perfil.";
            return;
        }

        var nome = FormNome.Trim();
        var permissaoIds = _podeGerenciarPermissoes ? ObterPermissaoIdsSelecionadas() : new List<int>(_permissoesAtuais);

        try
        {
            if (EditorNovo)
            {
                await _perfilService.CriarAsync(new SalvarPerfilDto
                {
                    Nome = nome,
                    Descricao = FormDescricao,
                    Ativo = FormAtivo,
                    PermissaoIds = permissaoIds
                });
            }
            else
            {
                await _perfilService.AtualizarAsync(new SalvarPerfilDto
                {
                    Id = _formId,
                    Nome = nome,
                    Descricao = FormDescricao,
                    Ativo = FormAtivo,
                    PermissaoIds = permissaoIds
                });
            }

            var edicao = !EditorNovo;
            FecharEditor();
            await CarregarAsync();
            PerfisAlterados?.Invoke(this, EventArgs.Empty);
            _reportStatus?.Invoke(edicao ? $"Perfil \"{nome}\" atualizado." : $"Perfil \"{nome}\" cadastrado.");
        }
        catch (AppException ex)
        {
            EditorMensagem = ex.Message;
        }
        catch (Exception ex)
        {
            EditorMensagem = $"Falha ao salvar o perfil: {ex.Message}";
        }
    }

    private async Task AlternarSituacaoAsync(PerfilDto? perfil)
    {
        if (perfil == null)
            return;

        var novaSituacao = !perfil.Ativo;
        var nome = perfil.Nome;

        try
        {
            await _perfilService.AlterarStatusAtivoAsync(perfil.Id, novaSituacao);
            await CarregarAsync();
            PerfisAlterados?.Invoke(this, EventArgs.Empty);
            _reportStatus?.Invoke($"Perfil \"{nome}\" {(novaSituacao ? "ativado" : "inativado")}.");
        }
        catch (Exception ex)
        {
            _reportStatus?.Invoke(ex.Message);
        }
    }

    // ---------- Exclusão ----------

    private void AbrirConfirmacaoExclusao(PerfilDto? perfil)
    {
        if (perfil == null)
            return;

        _perfilParaExcluir = perfil;
        ConfirmacaoAberta = true;
    }

    private void FecharConfirmacao()
    {
        ConfirmacaoAberta = false;
        _perfilParaExcluir = null;
    }

    private async Task ExcluirAsync()
    {
        if (_perfilParaExcluir == null)
            return;

        var perfil = _perfilParaExcluir;

        try
        {
            await _perfilService.ExcluirAsync(perfil.Id);
            FecharConfirmacao();
            await CarregarAsync();
            PerfisAlterados?.Invoke(this, EventArgs.Empty);
            _reportStatus?.Invoke($"Perfil \"{perfil.Nome}\" excluído.");
        }
        catch (Exception ex)
        {
            FecharConfirmacao();
            _reportStatus?.Invoke(ex.Message);
        }
    }
}

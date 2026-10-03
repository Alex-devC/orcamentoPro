using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Threading;
using OrcPro.Application.DTOs.Auth;
using OrcPro.Application.DTOs.Common;
using OrcPro.Application.DTOs.Peca;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Common;
using OrcPro.Domain.Common.Formatters;

namespace OrcPro.App.ViewModels;

/// <summary>
/// Tela de Peças / Itens: DataGrid com ordenação no banco, pesquisa com debounce, filtros e
/// paginação, além das ações Novo, Editar, Visualizar, Ativar/Inativar e Excluir (com
/// confirmação). Peças com itens de orçamento vinculados só podem ser inativadas, pois a
/// exclusão é bloqueada pelo serviço. Código único; preços validados ao salvar.
/// </summary>
public class PecasViewModel : ViewModelBase
{
    private const int PageSize = 20;
    private const int DebounceBuscaMilissegundos = 350;

    private readonly IPecaService _pecaService;
    private readonly Action<string>? _reportStatus;
    private readonly DispatcherTimer _buscaTimer;

    private readonly bool _podeVisualizar;
    private readonly bool _podeCriar;
    private readonly bool _podeEditar;
    private readonly bool _podeExcluir;
    private readonly bool _podeAtivarInativar;

    private PecaDto? _pecaSelecionada;
    private PecaDto? _pecaParaExcluir;

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
    private string _formCodigo = string.Empty;
    private string _formDescricao = string.Empty;
    private string _formCategoria = string.Empty;
    private string _formMarca = string.Empty;
    private string _formModelo = string.Empty;
    private string _formCodigoBarras = string.Empty;
    private string _formUnidadeMedida = "UN";
    private decimal _formPrecoCusto;
    private decimal _formPrecoVenda;
    private decimal _formEstoqueAtual;
    private decimal _formEstoqueMinimo;
    private string _formObservacoes = string.Empty;
    private bool _formAtivo = true;
    private string _editorMensagem = string.Empty;

    private bool _visualizacaoAberta;
    private bool _confirmacaoAberta;

    public PecasViewModel(
        IPecaService pecaService,
        UsuarioSessaoDto sessao,
        Action<string>? reportStatus = null)
    {
        _pecaService = pecaService;
        _reportStatus = reportStatus;

        _podeVisualizar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Pecas.Visualizar);
        _podeCriar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Pecas.Criar);
        _podeEditar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Pecas.Editar);
        _podeExcluir = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Pecas.Excluir);
        _podeAtivarInativar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Pecas.AtivarInativar);

        _buscaTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceBuscaMilissegundos) };
        _buscaTimer.Tick += OnBuscaTimerTick;

        NovoCommand = new RelayCommand(_ => AbrirNovo(), _ => _podeCriar);
        EditarCommand = new RelayCommand(p => AbrirEdicao(p as PecaDto ?? PecaSelecionada), _ => _podeEditar);
        VisualizarCommand = new RelayCommand(p => AbrirVisualizacao(p as PecaDto ?? PecaSelecionada), _ => _podeVisualizar);
        SalvarCommand = new AsyncRelayCommand(_ => SalvarAsync());
        CancelarEditorCommand = new RelayCommand(_ => FecharEditor());
        FecharVisualizacaoCommand = new RelayCommand(_ => FecharVisualizacao());
        AlternarSituacaoCommand = new AsyncRelayCommand(p => AlternarSituacaoAsync(p as PecaDto), _ => _podeAtivarInativar);
        ExcluirCommand = new RelayCommand(p => AbrirConfirmacaoExclusao(p as PecaDto ?? PecaSelecionada), _ => _podeExcluir);
        ConfirmarExclusaoCommand = new AsyncRelayCommand(_ => ExcluirAsync());
        CancelarExclusaoCommand = new RelayCommand(_ => FecharConfirmacao());
        AtualizarListaCommand = new AsyncRelayCommand(_ => CarregarAsync());
        LimparBuscaCommand = new RelayCommand(_ => Busca = string.Empty);
        PaginaAnteriorCommand = new AsyncRelayCommand(_ => IrParaPagina(Pagina - 1), _ => PodePaginaAnterior);
        PaginaProximaCommand = new AsyncRelayCommand(_ => IrParaPagina(Pagina + 1), _ => PodePaginaProxima);
    }

    public bool PodeVisualizar => _podeVisualizar;
    public bool PodeCriar => _podeCriar;
    public bool PodeEditar => _podeEditar;
    public bool PodeExcluir => _podeExcluir;
    public bool PodeAtivarInativar => _podeAtivarInativar;

    public ObservableCollection<PecaDto> Pecas { get; } = new();

    public string Busca
    {
        get => _busca;
        set
        {
            if (!SetField(ref _busca, value)) return;
            AgendarRecarga();
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

    public PecaDto? PecaSelecionada
    {
        get => _pecaSelecionada;
        set => SetField(ref _pecaSelecionada, value);
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

    public bool TemRegistros => Pecas.Count > 0;

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

    public string FormCodigo
    {
        get => _formCodigo;
        set => SetField(ref _formCodigo, value);
    }

    public string FormDescricao
    {
        get => _formDescricao;
        set => SetField(ref _formDescricao, value);
    }

    public string FormCategoria
    {
        get => _formCategoria;
        set => SetField(ref _formCategoria, value);
    }

    public string FormMarca
    {
        get => _formMarca;
        set => SetField(ref _formMarca, value);
    }

    public string FormModelo
    {
        get => _formModelo;
        set => SetField(ref _formModelo, value);
    }

    public string FormCodigoBarras
    {
        get => _formCodigoBarras;
        set => SetField(ref _formCodigoBarras, value);
    }

    public string FormUnidadeMedida
    {
        get => _formUnidadeMedida;
        set => SetField(ref _formUnidadeMedida, value);
    }

    public decimal FormPrecoCusto
    {
        get => _formPrecoCusto;
        set => SetField(ref _formPrecoCusto, value);
    }

    public decimal FormPrecoVenda
    {
        get => _formPrecoVenda;
        set => SetField(ref _formPrecoVenda, value);
    }

    public decimal FormEstoqueAtual
    {
        get => _formEstoqueAtual;
        set => SetField(ref _formEstoqueAtual, value);
    }

    public decimal FormEstoqueMinimo
    {
        get => _formEstoqueMinimo;
        set => SetField(ref _formEstoqueMinimo, value);
    }

    public string FormObservacoes
    {
        get => _formObservacoes;
        set => SetField(ref _formObservacoes, value);
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
            if (SetField(ref _editorMensagem, value))
                OnPropertyChanged(nameof(EditorMensagemVisivel));
        }
    }

    public bool EditorMensagemVisivel => !string.IsNullOrWhiteSpace(EditorMensagem);

    public bool VisualizacaoAberta
    {
        get => _visualizacaoAberta;
        private set => SetField(ref _visualizacaoAberta, value);
    }

    public bool ConfirmacaoAberta
    {
        get => _confirmacaoAberta;
        private set => SetField(ref _confirmacaoAberta, value);
    }

    public string ConfirmacaoMensagem => _pecaParaExcluir is null
        ? string.Empty
        : $"Deseja excluir a peça \"{_pecaParaExcluir.Descricao}\" ({_pecaParaExcluir.Codigo})? Esta ação não pode ser desfeita.";

    public RelayCommand NovoCommand { get; }
    public RelayCommand EditarCommand { get; }
    public RelayCommand VisualizarCommand { get; }
    public AsyncRelayCommand SalvarCommand { get; }
    public RelayCommand CancelarEditorCommand { get; }
    public RelayCommand FecharVisualizacaoCommand { get; }
    public AsyncRelayCommand AlternarSituacaoCommand { get; }
    public RelayCommand ExcluirCommand { get; }
    public AsyncRelayCommand ConfirmarExclusaoCommand { get; }
    public RelayCommand CancelarExclusaoCommand { get; }
    public AsyncRelayCommand AtualizarListaCommand { get; }
    public RelayCommand LimparBuscaCommand { get; }
    public AsyncRelayCommand PaginaAnteriorCommand { get; }
    public AsyncRelayCommand PaginaProximaCommand { get; }

    public override async Task InitializeAsync()
    {
        await CarregarAsync();
    }

    public async Task CarregarAsync()
    {
        var request = new PagedRequest
        {
            PageNumber = Pagina,
            PageSize = PageSize,
            SearchTerm = string.IsNullOrWhiteSpace(Busca) ? null : Busca
        };

        request.Filters.Add(new FilterRequest
        {
            PropertyName = "Status",
            Value = SituacaoFiltro switch { 1 => "ativos", 2 => "inativos", _ => "todos" }
        });

        if (_ordenacao != null)
            request.Sorts.Add(_ordenacao);

        try
        {
            var result = await _pecaService.ListarPaginadoAsync(request);

            Pecas.Clear();
            foreach (var peca in result.Items)
            {
                Pecas.Add(peca);
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
            _reportStatus?.Invoke($"Falha ao carregar peças: {ex.Message}");
        }
    }

    public async Task AplicarOrdenacaoAsync(string propriedade)
    {
        if (string.IsNullOrWhiteSpace(propriedade))
            return;

        _ordenacao = _ordenacao != null && string.Equals(_ordenacao.PropertyName, propriedade, StringComparison.Ordinal)
            ? new SortRequest(propriedade, !_ordenacao.IsDescending)
            : new SortRequest(propriedade);

        await CarregarAsync();
    }

    private void AgendarRecarga()
    {
        _buscaTimer.Stop();
        _buscaTimer.Start();
    }

    private async void OnBuscaTimerTick(object? sender, EventArgs e)
    {
        _buscaTimer.Stop();
        Pagina = 1;
        await CarregarAsync();
    }

    private async Task IrParaPagina(int pagina)
    {
        if (pagina < 1 || pagina > TotalPaginas)
            return;

        Pagina = pagina;
        await CarregarAsync();
    }

    private void AbrirNovo()
    {
        _editorNovo = true;
        _formId = 0;
        EditorTitulo = "Nova peça";
        FormCodigo = string.Empty;
        FormDescricao = string.Empty;
        FormCategoria = string.Empty;
        FormMarca = string.Empty;
        FormModelo = string.Empty;
        FormCodigoBarras = string.Empty;
        FormUnidadeMedida = "UN";
        FormPrecoCusto = 0;
        FormPrecoVenda = 0;
        FormEstoqueAtual = 0;
        FormEstoqueMinimo = 0;
        FormObservacoes = string.Empty;
        FormAtivo = true;
        EditorMensagem = string.Empty;
        EditorAberto = true;

        OnPropertyChanged(nameof(EditorNovo));
    }

    private void AbrirEdicao(PecaDto? peca)
    {
        if (peca == null)
            return;

        PreencherFormulario(peca);
        EditorTitulo = "Editar peça";
        _editorNovo = false;
        _formId = peca.Id;
        EditorAberto = true;

        OnPropertyChanged(nameof(EditorNovo));
    }

    private void AbrirVisualizacao(PecaDto? peca)
    {
        if (peca == null)
            return;

        PreencherFormulario(peca);
        VisualizacaoAberta = true;
    }

    private void PreencherFormulario(PecaDto peca)
    {
        _formId = peca.Id;
        FormCodigo = peca.Codigo;
        FormDescricao = peca.Descricao;
        FormCategoria = peca.Categoria ?? string.Empty;
        FormMarca = peca.Marca ?? string.Empty;
        FormModelo = peca.Modelo ?? string.Empty;
        FormCodigoBarras = peca.CodigoBarras ?? string.Empty;
        FormUnidadeMedida = peca.UnidadeMedida ?? "UN";
        FormPrecoCusto = peca.PrecoCusto;
        FormPrecoVenda = peca.PrecoVenda;
        FormEstoqueAtual = peca.EstoqueAtual;
        FormEstoqueMinimo = peca.EstoqueMinimo;
        FormObservacoes = peca.Observacoes ?? string.Empty;
        FormAtivo = peca.Ativo;
        EditorMensagem = string.Empty;
    }

    private void FecharEditor()
    {
        EditorAberto = false;
        EditorMensagem = string.Empty;
    }

    private void FecharVisualizacao()
    {
        VisualizacaoAberta = false;
    }

     private async Task SalvarAsync()
     {
         var validation = FormValidator.Create()
             .Required(FormCodigo, "o código da peça")
             .Required(FormDescricao, "a descrição da peça")
             .DecimalNaoNegativo(FormPrecoVenda, "preço de venda")
             .Build();

         if (!validation.IsValid)
         {
             EditorMensagem = validation.FirstError;
             return;
         }

        try
        {
            var dto = CriarDto();

            if (EditorNovo)
            {
                await _pecaService.CriarAsync(dto);
            }
            else
            {
                await _pecaService.AtualizarAsync(new AtualizarPecaDto
                {
                    Id = _formId,
                    Codigo = dto.Codigo,
                    Descricao = dto.Descricao,
                    Categoria = dto.Categoria,
                    Marca = dto.Marca,
                    Modelo = dto.Modelo,
                    CodigoBarras = dto.CodigoBarras,
                    UnidadeMedida = dto.UnidadeMedida,
                    PrecoCusto = dto.PrecoCusto,
                    PrecoVenda = dto.PrecoVenda,
                    EstoqueAtual = dto.EstoqueAtual,
                    EstoqueMinimo = dto.EstoqueMinimo,
                    Observacoes = dto.Observacoes,
                    Ativo = dto.Ativo
                });
            }

            var edicao = !EditorNovo;
            FecharEditor();
            await CarregarAsync();
            _reportStatus?.Invoke(edicao ? $"Peça \"{FormDescricao}\" atualizada." : $"Peça \"{FormDescricao}\" cadastrada.");
        }
        catch (Exception ex)
        {
            EditorMensagem = ex.Message;
        }
    }

    private CriarPecaDto CriarDto()
    {
        return new CriarPecaDto
        {
            Codigo = EditorNovo ? FormCodigo.Trim().ToUpperInvariant() : FormCodigo,
            Descricao = InputFormattingHelper.ToUpperCase(FormDescricao.Trim()),
            Categoria = InputFormattingHelper.NormalizeText(FormCategoria),
            Marca = InputFormattingHelper.NormalizeText(FormMarca),
            Modelo = InputFormattingHelper.NormalizeText(FormModelo),
            CodigoBarras = InputFormattingHelper.NormalizeText(FormCodigoBarras),
            UnidadeMedida = FormUnidadeMedida.Trim().ToUpperInvariant(),
            PrecoCusto = FormPrecoCusto,
            PrecoVenda = FormPrecoVenda,
            EstoqueAtual = FormEstoqueAtual,
            EstoqueMinimo = FormEstoqueMinimo,
            Observacoes = InputFormattingHelper.NormalizeText(FormObservacoes),
            Ativo = FormAtivo
        };
    }

    private async Task AlternarSituacaoAsync(PecaDto? peca)
    {
        if (peca == null)
            return;

        var nome = peca.Descricao;

        try
        {
            if (peca.Ativo)
            {
                await _pecaService.InativarAsync(peca.Id);
            }
            else
            {
                await _pecaService.AtualizarAsync(new AtualizarPecaDto
                {
                    Id = peca.Id,
                    Codigo = peca.Codigo,
                    Descricao = peca.Descricao,
                    Categoria = peca.Categoria,
                    Marca = peca.Marca,
                    Modelo = peca.Modelo,
                    CodigoBarras = peca.CodigoBarras,
                    UnidadeMedida = peca.UnidadeMedida,
                    PrecoCusto = peca.PrecoCusto,
                    PrecoVenda = peca.PrecoVenda,
                    EstoqueAtual = peca.EstoqueAtual,
                    EstoqueMinimo = peca.EstoqueMinimo,
                    Observacoes = peca.Observacoes,
                    Ativo = true
                });
            }

            await CarregarAsync();
            _reportStatus?.Invoke($"Peça \"{nome}\" {(peca.Ativo ? "inativada" : "reativada")}.");
        }
        catch (Exception ex)
        {
            _reportStatus?.Invoke($"Falha ao alterar a situação: {ex.Message}");
        }
    }

    private void AbrirConfirmacaoExclusao(PecaDto? peca)
    {
        if (peca == null)
            return;

        _pecaParaExcluir = peca;
        ConfirmacaoAberta = true;
    }

    private void FecharConfirmacao()
    {
        ConfirmacaoAberta = false;
        _pecaParaExcluir = null;
    }

    private async Task ExcluirAsync()
    {
        if (_pecaParaExcluir == null)
            return;

        var peca = _pecaParaExcluir;

        try
        {
            await _pecaService.ExcluirAsync(peca.Id);
            FecharConfirmacao();
            await CarregarAsync();
            _reportStatus?.Invoke($"Peça \"{peca.Descricao}\" excluída.");
        }
        catch (Exception ex)
        {
            FecharConfirmacao();
            _reportStatus?.Invoke(ex.Message);
        }
    }
}
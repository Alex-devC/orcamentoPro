using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Threading;
using OrcPro.Application.DTOs.Auth;
using OrcPro.Application.DTOs.Common;
using OrcPro.Application.DTOs.Servico;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Common;
using OrcPro.Domain.Common.Formatters;

namespace OrcPro.App.ViewModels;

/// <summary>
/// Tela de Serviços / Mão de Obra: DataGrid com ordenação no banco, pesquisa com debounce,
/// filtros e paginação, além das ações Novo, Editar, Visualizar, Ativar/Inativar e Excluir
/// (com confirmação). O código é gerado pelo sistema e nunca digitado; valor e tempo
/// estimado usam entrada natural de texto, sem máscara agressiva durante a digitação.
/// </summary>
public class ServicosViewModel : ViewModelBase
{
    private const int PageSize = 20;
    private const int DebounceBuscaMilissegundos = 350;

    // Nomes lógicos dos campos: chave da validação compartilhada (resumo no topo do
    // formulário, destaque do campo inválido e foco no primeiro erro).
    private const string CampoCodigo = "Codigo";
    private const string CampoDescricao = "Descricao";
    private const string CampoValor = "Valor";
    private const string CampoTempoEstimado = "TempoEstimado";

    private readonly IServicoService _servicoService;
    private readonly Action<string>? _reportStatus;
    private readonly DispatcherTimer _buscaTimer;

    private readonly bool _podeVisualizar;
    private readonly bool _podeCriar;
    private readonly bool _podeEditar;
    private readonly bool _podeExcluir;
    private readonly bool _podeAtivarInativar;

    private ServicoDto? _servicoSelecionada;
    private ServicoDto? _servicoParaExcluir;

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
    private string _formUnidade = "UN";
    private string _formObservacoes = string.Empty;
    private string _formValorTexto = string.Empty;
    private string _formTempoEstimadoTexto = string.Empty;
    private decimal _formValor;
    private decimal _formTempoEstimado;
    private bool _formAtivo = true;
    private string _editorMensagem = string.Empty;

    private bool _visualizacaoAberta;
    private bool _confirmacaoAberta;

    public ServicosViewModel(
        IServicoService servicoService,
        UsuarioSessaoDto sessao,
        Action<string>? reportStatus = null)
    {
        _servicoService = servicoService;
        _reportStatus = reportStatus;

        _podeVisualizar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Servicos.Visualizar);
        _podeCriar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Servicos.Criar);
        _podeEditar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Servicos.Editar);
        _podeExcluir = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Servicos.Excluir);
        _podeAtivarInativar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Servicos.AtivarInativar);

        _buscaTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceBuscaMilissegundos) };
        _buscaTimer.Tick += OnBuscaTimerTick;

        NovoCommand = new AsyncRelayCommand(_ => AbrirNovoAsync(), _ => _podeCriar);
        EditarCommand = new RelayCommand(p => AbrirEdicao(p as ServicoDto ?? ServicoSelecionada), _ => _podeEditar);
        VisualizarCommand = new RelayCommand(p => AbrirVisualizacao(p as ServicoDto ?? ServicoSelecionada), _ => _podeVisualizar);
        SalvarCommand = new AsyncRelayCommand(_ => SalvarAsync());
        CancelarEditorCommand = new RelayCommand(_ => FecharEditor());
        FecharVisualizacaoCommand = new RelayCommand(_ => FecharVisualizacao());
        AlternarSituacaoCommand = new AsyncRelayCommand(p => AlternarSituacaoAsync(p as ServicoDto), _ => _podeAtivarInativar);
        ExcluirCommand = new RelayCommand(p => AbrirConfirmacaoExclusao(p as ServicoDto ?? ServicoSelecionada), _ => _podeExcluir);
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

    public ObservableCollection<ServicoDto> Servicos { get; } = new();

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

    public ServicoDto? ServicoSelecionada
    {
        get => _servicoSelecionada;
        set => SetField(ref _servicoSelecionada, value);
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

    public bool TemRegistros => Servicos.Count > 0;

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

    /// <summary>Código gerado pelo sistema — exibido como somente leitura.</summary>
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

    public string FormUnidade
    {
        get => _formUnidade;
        set => SetField(ref _formUnidade, value);
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

    // ======================= Campos numéricos editáveis =======================
    //
    // Valor e tempo estimado são editáveis como TEXTO, e não como decimal com
    // StringFormat. Usar {Binding decimal, StringFormat=N2} em um TextBox TwoWay
    // reescreve o texto a cada tecla na cultura pt-BR: "10,50" virava "1.050,00" e
    // "1" podia virar "1.000". Aqui o usuário digita livremente (1, 10, 10,50, 1000,
    // 1250,75) e a conversão para decimal acontece só na validação, via
    // DecimalInputHelper. As propriedades decimais abaixo seguem existindo para a
    // ficha de visualização (somente leitura).
    // ========================================================================

    /// <summary>Valor digitado pelo usuário (texto livre).</summary>
    public string FormValorTexto
    {
        get => _formValorTexto;
        set => SetField(ref _formValorTexto, value);
    }

    /// <summary>Tempo estimado digitado pelo usuário, em horas (texto livre).</summary>
    public string FormTempoEstimadoTexto
    {
        get => _formTempoEstimadoTexto;
        set => SetField(ref _formTempoEstimadoTexto, value);
    }

    /// <summary>Valor já convertido para decimal.</summary>
    public decimal ValorDigitado => DecimalInputHelper.ConverterOuPadrao(FormValorTexto);

    /// <summary>Tempo estimado já convertido para decimal (horas).</summary>
    public decimal TempoEstimadoDigitado => DecimalInputHelper.ConverterOuPadrao(FormTempoEstimadoTexto);

    /// <summary>Valor em decimal, usado na ficha de visualização (somente leitura).</summary>
    public decimal FormValor
    {
        get => _formValor;
        private set => SetField(ref _formValor, value);
    }

    /// <summary>Tempo estimado em decimal, usado na ficha de visualização (somente leitura).</summary>
    public decimal FormTempoEstimado
    {
        get => _formTempoEstimado;
        private set => SetField(ref _formTempoEstimado, value);
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

    public string ConfirmacaoMensagem => _servicoParaExcluir is null
        ? string.Empty
        : $"Deseja excluir o serviço \"{_servicoParaExcluir.Descricao}\" ({_servicoParaExcluir.Codigo})? Esta ação não pode ser desfeita.";

    // ---------- Estado visual de erro (usado pelos estilos do formulário) ----------

    public bool CodigoTemErro => CampoInvalido(CampoCodigo);
    public bool DescricaoTemErro => CampoInvalido(CampoDescricao);
    public bool ValorTemErro => CampoInvalido(CampoValor);
    public bool TempoEstimadoTemErro => CampoInvalido(CampoTempoEstimado);

    protected override void AoAlterarValidacao(string campo)
    {
        base.AoAlterarValidacao(campo);

        switch (campo)
        {
            case CampoCodigo:
                OnPropertyChanged(nameof(CodigoTemErro));
                break;
            case CampoDescricao:
                OnPropertyChanged(nameof(DescricaoTemErro));
                break;
            case CampoValor:
                OnPropertyChanged(nameof(ValorTemErro));
                break;
            case CampoTempoEstimado:
                OnPropertyChanged(nameof(TempoEstimadoTemErro));
                break;
        }
    }

    public AsyncRelayCommand NovoCommand { get; }
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

    // ======================= Carga de dados =======================

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
            var result = await _servicoService.ListarPaginadoAsync(request);

            Servicos.Clear();
            foreach (var servico in result.Items)
            {
                Servicos.Add(servico);
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
            _reportStatus?.Invoke($"Falha ao carregar serviços: {ex.Message}");
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

    /// <summary>
    /// Abre o formulário de cadastro já com o próximo código disponível preenchido.
    /// O código é gerado pelo sistema (<see cref="IServicoService.GerarProximoCodigoAsync"/>)
    /// e o campo fica somente leitura — o usuário nunca digita o código.
    /// </summary>
    private async Task AbrirNovoAsync()
    {
        _editorNovo = true;
        _formId = 0;
        EditorTitulo = "Novo serviço";
        FormDescricao = string.Empty;
        FormCategoria = string.Empty;
        FormUnidade = "UN";
        FormValorTexto = string.Empty;
        FormTempoEstimadoTexto = string.Empty;
        SincronizarValoresNumericos();
        FormObservacoes = string.Empty;
        FormAtivo = true;
        EditorMensagem = string.Empty;
        LimparErrosValidacao();

        try
        {
            FormCodigo = await _servicoService.GerarProximoCodigoAsync();
        }
        catch (Exception ex)
        {
            // Sem código o cadastro não pode prosseguir: informa e não abre o editor.
            EditorMensagem = $"Não foi possível gerar o código do serviço: {ex.Message}";
            _reportStatus?.Invoke(EditorMensagem);
            return;
        }

        EditorAberto = true;
        OnPropertyChanged(nameof(EditorNovo));
    }

    private void AbrirEdicao(ServicoDto? servico)
    {
        if (servico == null)
            return;

        PreencherFormulario(servico);
        EditorTitulo = "Editar serviço";
        _editorNovo = false;
        _formId = servico.Id;
        EditorAberto = true;

        OnPropertyChanged(nameof(EditorNovo));
    }

    private void AbrirVisualizacao(ServicoDto? servico)
    {
        if (servico == null)
            return;

        PreencherFormulario(servico);
        VisualizacaoAberta = true;
    }

    private void PreencherFormulario(ServicoDto servico)
    {
        _formId = servico.Id;
        FormCodigo = servico.Codigo;
        FormDescricao = servico.Descricao;
        FormCategoria = servico.Categoria ?? string.Empty;
        FormUnidade = string.IsNullOrWhiteSpace(servico.Unidade) ? "UN" : servico.Unidade;
        FormValorTexto = ParaTextoDeEdicao(servico.Valor);
        FormTempoEstimadoTexto = ParaTextoDeEdicao(servico.TempoEstimado);
        SincronizarValoresNumericos();
        FormObservacoes = servico.Observacoes ?? string.Empty;
        FormAtivo = servico.Ativo;
        EditorMensagem = string.Empty;
        LimparErrosValidacao();
    }

    private void FecharEditor()
    {
        EditorAberto = false;
        LimparErrosValidacao();
        EditorMensagem = string.Empty;
    }

    private void FecharVisualizacao()
    {
        VisualizacaoAberta = false;
    }

    /// <summary>
    /// Copia os textos digitados para as propriedades decimais usadas na ficha de
    /// visualização, mantendo as duas visões (edição e leitura) coerentes.
    /// </summary>
    private void SincronizarValoresNumericos()
    {
        FormValor = ValorDigitado;
        FormTempoEstimado = TempoEstimadoDigitado;
    }

    /// <summary>Converte um decimal para o texto de edição sem separador de milhar.</summary>
    private static string ParaTextoDeEdicao(decimal valor)
        => DecimalInputHelper.FormatarQuantidade(valor).Replace(".", string.Empty);

    /// <summary>
    /// Um campo numérico vazio é tratado como zero (ainda não digitado); só reprova
    /// quando há texto que não representa número.
    /// </summary>
    private static bool EhValido(string? texto)
        => string.IsNullOrWhiteSpace(texto) || DecimalInputHelper.EhValido(texto);


    /// <summary>Valida o valor digitado: numérico e não negativo.</summary>
    private ValidationResult ValidarValor()
    {
        if (!EhValido(FormValorTexto))
            return ValidationResult.Failure("Informe um valor numérico válido.");

        if (ValorDigitado < 0)
            return ValidationResult.Failure("O valor do serviço não pode ser negativo.");

        return ValidationResult.Success();
    }

    /// <summary>Valida o tempo estimado digitado: numérico e não negativo.</summary>
    private ValidationResult ValidarTempoEstimado()
    {
        if (!EhValido(FormTempoEstimadoTexto))
            return ValidationResult.Failure("Informe um tempo estimado numérico válido.");

        if (TempoEstimadoDigitado < 0)
            return ValidationResult.Failure("O tempo estimado não pode ser negativo.");

        return ValidationResult.Success();
    }

    /// <summary>Sincroniza o resultado da validação de um campo com a infraestrutura compartilhada.</summary>
    private void AtualizarErroCampo(string campo, ValidationResult resultado)
    {
        if (resultado.IsValid)
            LimparErroValidacao(campo);
        else
            DefinirErroValidacao(campo, resultado.FirstError);
    }

    private async Task SalvarAsync()
    {
        // Converte o texto digitado uma única vez, aqui na validação — a digitação
        // em si nunca é reformatada.
        var valor = ValorDigitado;
        var tempo = TempoEstimadoDigitado;

        var resultadoValor = ValidarValor();
        var resultadoTempo = ValidarTempoEstimado();

        var validation = FormValidator.Create()
            .Required(FormCodigo, "o código do serviço")
            .Required(FormDescricao, "a descrição do serviço")
            .DecimalNaoNegativo(valor, "valor do serviço")
            .DecimalNaoNegativo(tempo, "tempo estimado")
            .Custom(!resultadoValor.IsValid, resultadoValor.FirstError)
            .Custom(!resultadoTempo.IsValid, resultadoTempo.FirstError)
            .Build();

        // Reflete os campos na infraestrutura de validação compartilhada
        // (resumo no topo do formulário + foco no primeiro campo inválido).
        AtualizarErroCampo(CampoCodigo, FormValidator.Required(FormCodigo, "o código do serviço"));
        AtualizarErroCampo(CampoDescricao, FormValidator.Required(FormDescricao, "a descrição do serviço"));
        AtualizarErroCampo(CampoValor, resultadoValor);
        AtualizarErroCampo(CampoTempoEstimado, resultadoTempo);

        if (!validation.IsValid)
        {
            EditorMensagem = validation.FirstError;
            SolicitarFocoPrimeiroCampoInvalido();
            return;
        }

        try
        {
            var dto = CriarDto();

            if (EditorNovo)
            {
                await _servicoService.CriarAsync(dto);
            }
            else
            {
                await _servicoService.AtualizarAsync(new AtualizarServicoDto
                {
                    Id = _formId,
                    Codigo = dto.Codigo,
                    Descricao = dto.Descricao,
                    Categoria = dto.Categoria,
                    Valor = dto.Valor,
                    Unidade = dto.Unidade,
                    TempoEstimado = dto.TempoEstimado,
                    Observacoes = dto.Observacoes,
                    Ativo = dto.Ativo
                });
            }

            var edicao = !EditorNovo;
            FecharEditor();
            await CarregarAsync();
            _reportStatus?.Invoke(edicao ? $"Serviço \"{FormDescricao}\" atualizado." : $"Serviço \"{FormDescricao}\" cadastrado.");
        }
        catch (Exception ex)
        {
            EditorMensagem = ex.Message;
        }
    }

    private CriarServicoDto CriarDto()
    {
        return new CriarServicoDto
        {
            Codigo = EditorNovo ? FormCodigo.Trim().ToUpperInvariant() : FormCodigo,
            Descricao = InputFormattingHelper.ToUpperCase(FormDescricao.Trim()),
            Categoria = InputFormattingHelper.NormalizeText(FormCategoria),
            Valor = ValorDigitado,
            Unidade = FormUnidade.Trim().ToUpperInvariant(),
            TempoEstimado = TempoEstimadoDigitado,
            Observacoes = InputFormattingHelper.NormalizeText(FormObservacoes),
            Ativo = FormAtivo
        };
    }

    // ======================= Situação e exclusão =======================

    private async Task AlternarSituacaoAsync(ServicoDto? servico)
    {
        if (servico == null)
            return;

        var nome = servico.Descricao;

        try
        {
            if (servico.Ativo)
                await _servicoService.InativarAsync(servico.Id);
            else
                await _servicoService.AtivarAsync(servico.Id);

            await CarregarAsync();
            _reportStatus?.Invoke($"Serviço \"{nome}\" {(servico.Ativo ? "inativado" : "reativado")}.");
        }
        catch (Exception ex)
        {
            _reportStatus?.Invoke($"Falha ao alterar a situação: {ex.Message}");
        }
    }

    private void AbrirConfirmacaoExclusao(ServicoDto? servico)
    {
        if (servico == null)
            return;

        _servicoParaExcluir = servico;
        ConfirmacaoAberta = true;
    }

    private void FecharConfirmacao()
    {
        ConfirmacaoAberta = false;
        _servicoParaExcluir = null;
    }

    private async Task ExcluirAsync()
    {
        if (_servicoParaExcluir == null)
            return;

        var servico = _servicoParaExcluir;

        try
        {
            await _servicoService.ExcluirAsync(servico.Id);
            FecharConfirmacao();
            await CarregarAsync();
            _reportStatus?.Invoke($"Serviço \"{servico.Descricao}\" excluído.");
        }
        catch (Exception ex)
        {
            FecharConfirmacao();
            _reportStatus?.Invoke(ex.Message);
        }
    }
}

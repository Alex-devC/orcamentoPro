using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using OrcPro.Application.DTOs.Cliente;
using OrcPro.Application.DTOs.Auth;
using OrcPro.Application.DTOs.Common;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Common;
using OrcPro.Domain.Common.Formatters;

// Alias to disambiguate between OrcPro.Domain.Common.CpfCnpjValidator (legacy) and OrcPro.Domain.Common.Formatters.CpfCnpjValidator (new)
using CpfCnpjValidatorEx = OrcPro.Domain.Common.Formatters.CpfCnpjValidator;

namespace OrcPro.App.ViewModels;

/// <summary>
/// Tela de Clientes: DataGrid com ordenação, pesquisa, filtros e paginação, além das
/// ações Novo, Editar, Visualizar, Ativar/Inativar e Excluir (com confirmação).
/// Regras: CPF/CNPJ validado e único quando informado; cliente com orçamento vinculado
/// só pode ser inativado (a exclusão é bloqueada pelo serviço).
/// </summary>
public class ClientesViewModel : ViewModelBase
{
    private const int PageSize = 20;
    private const int DebounceBuscaMilissegundos = 350;

    private readonly IClienteService _clienteService;
    private readonly ICepService? _cepService;
    private readonly Action<string>? _reportStatus;
    private readonly DispatcherTimer _buscaTimer;
    private bool _cepCarregando;
    private CancellationTokenSource? _cepCancellationTokenSource;
    private readonly CepQueryCache _cepCache = new();

    // Track which fields the user has manually edited (to avoid overwriting on CEP lookup)
    private bool _cepLogradouroEditado;
    private bool _cepBairroEditado;
    private bool _cepCidadeEditado;
    private bool _cepUfEditado;

    private readonly bool _podeVisualizar;
    private readonly bool _podeCriar;
    private readonly bool _podeEditar;
    private readonly bool _podeExcluir;
    private readonly bool _podeAtivarInativar;

    private ClienteDto? _clienteSelecionado;
    private ClienteDto? _clienteParaExcluir;

    private string _busca = string.Empty;
    private int _situacaoFiltro;
    private string _tipoPessoaFiltro = "todos";
    private int _pagina = 1;
    private int _totalPaginas = 1;
    private int _totalRegistros;
    private SortRequest? _ordenacao;

    private bool _editorAberto;
    private bool _editorNovo;
    private int _formId;
    private string _editorTitulo = string.Empty;
    private string _formCodigo = string.Empty;
    private string _formTipoPessoa = "PJ";
    private string _formNome = string.Empty;
    private string _formNomeFantasia = string.Empty;
    private string _formCpfCnpj = string.Empty;
    private string _formRgIe = string.Empty;
    private string _formTelefone = string.Empty;
    private string _formCelular = string.Empty;
    private string _formEmail = string.Empty;
    private string _formEmailFinanceiro = string.Empty;
    private string _formCep = string.Empty;
    private string _formLogradouro = string.Empty;
    private string _formNumero = string.Empty;
    private string _formComplemento = string.Empty;
    private string _formBairro = string.Empty;
    private string _formCidade = string.Empty;
    private string _formUf = string.Empty;
    private string _formObservacoes = string.Empty;
    private bool _formAtivo = true;
    private int _formQuantidadeOrcamentos;
    private string _editorMensagem = string.Empty;

    private bool _visualizacaoAberta;
    private bool _confirmacaoAberta;

    public ClientesViewModel(
        IClienteService clienteService,
        UsuarioSessaoDto sessao,
        ICepService? cepService = null,
        Action<string>? reportStatus = null)
    {
        _clienteService = clienteService;
        _cepService = cepService;
        _reportStatus = reportStatus;

        // Permissões do módulo (CLIENTES.*) — o perfil Administrador recebe todas
        // automaticamente pelo sincronizador do catálogo.
        _podeVisualizar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Clientes.Visualizar);
        _podeCriar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Clientes.Criar);
        _podeEditar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Clientes.Editar);
        _podeExcluir = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Clientes.Excluir);
        _podeAtivarInativar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Clientes.AtivarInativar);

        _buscaTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceBuscaMilissegundos) };
        _buscaTimer.Tick += async (_, _) =>
        {
            _buscaTimer.Stop();
            await CarregarAsync();
        };

        NovoCommand = new RelayCommand(_ => AbrirNovo(), _ => _podeCriar);
        EditarCommand = new RelayCommand(p => AbrirEdicao(p as ClienteDto ?? ClienteSelecionado), _ => _podeEditar);
        VisualizarCommand = new RelayCommand(p => AbrirVisualizacao(p as ClienteDto ?? ClienteSelecionado), _ => _podeVisualizar);
        SalvarCommand = new AsyncRelayCommand(_ => SalvarAsync());
        CancelarEditorCommand = new RelayCommand(_ => FecharEditor());
        FecharVisualizacaoCommand = new RelayCommand(_ => FecharVisualizacao());
        AlternarSituacaoCommand = new AsyncRelayCommand(p => AlternarSituacaoAsync(p as ClienteDto), _ => _podeAtivarInativar);
        ExcluirCommand = new RelayCommand(p => AbrirConfirmacaoExclusao(p as ClienteDto ?? ClienteSelecionado), _ => _podeExcluir);
        ConfirmarExclusaoCommand = new AsyncRelayCommand(_ => ExcluirAsync());
        CancelarExclusaoCommand = new RelayCommand(_ => FecharConfirmacao());
        AtualizarListaCommand = new AsyncRelayCommand(_ => CarregarAsync());
        LimparBuscaCommand = new RelayCommand(_ => Busca = string.Empty);
        PaginaAnteriorCommand = new AsyncRelayCommand(_ => IrParaPagina(Pagina - 1), _ => PodePaginaAnterior);
        PaginaProximaCommand = new AsyncRelayCommand(_ => IrParaPagina(Pagina + 1), _ => PodePaginaProxima);
    }

    // ---------- Permissões do módulo ----------

    public bool PodeVisualizar => _podeVisualizar;
    public bool PodeCriar => _podeCriar;
    public bool PodeEditar => _podeEditar;
    public bool PodeExcluir => _podeExcluir;
    public bool PodeAtivarInativar => _podeAtivarInativar;

    public ObservableCollection<ClienteDto> Clientes { get; } = new();

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

    /// <summary>"todos", "PF" ou "PJ".</summary>
    public string TipoPessoaFiltro
    {
        get => _tipoPessoaFiltro;
        set
        {
            if (!SetField(ref _tipoPessoaFiltro, value)) return;
            Pagina = 1;
            _ = CarregarAsync();
        }
    }

    public ClienteDto? ClienteSelecionado
    {
        get => _clienteSelecionado;
        set => SetField(ref _clienteSelecionado, value);
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

    public bool TemRegistros => Clientes.Count > 0;

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

    public string FormTipoPessoa
    {
        get => _formTipoPessoa;
        set => SetField(ref _formTipoPessoa, value);
    }

    public string FormNome
    {
        get => _formNome;
        set => SetField(ref _formNome, value);
    }

    public string FormNomeFantasia
    {
        get => _formNomeFantasia;
        set => SetField(ref _formNomeFantasia, value);
    }

    public string FormCpfCnpj
    {
        get => _formCpfCnpj;
        set => SetField(ref _formCpfCnpj, value);
    }

    public string FormRgIe
    {
        get => _formRgIe;
        set => SetField(ref _formRgIe, value);
    }

    public string FormTelefone
    {
        get => _formTelefone;
        set => SetField(ref _formTelefone, value);
    }

    public string FormCelular
    {
        get => _formCelular;
        set => SetField(ref _formCelular, value);
    }

    public string FormEmail
    {
        get => _formEmail;
        set => SetField(ref _formEmail, value);
    }
public string FormEmailFinanceiro
    {
        get => _formEmailFinanceiro;
        set => SetField(ref _formEmailFinanceiro, value);
    }

    public string FormCep
    {
        get => _formCep;
        set
        {
            if (!SetField(ref _formCep, value)) return;
            _ = TentarConsultarCepAsync(value);
        }
    }

    public string FormLogradouro
    {
        get => _formLogradouro;
        set
        {
            if (SetField(ref _formLogradouro, value))
                _cepLogradouroEditado = true;
        }
    }

    public string FormNumero
    {
        get => _formNumero;
        set => SetField(ref _formNumero, value);
    }

    public string FormComplemento
    {
        get => _formComplemento;
        set => SetField(ref _formComplemento, value);
    }

    public string FormBairro
    {
        get => _formBairro;
        set
        {
            if (SetField(ref _formBairro, value))
                _cepBairroEditado = true;
        }
    }

    public string FormCidade
    {
        get => _formCidade;
        set
        {
            if (SetField(ref _formCidade, value))
                _cepCidadeEditado = true;
        }
    }

    public string FormUf
    {
        get => _formUf;
        set
        {
            if (SetField(ref _formUf, value))
                _cepUfEditado = true;
        }
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

    public bool CepCarregando
    {
        get => _cepCarregando;
        private set => SetField(ref _cepCarregando, value);
    }

    /// <summary>Orçamentos vinculados (define exclusão ou apenas inativação).</summary>
    public string ResumoOrcamentos => _formQuantidadeOrcamentos == 0
        ? "Sem orçamentos vinculados — pode ser excluído."
        : $"{_formQuantidadeOrcamentos} orçamento(s) vinculado(s) — exclusão bloqueada; use a inativação.";

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

    // ---------- Visualização e exclusão ----------

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

    public string ConfirmacaoMensagem => _clienteParaExcluir is null
        ? string.Empty
        : $"Deseja excluir o cliente \"{_clienteParaExcluir.NomeRazaoSocial}\" ({_clienteParaExcluir.Codigo})? Esta ação não pode ser desfeita.";

    // ---------- CEP ----------

    /// <summary>
    /// Quando o CEP é completado (8 dígitos), consulta automaticamente o endereço.
    /// Não bloqueia a interface; preserva campos que o usuário editou manualmente.
    /// Não repete a consulta ao mesmo CEP (cache por instância de ViewModel).
    /// </summary>
    private async Task TentarConsultarCepAsync(string cepInput)
    {
        if (_cepService is null)
            return;

        _cepCancellationTokenSource?.Cancel();
        _cepCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var cep = CepMaskHelper.Normalizar(cepInput);

        // Só consultar quando tiver 8 dígitos completos
        if (!CepMaskHelper.EstaCompletoParaConsulta(cep))
            return;

        // Cache: não repetir consulta ao mesmo CEP na mesma sessão de edição
        if (!_cepCache.ShouldQuery(cep))
            return;

        CepCarregando = true;
        EditorMensagem = string.Empty;

        try
        {
            var result = await _cepService.ConsultarAsync(cep, _cepCancellationTokenSource.Token);

            if (result.Success)
            {
                if (!_cepLogradouroEditado) FormLogradouro = result.Logradouro ?? string.Empty;
                if (!_cepBairroEditado) FormBairro = result.Bairro ?? string.Empty;
                if (!_cepCidadeEditado) FormCidade = result.Cidade ?? string.Empty;
                if (!_cepUfEditado) FormUf = result.Uf ?? string.Empty;
            }
            else
            {
                // CEP não encontrado ou serviço indisponível: permite preenchimento manual
                EditorMensagem = $"Não foi possível localizar o CEP '{CepMaskHelper.Formatar(cep)}'. Preencha o endereço manualmente.";
            }
        }
        catch (OperationCanceledException)
        {
            // Consulta cancelada - não é um erro
        }
        finally
        {
            CepCarregando = false;
        }
    }

    // ---------- Comandos ----------

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
// ---------- Carga de dados ----------

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

        if (!string.IsNullOrWhiteSpace(TipoPessoaFiltro) && !TipoPessoaFiltro.Equals("todos", StringComparison.OrdinalIgnoreCase))
        {
            request.Filters.Add(new FilterRequest { PropertyName = "TipoPessoa", Value = TipoPessoaFiltro });
        }

        if (_ordenacao != null)
            request.Sorts.Add(_ordenacao);

        try
        {
            var result = await _clienteService.ListarPaginadoAsync(request);

            Clientes.Clear();
            foreach (var cliente in result.Items)
            {
                Clientes.Add(cliente);
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
            _reportStatus?.Invoke($"Falha ao carregar clientes: {ex.Message}");
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
        _cepLogradouroEditado = false;
        _cepBairroEditado = false;
        _cepCidadeEditado = false;
        _cepUfEditado = false;
        _cepCache.Reset();
        EditorTitulo = "Novo cliente";
        FormCodigo = string.Empty;
        FormTipoPessoa = "PJ";
        FormNome = string.Empty;
        FormNomeFantasia = string.Empty;
        FormCpfCnpj = string.Empty;
        FormRgIe = string.Empty;
        FormTelefone = string.Empty;
        FormCelular = string.Empty;
        FormEmail = string.Empty;
        FormEmailFinanceiro = string.Empty;
        FormCep = string.Empty;
        FormLogradouro = string.Empty;
        FormNumero = string.Empty;
        FormComplemento = string.Empty;
        FormBairro = string.Empty;
        FormCidade = string.Empty;
        FormUf = string.Empty;
        FormObservacoes = string.Empty;
        FormAtivo = true;
        _formQuantidadeOrcamentos = 0;
        EditorMensagem = string.Empty;
        EditorAberto = true;

        OnPropertyChanged(nameof(EditorNovo));
        OnPropertyChanged(nameof(ResumoOrcamentos));
    }

    private void AbrirEdicao(ClienteDto? cliente)
    {
        if (cliente == null)
            return;

        PreencherFormulario(cliente);
        EditorTitulo = "Editar cliente";
        _editorNovo = false;
        _formId = cliente.Id;
        EditorAberto = true;

        OnPropertyChanged(nameof(EditorNovo));
    }

    private void AbrirVisualizacao(ClienteDto? cliente)
    {
        if (cliente == null)
            return;

        PreencherFormulario(cliente);
        VisualizacaoAberta = true;
    }

    private void PreencherFormulario(ClienteDto cliente)
    {
        _formId = cliente.Id;
        _cepLogradouroEditado = false;
        _cepBairroEditado = false;
        _cepCidadeEditado = false;
        _cepUfEditado = false;
        _cepCache.Reset();
        FormCodigo = cliente.Codigo;
        FormTipoPessoa = cliente.TipoPessoa;
        FormNome = cliente.NomeRazaoSocial;
        FormNomeFantasia = cliente.NomeFantasia ?? string.Empty;
        FormCpfCnpj = CpfCnpjValidatorEx.Formatar(cliente.CpfCnpj);
        FormRgIe = cliente.RgIe ?? string.Empty;
        FormTelefone = PhoneMaskHelper.Formatar(cliente.Telefone);
        FormCelular = PhoneMaskHelper.Formatar(cliente.Celular);
        FormEmail = cliente.Email;
        FormEmailFinanceiro = cliente.EmailFinanceiro ?? string.Empty;
        FormCep = CepMaskHelper.Formatar(cliente.Cep);
        FormLogradouro = cliente.Logradouro ?? string.Empty;
        FormNumero = cliente.Numero ?? string.Empty;
        FormComplemento = cliente.Complemento ?? string.Empty;
        FormBairro = cliente.Bairro ?? string.Empty;
        FormCidade = cliente.Cidade ?? string.Empty;
        FormUf = cliente.Uf ?? string.Empty;
        FormObservacoes = cliente.Observacoes ?? string.Empty;
        FormAtivo = cliente.Ativo;
        _formQuantidadeOrcamentos = cliente.QuantidadeOrcamentos;
        EditorMensagem = string.Empty;

        OnPropertyChanged(nameof(ResumoOrcamentos));
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
        if (string.IsNullOrWhiteSpace(FormNome))
        {
            EditorMensagem = "Informe o Nome / Razão Social do cliente.";
            return;
        }

        if (string.IsNullOrWhiteSpace(FormCelular))
        {
            EditorMensagem = "Informe o Celular / WhatsApp do cliente.";
            return;
        }

        if (string.IsNullOrWhiteSpace(FormEmail))
        {
            EditorMensagem = "Informe o e-mail principal do cliente.";
            return;
        }

        // Validação de CPF/CNPJ já no formulário (mensagem imediata) e novamente no serviço.
        // Usa o novo validator que suporta CPF, CNPJ numérico e CNPJ alfanumérico.
        if (!string.IsNullOrWhiteSpace(FormCpfCnpj) && !CpfCnpjValidatorEx.EhValido(FormCpfCnpj))
        {
            EditorMensagem = "O CPF/CNPJ informado é inválido. Confira os dígitos.";
            return;
        }

        // Aplicar formatação: texto para maiúsculo, e-mail para minúsculo, CPF/CNPJ normalizado,
        // telefone e CEP normalizados (apenas dígitos)
        var nome = InputFormattingHelper.ToUpperCase(FormNome.Trim());
        var cpfCnpjNormalizado = CpfCnpjValidatorEx.Normalizar(FormCpfCnpj);

        var dto = new CriarClienteDto
        {
            Codigo = EditorNovo ? null : FormCodigo,
            TipoPessoa = FormTipoPessoa,
            NomeRazaoSocial = nome,
            NomeFantasia = InputFormattingHelper.NormalizeText(FormNomeFantasia),
            CpfCnpj = cpfCnpjNormalizado,
            RgIe = InputFormattingHelper.NormalizeText(FormRgIe),
            Telefone = PhoneMaskHelper.Normalizar(FormTelefone),
            Celular = PhoneMaskHelper.Normalizar(FormCelular),
            Email = InputFormattingHelper.NormalizeEmail(FormEmail) ?? string.Empty,
            EmailFinanceiro = InputFormattingHelper.NormalizeEmail(FormEmailFinanceiro),
            Cep = CepMaskHelper.Normalizar(FormCep),
            Logradouro = InputFormattingHelper.NormalizeText(FormLogradouro),
            Numero = InputFormattingHelper.NormalizeText(FormNumero),
            Complemento = InputFormattingHelper.NormalizeText(FormComplemento),
            Bairro = InputFormattingHelper.NormalizeText(FormBairro),
            Cidade = InputFormattingHelper.NormalizeText(FormCidade),
            Uf = FormUf.Trim().ToUpperInvariant(),
            Observacoes = InputFormattingHelper.NormalizeText(FormObservacoes),
            Ativo = FormAtivo
        };

        try
        {
            if (EditorNovo)
            {
                await _clienteService.CriarAsync(dto);
            }
            else
            {
                await _clienteService.AtualizarAsync(new AtualizarClienteDto
                {
                    Id = _formId,
                    Codigo = dto.Codigo,
                    TipoPessoa = dto.TipoPessoa,
                    NomeRazaoSocial = dto.NomeRazaoSocial,
                    NomeFantasia = dto.NomeFantasia,
                    CpfCnpj = dto.CpfCnpj,
                    RgIe = dto.RgIe,
                    Telefone = dto.Telefone,
                    Celular = dto.Celular,
                    Email = dto.Email,
                    EmailFinanceiro = dto.EmailFinanceiro,
                    Cep = dto.Cep,
                    Logradouro = dto.Logradouro,
                    Numero = dto.Numero,
                    Complemento = dto.Complemento,
                    Bairro = dto.Bairro,
                    Cidade = dto.Cidade,
                    Uf = dto.Uf,
                    Observacoes = dto.Observacoes,
                    Ativo = dto.Ativo
                });
            }

            var edicao = !EditorNovo;
            FecharEditor();
            await CarregarAsync();
            _reportStatus?.Invoke(edicao ? $"Cliente \"{nome}\" atualizado." : $"Cliente \"{nome}\" cadastrado.");
        }
        catch (Exception ex)
        {
            EditorMensagem = ex.Message;
        }
    }
private async Task AlternarSituacaoAsync(ClienteDto? cliente)
    {
        if (cliente == null)
            return;

        var nome = cliente.NomeRazaoSocial;

        try
        {
            // Clientes vinculados a orçamentos podem apenas ser inativados (exclusão bloqueada).
            if (cliente.Ativo)
            {
                await _clienteService.InativarAsync(cliente.Id);
            }
            else
            {
                await _clienteService.AtualizarAsync(new AtualizarClienteDto
                {
                    Id = cliente.Id,
                    Codigo = cliente.Codigo,
                    TipoPessoa = cliente.TipoPessoa,
                    NomeRazaoSocial = cliente.NomeRazaoSocial,
                    NomeFantasia = cliente.NomeFantasia,
                    CpfCnpj = cliente.CpfCnpj,
                    RgIe = cliente.RgIe,
                    Telefone = cliente.Telefone,
                    Celular = cliente.Celular,
                    Email = cliente.Email,
                    EmailFinanceiro = cliente.EmailFinanceiro,
                    Cep = cliente.Cep,
                    Logradouro = cliente.Logradouro,
                    Numero = cliente.Numero,
                    Complemento = cliente.Complemento,
                    Bairro = cliente.Bairro,
                    Cidade = cliente.Cidade,
                    Uf = cliente.Uf,
                    Observacoes = cliente.Observacoes,
                    Ativo = true
                });
            }

            await CarregarAsync();
            _reportStatus?.Invoke($"Cliente \"{nome}\" {(cliente.Ativo ? "inativado" : "reativado")}.");
        }
        catch (Exception ex)
        {
            _reportStatus?.Invoke($"Falha ao alterar a situação: {ex.Message}");
        }
    }

    // ---------- Exclusão ----------

    private void AbrirConfirmacaoExclusao(ClienteDto? cliente)
    {
        if (cliente == null)
            return;

        if (cliente.QuantidadeOrcamentos > 0)
        {
            _reportStatus?.Invoke(
                $"O cliente \"{cliente.NomeRazaoSocial}\" possui {cliente.QuantidadeOrcamentos} orçamento(s) vinculado(s) e não pode ser excluído. Inative-o em vez de excluir.");
            return;
        }

        _clienteParaExcluir = cliente;
        ConfirmacaoAberta = true;
    }

    private void FecharConfirmacao()
    {
        ConfirmacaoAberta = false;
        _clienteParaExcluir = null;
    }

    private async Task ExcluirAsync()
    {
        if (_clienteParaExcluir == null)
            return;

        var cliente = _clienteParaExcluir;

        try
        {
            await _clienteService.ExcluirAsync(cliente.Id);
            FecharConfirmacao();
            await CarregarAsync();
            _reportStatus?.Invoke($"Cliente \"{cliente.NomeRazaoSocial}\" excluído.");
        }
        catch (Exception ex)
        {
            FecharConfirmacao();
            _reportStatus?.Invoke(ex.Message);
        }
    }
}
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using OrcPro.Application.DTOs.Auth;
using OrcPro.Application.DTOs.Common;
using OrcPro.Application.DTOs.Tecnico;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Application.Services;
using OrcPro.Domain.Common;
using OrcPro.Domain.Common.Formatters;

// Alias to disambiguate between OrcPro.Domain.Common.CpfCnpjValidator (legacy) and OrcPro.Domain.Common.Formatters.CpfCnpjValidator (new)
using CpfCnpjValidatorEx = OrcPro.Domain.Common.Formatters.CpfCnpjValidator;

namespace OrcPro.App.ViewModels;

/// <summary>
/// Tela de Técnicos: DataGrid com ordenação no banco, pesquisa com debounce, filtros e
/// paginação, além das ações Novo, Editar, Visualizar, Ativar/Inativar e Excluir (com
/// confirmação). O cadastro é independente do cadastro de Usuários — não há vínculo automático.
/// Regras: CPF validado e único quando informado (gravado sem máscara); técnico vinculado a
/// orçamentos só pode ser inativado, pois a exclusão é bloqueada pelo serviço.
/// </summary>
public class TecnicosViewModel : ViewModelBase
{
    private const int PageSize = 20;
    private const int DebounceBuscaMilissegundos = 350;
    private const string CepCampo = "CEP";

    private readonly ITecnicoService _tecnicoService;
    private readonly ICepService? _cepService;
    private readonly Action<string>? _reportStatus;
    private readonly DispatcherTimer _buscaTimer;

    private readonly bool _podeVisualizar;
    private readonly bool _podeCriar;
    private readonly bool _podeEditar;
    private readonly bool _podeExcluir;
    private readonly bool _podeAtivarInativar;

    private TecnicoDto? _tecnicoSelecionado;
    private TecnicoDto? _tecnicoParaExcluir;

    private string _busca = string.Empty;
    private int _situacaoFiltro;
    private string _especialidadeFiltro = string.Empty;
    private int _pagina = 1;
    private int _totalPaginas = 1;
    private int _totalRegistros;
    private SortRequest? _ordenacao;

    private bool _editorAberto;
    private bool _editorNovo;
    private int _formId;
    private string _editorTitulo = string.Empty;
    private string _formCodigo = string.Empty;
    private string _formNome = string.Empty;
    private string _formCpf = string.Empty;
    private string _formRg = string.Empty;
    private string _formTelefone = string.Empty;
    private string _formCelular = string.Empty;
    private string _formEmail = string.Empty;
    private string _formEspecialidade = string.Empty;
    private string _formRegistroProfissional = string.Empty;
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
    private bool _cepCarregando;

    // Lupa é a ÚNICA porta de entrada de consulta (sem auto-consulta via setter,
    // LostFocus, TextChanged ou 8 dígitos — ver ConsultarCepManualmenteAsync).

    public TecnicosViewModel(
        ITecnicoService tecnicoService,
        UsuarioSessaoDto sessao,
        ICepService? cepService = null,
        Action<string>? reportStatus = null)
    {
        _tecnicoService = tecnicoService;
        _cepService = cepService;
        _reportStatus = reportStatus;

        // Permissões do módulo (TECNICOS.*) — o perfil Administrador recebe todas
        // automaticamente pelo sincronizador do catálogo.
        _podeVisualizar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Tecnicos.Visualizar);
        _podeCriar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Tecnicos.Criar);
        _podeEditar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Tecnicos.Editar);
        _podeExcluir = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Tecnicos.Excluir);
        _podeAtivarInativar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Tecnicos.AtivarInativar);

        _buscaTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceBuscaMilissegundos) };
        _buscaTimer.Tick += OnBuscaTimerTick;

        NovoCommand = new RelayCommand(_ => AbrirNovo(), _ => _podeCriar);
        EditarCommand = new RelayCommand(p => AbrirEdicao(p as TecnicoDto ?? TecnicoSelecionado), _ => _podeEditar);
        VisualizarCommand = new RelayCommand(p => AbrirVisualizacao(p as TecnicoDto ?? TecnicoSelecionado), _ => _podeVisualizar);
        SalvarCommand = new AsyncRelayCommand(_ => SalvarAsync());
        CancelarEditorCommand = new RelayCommand(_ => FecharEditor());
        FecharVisualizacaoCommand = new RelayCommand(_ => FecharVisualizacao());
        AlternarSituacaoCommand = new AsyncRelayCommand(p => AlternarSituacaoAsync(p as TecnicoDto), _ => _podeAtivarInativar);
        ExcluirCommand = new RelayCommand(p => AbrirConfirmacaoExclusao(p as TecnicoDto ?? TecnicoSelecionado), _ => _podeExcluir);
        ConfirmarExclusaoCommand = new AsyncRelayCommand(_ => ExcluirAsync());
        CancelarExclusaoCommand = new RelayCommand(_ => FecharConfirmacao());
        AtualizarListaCommand = new AsyncRelayCommand(_ => CarregarAsync());
        LimparBuscaCommand = new RelayCommand(_ => Busca = string.Empty);
        PaginaAnteriorCommand = new AsyncRelayCommand(_ => IrParaPagina(Pagina - 1), _ => PodePaginaAnterior);
        PaginaProximaCommand = new AsyncRelayCommand(_ => IrParaPagina(Pagina + 1), _ => PodePaginaProxima);
        ConsultarCepCommand = new AsyncRelayCommand(_ => ConsultarCepManualmenteAsync());
    }

    // ---------- Permissões do módulo ----------

    public bool PodeVisualizar => _podeVisualizar;
    public bool PodeCriar => _podeCriar;
    public bool PodeEditar => _podeEditar;
    public bool PodeExcluir => _podeExcluir;
    public bool PodeAtivarInativar => _podeAtivarInativar;

    public ObservableCollection<TecnicoDto> Tecnicos { get; } = new();

    // ---------- Listagem, pesquisa e filtros ----------

    public string Busca
    {
        get => _busca;
        set
        {
            if (!SetField(ref _busca, value)) return;
            AgendarRecarga();
        }
    }

    /// <summary>Filtro por especialidade (correspondência parcial, aplicada no banco).</summary>
    public string EspecialidadeFiltro
    {
        get => _especialidadeFiltro;
        set
        {
            if (!SetField(ref _especialidadeFiltro, value)) return;
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

    public TecnicoDto? TecnicoSelecionado
    {
        get => _tecnicoSelecionado;
        set => SetField(ref _tecnicoSelecionado, value);
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

    public bool TemRegistros => Tecnicos.Count > 0;

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

    public string FormNome
    {
        get => _formNome;
        set => SetField(ref _formNome, value);
    }

    public string FormCpf
    {
        get => _formCpf;
        set => SetField(ref _formCpf, value);
    }

    public string FormRg
    {
        get => _formRg;
        set => SetField(ref _formRg, value);
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

    public string FormEspecialidade
    {
        get => _formEspecialidade;
        set => SetField(ref _formEspecialidade, value);
    }

    public string FormRegistroProfissional
    {
        get => _formRegistroProfissional;
        set => SetField(ref _formRegistroProfissional, value);
    }

    public string FormCep
    {
        get => _formCep;
        set
        {
            if (!SetField(ref _formCep, value)) return;
            OnPropertyChanged(nameof(CepPodeConsultarManualmente));

            // LUPA É A ÚNICA AÇÃO DE CONSULTA: digitar, apagar, sair do campo (LostFocus)
            // ou pressionar ENTER NUNCA consulta. Apenas limpa o erro anterior para
            // permitir correção e nova tentativa via lupa.
            LimparErroValidacao(CepCampo);
        }
    }

    public string FormLogradouro
    {
        get => _formLogradouro;
        set => SetField(ref _formLogradouro, value);
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
        set => SetField(ref _formBairro, value);
    }

    public string FormCidade
    {
        get => _formCidade;
        set => SetField(ref _formCidade, value);
    }

    public string FormUf
    {
        get => _formUf;
        set => SetField(ref _formUf, value);
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
        private set
        {
            if (SetField(ref _cepCarregando, value))
                OnPropertyChanged(nameof(CepPodeConsultarManualmente));
        }
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

    /// <summary>Indica se o botão de lupa de CEP pode ser clicado (serviço disponível, não carregando, CEP com 8 dígitos).</summary>
    public bool CepPodeConsultarManualmente => _cepService is not null && !CepCarregando && CepTemOitoDigitos();

    /// <summary>Indica que o CEP está em estado de erro (não encontrado/serviço indisponível) — borda vermelha.</summary>
    public bool CepTemErro => CampoInvalido(CepCampo);

    /// <summary>Mensagem de erro associada ao campo CEP (tooltip/resumo).</summary>
    public string CepMensagemErro => ErrosValidacao.TryGetValue(CepCampo, out var msg) ? msg : string.Empty;

    /// <summary>Notifica as propriedades visuais do CEP quando o estado de validação muda.</summary>
    protected override void AoAlterarValidacao(string campo)
    {
        if (campo == CepCampo)
        {
            OnPropertyChanged(nameof(CepTemErro));
            OnPropertyChanged(nameof(CepMensagemErro));
        }
    }

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

    public string ConfirmacaoMensagem => _tecnicoParaExcluir is null
        ? string.Empty
        : $"Deseja excluir o técnico \"{_tecnicoParaExcluir.Nome}\" ({_tecnicoParaExcluir.Codigo})? Esta ação não pode ser desfeita.";

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
    public AsyncRelayCommand ConsultarCepCommand { get; }

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

        if (!string.IsNullOrWhiteSpace(EspecialidadeFiltro))
        {
            request.Filters.Add(new FilterRequest { PropertyName = "Especialidade", Value = EspecialidadeFiltro });
        }

        if (_ordenacao != null)
            request.Sorts.Add(_ordenacao);

        try
        {
            var result = await _tecnicoService.ListarPaginadoAsync(request);

            Tecnicos.Clear();
            foreach (var tecnico in result.Items)
            {
                Tecnicos.Add(tecnico);
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
            _reportStatus?.Invoke($"Falha ao carregar técnicos: {ex.Message}");
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

    // ---------- Formulário ----------

    /// <summary>
    /// CONSULTA MANUAL via botão de lupa — ÚNICA porta de entrada de consulta de CEP.
    /// Digitar, apagar, sair do campo ou pressionar ENTER NUNCA consulta.
    /// Cada clique executa uma consulta real (sem cache) e aplica o endereço via
    /// propriedades (SetField → PropertyChanged), garantindo atualização imediata da UI.
    /// Log completo em logcep.txt (origem: MANUAL).
    /// </summary>
    private async Task ConsultarCepManualmenteAsync()
    {
        CepDiagnosticLogger.Linha("[CEP] BOTÃO LUPA CLICADO");
        CepDiagnosticLogger.Linha($"[CEP] Valor atual do campo: '{FormCep}'");
        CepDiagnosticLogger.Linha($"[CEP] CepPodeConsultarManualmente: {(CepPodeConsultarManualmente ? "true" : "false")}");

        if (_cepService is null)
        {
            CepDiagnosticLogger.Linha("[CEP] Serviço disponível: false — comando abortado (ICepService não injetado).");
            return;
        }

        if (string.IsNullOrWhiteSpace(FormCep))
        {
            CepDiagnosticLogger.Linha("[CEP] Campo CEP vazio — comando abortado.");
            return;
        }

        CepDiagnosticLogger.Linha("[CEP] ConsultarCepCommand iniciado");
        CepDiagnosticLogger.Linha("[CEP] Serviço disponível: true");
        CepDiagnosticLogger.Linha("[CEP] Origem da consulta: MANUAL");
        CepDiagnosticLogger.Linha("[CEP] Consulta manual — IGNORANDO CACHE.");

        var cep = CepMaskHelper.Normalizar(FormCep);
        CepDiagnosticLogger.Linha($"[CEP] CEP normalizado: {cep}");

        if (!CepMaskHelper.EstaCompletoParaConsulta(cep))
        {
            CepDiagnosticLogger.Linha($"[CEP] Falha: CEP com {cep.Length} dígito(s) — esperado 8.");
            // CEP incompleto: registra o formato inválido na validação (campo vermelho + resumo).
            EditorMensagem = "Digite um CEP com 8 dígitos para consultar.";
            DefinirErroValidacao(CepCampo, "O CEP deve ter 8 dígitos.");
            return;
        }

        CepCarregando = true;
        EditorMensagem = string.Empty;

        // A lupa é explícita: descarta o endereço anterior via PROPRIEDADES
        // (SetField → PropertyChanged) para a UI limpar/atualizar imediatamente,
        // e sempre executa consulta real (sem cache).
        CepDiagnosticLogger.Linha("[CEP] Limpando endereço anterior via propriedades (UI atualiza imediatamente).");
        FormLogradouro = string.Empty;
        FormBairro = string.Empty;
        FormCidade = string.Empty;
        FormUf = string.Empty;

        try
        {
            CepDiagnosticLogger.Linha("[CEP] Chamando ICepService.ConsultarAsync");
            var result = await _cepService.ConsultarAsync(cep);
            CepDiagnosticLogger.Linha(
                $"[CEP] ICepService retornou: Success={result.Success.ToString().ToLowerInvariant()}, Cep={result.Cep}");
            AplicarEndereco(result, "MANUAL");
        }
        catch (Exception ex)
        {
            CepDiagnosticLogger.LogarException("[CEP] Exceção na consulta manual", ex);
            EditorMensagem = $"Erro na consulta de CEP: {ex.Message}";
            DefinirErroValidacao(CepCampo, "Não foi possível consultar o CEP. Tente novamente.");
        }
        finally
        {
            CepCarregando = false;
        }
    }

    /// <summary>
    /// Aplica o resultado da consulta de CEP nos campos do formulário (sucesso ou falha)
    /// SEMPRE via propriedades (FormLogradouro/FormBairro/FormCidade/FormUf → SetField →
    /// PropertyChanged) para que o binding TwoWay atualize a UI imediatamente. Registra no
    /// logcep.txt os valores anterior/novo e o estado de validação.
    /// </summary>
    private void AplicarEndereco(CepAddressResult result, string origem)
    {
        CepDiagnosticLogger.Linha("=============== [APLICAÇÃO] ===============");
        CepDiagnosticLogger.Linha($"[APLICAÇÃO] Origem: {origem}");
        CepDiagnosticLogger.Linha($"[APLICAÇÃO] Success = {(result.Success ? "true" : "false")}");
        CepDiagnosticLogger.Linha($"[APLICAÇÃO] CEP retornado: {CepMaskHelper.Formatar(result.Cep)}");

        if (result.Success)
        {
            var logradouroAnterior = _formLogradouro;
            var bairroAnterior = _formBairro;
            var cidadeAnterior = _formCidade;
            var ufAnterior = _formUf;

            FormLogradouro = result.Logradouro ?? string.Empty;
            FormBairro = result.Bairro ?? string.Empty;
            FormCidade = result.Cidade ?? string.Empty;
            FormUf = result.Uf ?? string.Empty;

            CepDiagnosticLogger.Linha($"[APLICAÇÃO] Logradouro anterior: '{logradouroAnterior}'");
            CepDiagnosticLogger.Linha(
                $"[APLICAÇÃO] Logradouro novo: '{_formLogradouro}'");
            CepDiagnosticLogger.Linha($"[APLICAÇÃO] Bairro anterior: '{bairroAnterior}'");
            CepDiagnosticLogger.Linha(
                $"[APLICAÇÃO] Bairro novo: '{_formBairro}'");
            CepDiagnosticLogger.Linha($"[APLICAÇÃO] Cidade anterior: '{cidadeAnterior}'");
            CepDiagnosticLogger.Linha(
                $"[APLICAÇÃO] Cidade nova: '{_formCidade}'");
            CepDiagnosticLogger.Linha($"[APLICAÇÃO] UF anterior: '{ufAnterior}'");
            CepDiagnosticLogger.Linha(
                $"[APLICAÇÃO] UF nova: '{_formUf}'");

            LimparErroValidacao(CepCampo);
            CepDiagnosticLogger.Linha("[APLICAÇÃO] Erro do CEP removido (borda vermelha e resumo limpos).");
            CepDiagnosticLogger.Linha("[APLICAÇÃO] Endereço aplicado ao formulário.");
        }
        else
        {
            CepDiagnosticLogger.Linha($"[APLICAÇÃO] Mensagem de erro: {result.ErrorMessage}");
            RegistrarErroCep(result.Cep);
            CepDiagnosticLogger.Linha("[APLICAÇÃO] Erro registrado na infraestrutura de validação (borda vermelha + resumo no topo).");
            CepDiagnosticLogger.Linha("[APLICAÇÃO] Endereço antigo não é mantido como pertencente ao novo CEP.");
        }

        CepDiagnosticLogger.Linha("=============== FIM [APLICAÇÃO] ===============");
        OnPropertyChanged(nameof(CepPodeConsultarManualmente));
    }

    /// <summary>
    /// Registra o erro de consulta de CEP na infraestrutura de validação (campo vermelho +
    /// resumo no topo) e mantém o aviso geral do formulário.
    /// </summary>
    private void RegistrarErroCep(string cep)
    {
        var mensagem = $"Não foi possível localizar o CEP '{CepMaskHelper.Formatar(cep)}'. Preencha o endereço manualmente.";
        EditorMensagem = mensagem;
        DefinirErroValidacao(CepCampo, mensagem);
    }

    /// <summary>Sincroniza o resultado de uma validação de campo com a infraestrutura de validação.</summary>
    private void AtualizarErroCampo(string campo, ValidationResult resultado)
    {
        if (resultado.IsValid)
            LimparErroValidacao(campo);
        else
            DefinirErroValidacao(campo, resultado.FirstError);
    }

    private bool CepTemOitoDigitos()
    {
        var cep = CepMaskHelper.Normalizar(_formCep);
        return cep.Length == 8 && cep.All(char.IsDigit);
    }

    private void AbrirNovo()
    {
        _editorNovo = true;
        _formId = 0;
        LimparErrosValidacao();
        EditorTitulo = "Novo técnico";
        FormCodigo = string.Empty;
        FormNome = string.Empty;
        FormCpf = string.Empty;
        FormRg = string.Empty;
        FormTelefone = string.Empty;
        FormCelular = string.Empty;
        FormEmail = string.Empty;
        FormEspecialidade = string.Empty;
        FormRegistroProfissional = string.Empty;
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

    private void AbrirEdicao(TecnicoDto? tecnico)
    {
        if (tecnico == null)
            return;

        PreencherFormulario(tecnico);
        EditorTitulo = "Editar técnico";
        _editorNovo = false;
        _formId = tecnico.Id;
        EditorAberto = true;

        OnPropertyChanged(nameof(EditorNovo));
    }

    private void AbrirVisualizacao(TecnicoDto? tecnico)
    {
        if (tecnico == null)
            return;

        PreencherFormulario(tecnico);
        VisualizacaoAberta = true;
    }

    private void PreencherFormulario(TecnicoDto tecnico)
    {
        // Lupa é a ÚNICA porta de consulta: carregar cadastro NUNCA consulta.
        _formId = tecnico.Id;
        FormCodigo = tecnico.Codigo;
            FormNome = tecnico.Nome;
            FormCpf = CpfCnpjValidatorEx.Formatar(tecnico.Cpf);
            FormRg = tecnico.Rg ?? string.Empty;
            FormTelefone = PhoneMaskHelper.Formatar(tecnico.Telefone);
            FormCelular = PhoneMaskHelper.Formatar(tecnico.Celular);
            FormEmail = tecnico.Email ?? string.Empty;
            FormEspecialidade = tecnico.Especialidade ?? string.Empty;
            FormRegistroProfissional = tecnico.RegistroProfissional ?? string.Empty;
            FormCep = CepMaskHelper.Formatar(tecnico.Cep);
            FormLogradouro = tecnico.Logradouro ?? string.Empty;
            FormNumero = tecnico.Numero ?? string.Empty;
            FormComplemento = tecnico.Complemento ?? string.Empty;
            FormBairro = tecnico.Bairro ?? string.Empty;
            FormCidade = tecnico.Cidade ?? string.Empty;
            FormUf = tecnico.Uf ?? string.Empty;
            FormObservacoes = tecnico.Observacoes ?? string.Empty;
            FormAtivo = tecnico.Ativo;
            _formQuantidadeOrcamentos = tecnico.QuantidadeOrcamentos;

        LimparErrosValidacao();
        EditorMensagem = string.Empty;

        OnPropertyChanged(nameof(ResumoOrcamentos));
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

     private async Task SalvarAsync()
     {
         var validation = FormValidator.Create()
             .Required(FormNome, "o nome do técnico")
             .CpfCnpj(FormCpf)
             .Build();

         // Reflete os campos obrigatórios na infraestrutura de validação compartilhada
         // (resumo no topo do formulário + foco no primeiro campo inválido).
         AtualizarErroCampo("Nome", FormValidator.Required(FormNome, "o nome do técnico"));
         AtualizarErroCampo("Cpf", FormValidator.CpfCnpj(FormCpf));

         if (!validation.IsValid)
         {
             EditorMensagem = validation.FirstError;
             SolicitarFocoPrimeiroCampoInvalido();
             return;
         }

        var nome = FormNome.Trim().ToUpperInvariant();

        try
        {
            var dto = CriarDto();

            if (EditorNovo)
            {
                await _tecnicoService.CriarAsync(dto);
            }
            else
            {
                await _tecnicoService.AtualizarAsync(new AtualizarTecnicoDto
                {
                    Id = _formId,
                    Codigo = dto.Codigo,
                    Nome = dto.Nome,
                    Cpf = dto.Cpf,
                    Rg = dto.Rg,
                    Telefone = dto.Telefone,
                    Celular = dto.Celular,
                    Email = dto.Email,
                    Especialidade = dto.Especialidade,
                    RegistroProfissional = dto.RegistroProfissional,
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
            _reportStatus?.Invoke(edicao ? $"Técnico \"{nome}\" atualizado." : $"Técnico \"{nome}\" cadastrado.");
        }
        catch (Exception ex)
        {
            EditorMensagem = ex.Message;
        }
    }

    /// <summary>Projeta o formulário em um DTO de criação (reaproveitado na atualização).</summary>
    private CriarTecnicoDto CriarDto()
    {
        return new CriarTecnicoDto
        {
            Codigo = EditorNovo ? null : FormCodigo,
            Nome = FormNome.Trim().ToUpperInvariant(),
            Cpf = CpfCnpjValidatorEx.Normalizar(FormCpf),
            Rg = InputFormattingHelper.NormalizeText(FormRg),
            Telefone = PhoneMaskHelper.Normalizar(FormTelefone),
            Celular = PhoneMaskHelper.Normalizar(FormCelular),
            Email = InputFormattingHelper.NormalizeEmail(FormEmail),
            Especialidade = InputFormattingHelper.NormalizeText(FormEspecialidade),
            RegistroProfissional = InputFormattingHelper.NormalizeText(FormRegistroProfissional),
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
    }

    private async Task AlternarSituacaoAsync(TecnicoDto? tecnico)
    {
        if (tecnico == null)
            return;

        var nome = tecnico.Nome;

        try
        {
            // Técnicos vinculados a orçamentos podem apenas ser inativados (exclusão bloqueada).
            if (tecnico.Ativo)
            {
                await _tecnicoService.InativarAsync(tecnico.Id);
            }
            else
            {
                var dto = new AtualizarTecnicoDto
                {
                    Id = tecnico.Id,
                    Codigo = tecnico.Codigo,
                    Nome = tecnico.Nome,
                    Cpf = tecnico.Cpf,
                    Rg = tecnico.Rg,
                    Telefone = tecnico.Telefone,
                    Celular = tecnico.Celular,
                    Email = tecnico.Email,
                    Especialidade = tecnico.Especialidade,
                    RegistroProfissional = tecnico.RegistroProfissional,
                    Cep = tecnico.Cep,
                    Logradouro = tecnico.Logradouro,
                    Numero = tecnico.Numero,
                    Complemento = tecnico.Complemento,
                    Bairro = tecnico.Bairro,
                    Cidade = tecnico.Cidade,
                    Uf = tecnico.Uf,
                    Observacoes = tecnico.Observacoes,
                    Ativo = true
                };

                await _tecnicoService.AtualizarAsync(dto);
            }

            await CarregarAsync();
            _reportStatus?.Invoke($"Técnico \"{nome}\" {(tecnico.Ativo ? "inativado" : "reativado")}.");
        }
        catch (Exception ex)
        {
            _reportStatus?.Invoke($"Falha ao alterar a situação: {ex.Message}");
        }
    }

    // ---------- Exclusão ----------

    private void AbrirConfirmacaoExclusao(TecnicoDto? tecnico)
    {
        if (tecnico == null)
            return;

        if (tecnico.QuantidadeOrcamentos > 0)
        {
            _reportStatus?.Invoke(
                $"O técnico \"{tecnico.Nome}\" possui {tecnico.QuantidadeOrcamentos} orçamento(s) vinculado(s) e não pode ser excluído. Inative-o em vez de excluir.");
            return;
        }

        _tecnicoParaExcluir = tecnico;
        ConfirmacaoAberta = true;
    }

    private void FecharConfirmacao()
    {
        ConfirmacaoAberta = false;
        _tecnicoParaExcluir = null;
    }

    private async Task ExcluirAsync()
    {
        if (_tecnicoParaExcluir == null)
            return;

        var tecnico = _tecnicoParaExcluir;

        try
        {
            await _tecnicoService.ExcluirAsync(tecnico.Id);
            FecharConfirmacao();
            await CarregarAsync();
            _reportStatus?.Invoke($"Técnico \"{tecnico.Nome}\" excluído.");
        }
        catch (Exception ex)
        {
            FecharConfirmacao();
            _reportStatus?.Invoke(ex.Message);
        }
    }
}

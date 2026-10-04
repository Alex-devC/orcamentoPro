using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using OrcPro.Application.DTOs.Cliente;
using OrcPro.Application.DTOs.Auth;
using OrcPro.Application.DTOs.Common;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Application.Services;
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
    private const string CepCampo = "CEP";

    private readonly IClienteService _clienteService;
    private readonly ICepService? _cepService;
    private readonly Action<string>? _reportStatus;
    private readonly DispatcherTimer _buscaTimer;
    private bool _cepCarregando;

    // Lupa é a ÚNICA porta de entrada de consulta (sem auto-consulta via setter,
    // LostFocus, TextChanged ou 8 dígitos — ver ConsultarCepManualmenteAsync).

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
        ConsultarCepCommand = new AsyncRelayCommand(_ => ConsultarCepManualmenteAsync());
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
            OnPropertyChanged(nameof(CepPodeConsultarManualmente));

            // Lupa é a ÚNICA porta de consulta: digitar/apagar/sair do campo ou
            // pressionar ENTER NUNCA consulta. Apenas limpa o erro anterior para
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

    public string ConfirmacaoMensagem => _clienteParaExcluir is null
        ? string.Empty
        : $"Deseja excluir o cliente \"{_clienteParaExcluir.NomeRazaoSocial}\" ({_clienteParaExcluir.Codigo})? Esta ação não pode ser desfeita.";

    // ---------- CEP (LUPA É A ÚNICA PORTA DE CONSULTA) ----------

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
    /// SEMPRE via propriedades (FormLogradouro/FormBairro/FormCidade/FormUf), de modo que
    /// SetField dispare PropertyChanged e a UI reflita imediatamente. Registra no
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

            // CAUSA RAIZ do bug "salva mas não mostra": atribuir backing field
            // (_formLogradouro = ...) NÃO dispara PropertyChanged → a tela não atualiza.
            // Aqui SEMPRE usamos as propriedades → SetField → PropertyChanged.
            FormLogradouro = result.Logradouro ?? string.Empty;
            CepDiagnosticLogger.Linha($"[APLICAÇÃO] PropertyChanged confirmado: FormLogradouro = '{_formLogradouro}'");
            FormBairro = result.Bairro ?? string.Empty;
            CepDiagnosticLogger.Linha($"[APLICAÇÃO] PropertyChanged confirmado: FormBairro = '{_formBairro}'");
            FormCidade = result.Cidade ?? string.Empty;
            CepDiagnosticLogger.Linha($"[APLICAÇÃO] PropertyChanged confirmado: FormCidade = '{_formCidade}'");
            FormUf = result.Uf ?? string.Empty;
            CepDiagnosticLogger.Linha($"[APLICAÇÃO] PropertyChanged confirmado: FormUf = '{_formUf}'");

            CepDiagnosticLogger.Linha($"[APLICAÇÃO] Logradouro anterior: '{logradouroAnterior}'");
            CepDiagnosticLogger.Linha(
                $"[APLICAÇÃO] Logradouro novo: '{_formLogradouro}' (via propriedade → PropertyChanged disparado)");
            CepDiagnosticLogger.Linha($"[APLICAÇÃO] Bairro anterior: '{bairroAnterior}'");
            CepDiagnosticLogger.Linha(
                $"[APLICAÇÃO] Bairro novo: '{_formBairro}' (via propriedade → PropertyChanged disparado)");
            CepDiagnosticLogger.Linha($"[APLICAÇÃO] Cidade anterior: '{cidadeAnterior}'");
            CepDiagnosticLogger.Linha(
                $"[APLICAÇÃO] Cidade nova: '{_formCidade}' (via propriedade → PropertyChanged disparado)");
            CepDiagnosticLogger.Linha($"[APLICAÇÃO] UF anterior: '{ufAnterior}'");
            CepDiagnosticLogger.Linha(
                $"[APLICAÇÃO] UF nova: '{_formUf}' (via propriedade → PropertyChanged disparado)");

            LimparErroValidacao(CepCampo);
            CepDiagnosticLogger.Linha("[APLICAÇÃO] Erro do CEP removido (borda vermelha e resumo limpos).");
            CepDiagnosticLogger.Linha("[APLICAÇÃO] Endereço aplicado ao formulário via propriedades.");
        }
        else
        {
            CepDiagnosticLogger.Linha($"[APLICAÇÃO] Mensagem de erro: {result.ErrorMessage}");
            // Falha: limpa via propriedades para a UI não exibir endereço antigo
            // como se pertencesse ao novo CEP.
            FormLogradouro = string.Empty;
            FormBairro = string.Empty;
            FormCidade = string.Empty;
            FormUf = string.Empty;
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

    /// <summary>Indica se o botão de lupa de CEP pode ser clicado (serviço disponível, não carregando, CEP com 8 dígitos).</summary>
    public bool CepPodeConsultarManualmente => _cepService is not null && !CepCarregando && CepTemOitoDigitos();
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
        LimparErrosValidacao();
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
        // Lupa é a ÚNICA porta de consulta: carregar cadastro NUNCA consulta.
        _formId = cliente.Id;
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
             .Required(FormNome, "o Nome / Razão Social do cliente")
             .Required(FormCelular, "o Celular / WhatsApp do cliente")
             .Required(FormEmail, "o e-mail principal do cliente")
             .CpfCnpj(FormCpfCnpj)
             .Build();

         // Reflete os campos obrigatórios na infraestrutura de validação compartilhada
         // (resumo no topo do formulário + foco no primeiro campo inválido).
         AtualizarErroCampo("Nome", FormValidator.Required(FormNome, "o Nome / Razão Social do cliente"));
         AtualizarErroCampo("Celular", FormValidator.Required(FormCelular, "o Celular / WhatsApp do cliente"));
         AtualizarErroCampo("Email", FormValidator.Required(FormEmail, "o e-mail principal do cliente"));
         AtualizarErroCampo("CpfCnpj", FormValidator.CpfCnpj(FormCpfCnpj));

         if (!validation.IsValid)
         {
             EditorMensagem = validation.FirstError;
             SolicitarFocoPrimeiroCampoInvalido();
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
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using OrcPro.Application.DTOs.Auth;
using OrcPro.Application.DTOs.Cliente;
using OrcPro.Application.DTOs.Common;
using OrcPro.Application.DTOs.Empresa;
using OrcPro.Application.DTOs.Orcamento;
using OrcPro.Application.DTOs.Peca;
using OrcPro.Application.DTOs.Servico;
using OrcPro.Application.DTOs.Tecnico;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Common;
using OrcPro.Domain.Common.Formatters;
using OrcPro.Domain.Entities.Orcamento;

namespace OrcPro.App.ViewModels;

/// <summary>
/// Tela de Orçamentos: listagem com pesquisa, filtros (status, cliente e período),
/// ordenação no banco e paginação, além das ações Novo, Editar, Visualizar, Alterar Status
/// e Excluir (com confirmação).
///
/// <para>Integra os cadastros existentes — Cliente, Emitente (Minha Empresa), Peças,
/// Serviços e Técnicos — sem duplicar nenhum deles. O emitente é obtido do serviço de
/// Minha Empresa; não há nada a redigitar dentro do orçamento.</para>
/// </summary>
public class OrcamentosViewModel : ViewModelBase
{
    private const int PageSize = 20;
    private const int DebounceBuscaMilissegundos = 350;

    private const string CampoCliente = "Cliente";
    private const string CampoValidade = "Validade";
    private const string CampoDesconto = "Desconto";
    private const string CampoItens = "Itens";

    private readonly IOrcamentoService _orcamentoService;
    private readonly IClienteService _clienteService;
    private readonly IPecaService _pecaService;
    private readonly IServicoService _servicoService;
    private readonly ITecnicoService _tecnicoService;
    private readonly IEmpresaService _empresaService;
    private readonly Action<string>? _reportStatus;
    private readonly DispatcherTimer _buscaTimer;

    /// <summary>Usuário autenticado: responsável por criação, histórico e alterações.</summary>
    public int UsuarioAtualId { get; }

    private readonly bool _podeVisualizar;
    private readonly bool _podeCriar;
    private readonly bool _podeEditar;
    private readonly bool _podeExcluir;
    private readonly bool _podeAlterarStatus;

    private OrcamentoResumoDto? _orcamentoSelecionado;
    private OrcamentoResumoDto? _orcamentoParaExcluir;

    private string _busca = string.Empty;
    private int _statusFiltroId;
    private int _clienteFiltroId;
    private DateTime? _dataInicial;
    private DateTime? _dataFinal;
    private int _pagina = 1;
    private int _totalPaginas = 1;
    private int _totalRegistros;
    private SortRequest? _ordenacao;

    private bool _editorAberto;
    private bool _visualizacaoAberta;
    private bool _confirmacaoAberta;
    private string _editorMensagem = string.Empty;

    public OrcamentosViewModel(
        IOrcamentoService orcamentoService,
        IClienteService clienteService,
        IPecaService pecaService,
        IServicoService servicoService,
        ITecnicoService tecnicoService,
        IEmpresaService empresaService,
        UsuarioSessaoDto sessao,
        Action<string>? reportStatus = null)
    {
        _orcamentoService = orcamentoService;
        _clienteService = clienteService;
        _pecaService = pecaService;
        _servicoService = servicoService;
        _tecnicoService = tecnicoService;
        _empresaService = empresaService;
        _reportStatus = reportStatus;

        UsuarioAtualId = sessao.Id;

        _podeVisualizar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Orcamentos.Visualizar);
        _podeCriar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Orcamentos.Criar);
        _podeEditar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Orcamentos.Editar);
        _podeExcluir = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Orcamentos.Excluir);
        _podeAlterarStatus = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Orcamentos.AlterarStatus);

        _buscaTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceBuscaMilissegundos) };
        _buscaTimer.Tick += OnBuscaTimerTick;

        NovoCommand = new AsyncRelayCommand(_ => AbrirNovoAsync(), _ => _podeCriar && EmitenteConfigurado);
        EditarCommand = new RelayCommand(p => AbrirEdicao(p as OrcamentoResumoDto ?? OrcamentoSelecionado), _ => _podeEditar);
        VisualizarCommand = new RelayCommand(p => AbrirVisualizacao(p as OrcamentoResumoDto ?? OrcamentoSelecionado), _ => _podeVisualizar);
        SalvarCommand = new AsyncRelayCommand(_ => SalvarAsync());
        CancelarEditorCommand = new RelayCommand(_ => FecharEditor());
        FecharVisualizacaoCommand = new RelayCommand(_ => FecharVisualizacao());
        AlterarStatusCommand = new RelayCommand(p => AbrirAlteracaoStatus(p as OrcamentoResumoDto ?? OrcamentoSelecionado), _ => _podeAlterarStatus);
        ConfirmarStatusCommand = new AsyncRelayCommand(_ => ConfirmarAlteracaoStatusAsync());
        ExcluirCommand = new RelayCommand(p => AbrirConfirmacaoExclusao(p as OrcamentoResumoDto ?? OrcamentoSelecionado), _ => _podeExcluir);
        ConfirmarExclusaoCommand = new AsyncRelayCommand(_ => ExcluirAsync());
        CancelarExclusaoCommand = new RelayCommand(_ => FecharConfirmacao());
        AtualizarListaCommand = new AsyncRelayCommand(_ => CarregarAsync());
        LimparBuscaCommand = new RelayCommand(_ => Busca = string.Empty);
        PaginaAnteriorCommand = new AsyncRelayCommand(_ => IrParaPagina(Pagina - 1), _ => PodePaginaAnterior);
        PaginaProximaCommand = new AsyncRelayCommand(_ => IrParaPagina(Pagina + 1), _ => PodePaginaProxima);
        CarregarCatálogosCommand = new AsyncRelayCommand(_ => CarregarCatalogosAsync());
    }

    public bool PodeVisualizar => _podeVisualizar;
    public bool PodeCriar => _podeCriar;
    public bool PodeEditar => _podeEditar;
    public bool PodeExcluir => _podeExcluir;
    public bool PodeAlterarStatus => _podeAlterarStatus;

    /// <summary>Emitente configurado em Minha Empresa. Sem ele não há como orçar.</summary>
    public bool EmitenteConfigurado { get; private set; } = true;

    public ObservableCollection<OrcamentoResumoDto> Orcamentos { get; } = new();

    public string Busca
    {
        get => _busca;
        set
        {
            if (!SetField(ref _busca, value)) return;
            AgendarRecarga();
        }
    }

    /// <summary>0 = todos; caso contrário, id do status selecionado.</summary>
    public int StatusFiltroId
    {
        get => _statusFiltroId;
        set
        {
            if (!SetField(ref _statusFiltroId, value)) return;
            Pagina = 1;
            _ = CarregarAsync();
        }
    }

    /// <summary>0 = todos os clientes.</summary>
    public int ClienteFiltroId
    {
        get => _clienteFiltroId;
        set
        {
            if (!SetField(ref _clienteFiltroId, value)) return;
            Pagina = 1;
            _ = CarregarAsync();
        }
    }

    public DateTime? DataInicial
    {
        get => _dataInicial;
        set
        {
            if (!SetField(ref _dataInicial, value)) return;
            Pagina = 1;
            _ = CarregarAsync();
        }
    }

    public DateTime? DataFinal
    {
        get => _dataFinal;
        set
        {
            if (!SetField(ref _dataFinal, value)) return;
            Pagina = 1;
            _ = CarregarAsync();
        }
    }

    public OrcamentoResumoDto? OrcamentoSelecionado
    {
        get => _orcamentoSelecionado;
        set => SetField(ref _orcamentoSelecionado, value);
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
    public bool TemRegistros => Orcamentos.Count > 0;

    public string ResumoPaginacao
    {
        get
        {
            var registro = TotalRegistros == 1 ? "registro" : "registros";
            return $"Página {Pagina} de {TotalPaginas} · {TotalRegistros} {registro}";
        }
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

    public bool EditorAberto
    {
        get => _editorAberto;
        private set => SetField(ref _editorAberto, value);
    }

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

    public string ConfirmacaoMensagem => _orcamentoParaExcluir is null
        ? string.Empty
        : $"Deseja excluir o orçamento {_orcamentoParaExcluir.Numero}? Esta ação não pode ser desfeita.";

    public AsyncRelayCommand NovoCommand { get; }
    public RelayCommand EditarCommand { get; }
    public RelayCommand VisualizarCommand { get; }
    public AsyncRelayCommand SalvarCommand { get; }
    public RelayCommand CancelarEditorCommand { get; }
    public RelayCommand FecharVisualizacaoCommand { get; }
    public RelayCommand AlterarStatusCommand { get; }
    public AsyncRelayCommand ConfirmarStatusCommand { get; }
    public RelayCommand ExcluirCommand { get; }
    public AsyncRelayCommand ConfirmarExclusaoCommand { get; }
    public RelayCommand CancelarExclusaoCommand { get; }
    public AsyncRelayCommand AtualizarListaCommand { get; }
    public RelayCommand LimparBuscaCommand { get; }
    public AsyncRelayCommand PaginaAnteriorCommand { get; }
    public AsyncRelayCommand PaginaProximaCommand { get; }
    public AsyncRelayCommand CarregarCatálogosCommand { get; }

    public override async Task InitializeAsync()
    {
        await CarregarCatalogosAsync();
        await CarregarAsync();
    }
    // ======================= Catálogos auxiliares (seleções) =======================

    public ObservableCollection<ClienteDto> Clientes { get; } = new();
    public ObservableCollection<PecaDto> Pecas { get; } = new();
    public ObservableCollection<ServicoDto> Servicos { get; } = new();
    public ObservableCollection<TecnicoDto> Tecnicos { get; } = new();
    public ObservableCollection<OrcamentoStatusDto> StatusDisponiveis { get; } = new();

    /// <summary>Lista do filtro de status, já com a opção "Todos" (Id = 0).</summary>
    public List<OrcamentoStatusDto> FiltrosStatus { get; private set; } = new();

    /// <summary>Lista do filtro de cliente, já com a opção "Todos" (Id = 0).</summary>
    public List<ClienteDto> FiltrosCliente { get; private set; } = new();

    private readonly ClienteDto _todosClientes = new() { Id = 0, NomeRazaoSocial = "Todos os clientes" };

    private static OrcamentoStatusDto TodosStatus()
        => new() { Id = 0, Codigo = "TODOS", Nome = "Todos os status" };

    /// <summary>Emitente exibido em modo somente leitura no cabeçalho do orçamento.</summary>
    public EmpresaDto? Emitente { get; private set; }

    public string EmitenteResumo => Emitente is null
        ? "EMITENTE NAO CONFIGURADO"
        : $"{Emitente.RazaoSocial}{(string.IsNullOrWhiteSpace(Emitente.Cnpj) ? string.Empty : " — " + Emitente.Cnpj)}";

    private async Task CarregarCatalogosAsync()
    {
        try
        {
            // O emitente vem do serviço de Minha Empresa — o usuário não digita os dados aqui.
            Emitente = await _empresaService.ObterAsync();
            EmitenteConfigurado = Emitente is not null;
            OnPropertyChanged(nameof(Emitente));
            OnPropertyChanged(nameof(EmitenteResumo));
            OnPropertyChanged(nameof(EmitenteConfigurado));

            var status = await _orcamentoService.ListarStatusDisponiveisAsync();
            StatusDisponiveis.Clear();
            foreach (var s in status)
                StatusDisponiveis.Add(s);

            FiltrosStatus = new List<OrcamentoStatusDto> { TodosStatus() };
            FiltrosStatus.AddRange(StatusDisponiveis);
            OnPropertyChanged(nameof(FiltrosStatus));

            // Limpa antes de recarregar: AbrirNovoOrcamentoAsync (ribbon Novo) chama este
            // método logo após o InitializeAsync; sem o Clear os combos teriam itens duplicados.
            Clientes.Clear();
            Pecas.Clear();
            Servicos.Clear();
            Tecnicos.Clear();

            foreach (var c in await _clienteService.ListarTodosAtivosAsync())
                Clientes.Add(c);

            FiltrosCliente = new List<ClienteDto> { _todosClientes };
            FiltrosCliente.AddRange(Clientes);
            OnPropertyChanged(nameof(FiltrosCliente));

            foreach (var p in await _pecaService.ListarTodasAtivasAsync())
                Pecas.Add(p);

            foreach (var s in await _servicoService.ListarTodosAtivosAsync())
                Servicos.Add(s);

            foreach (var t in await _tecnicoService.ListarTodosAtivosAsync())
                Tecnicos.Add(t);
        }
        catch (Exception ex)
        {
            EmitenteConfigurado = false;
            OnPropertyChanged(nameof(EmitenteConfigurado));
            _reportStatus?.Invoke($"Falha ao carregar catálogos: {ex.Message}");
        }
    }

    // ======================= Listagem =======================

    public async Task CarregarAsync()
    {
        var request = new PagedRequest
        {
            PageNumber = Pagina,
            PageSize = PageSize,
            SearchTerm = string.IsNullOrWhiteSpace(Busca) ? null : Busca
        };

        if (StatusFiltroId > 0)
            request.Filters.Add(new FilterRequest { PropertyName = "StatusId", Value = StatusFiltroId.ToString() });

        if (ClienteFiltroId > 0)
            request.Filters.Add(new FilterRequest { PropertyName = "ClienteId", Value = ClienteFiltroId.ToString() });

        if (DataInicial.HasValue)
            request.Filters.Add(new FilterRequest { PropertyName = "DataInicial", Value = DataInicial.Value.ToString("yyyy-MM-dd") });

        if (DataFinal.HasValue)
            request.Filters.Add(new FilterRequest { PropertyName = "DataFinal", Value = DataFinal.Value.ToString("yyyy-MM-dd") });

        if (_ordenacao != null)
            request.Sorts.Add(_ordenacao);

        try
        {
            var result = await _orcamentoService.ListarPaginadoAsync(request);

            Orcamentos.Clear();
            foreach (var o in result.Items)
                Orcamentos.Add(o);

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
            _reportStatus?.Invoke($"Falha ao carregar orçamentos: {ex.Message}");
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

    private void AbrirConfirmacaoExclusao(OrcamentoResumoDto? orcamento)
    {
        if (orcamento == null)
            return;

        _orcamentoParaExcluir = orcamento;
        ConfirmacaoAberta = true;
    }

    private void FecharConfirmacao()
    {
        ConfirmacaoAberta = false;
        _orcamentoParaExcluir = null;
    }

    private async Task ExcluirAsync()
    {
        if (_orcamentoParaExcluir == null)
            return;

        var alvo = _orcamentoParaExcluir;

        try
        {
            await _orcamentoService.ExcluirAsync(new ExcluirOrcamentoDto
            {
                OrcamentoId = alvo.Id,
                UsuarioId = UsuarioAtualId
            });

            FecharConfirmacao();
            await CarregarAsync();
            _reportStatus?.Invoke($"Orçamento {alvo.Numero} excluído.");
        }
        catch (Exception ex)
        {
            FecharConfirmacao();
            _reportStatus?.Invoke(ex.Message);
        }
    }

    private void FecharVisualizacao() => VisualizacaoAberta = false;
    // ======================= Editor: linhas de peça =======================

    /// <summary>
    /// Linha editável de peça na tela. O valor unitário é TEXTO LIVRE (entrada natural,
    /// igual ao módulo Peças): digitar "1" nunca vira "1000"; a conversão para decimal
    /// acontece na validação, via <see cref="DecimalInputHelper"/>.
    /// </summary>
    public sealed class LinhaItem : ViewModelBase
    {
        private readonly Action _recalcular;

        private int? _pecaId;
        private string _codigo = string.Empty;
        private string _descricao = string.Empty;
        private string _unidade = "UN";
        private string _quantidadeTexto = "1";
        private string _precoTexto = string.Empty;
        private string _descontoTexto = string.Empty;

        public LinhaItem(Action recalcular) => _recalcular = recalcular;

        public int? PecaId
        {
            get => _pecaId;
            set => SetField(ref _pecaId, value);
        }

        public string Codigo
        {
            get => _codigo;
            set => SetField(ref _codigo, value);
        }

        public string Descricao
        {
            get => _descricao;
            set => SetField(ref _descricao, value);
        }

        public string Unidade
        {
            get => _unidade;
            set => SetField(ref _unidade, value);
        }

        public string QuantidadeTexto
        {
            get => _quantidadeTexto;
            set { if (SetField(ref _quantidadeTexto, value)) Recalcular(); }
        }

        public string PrecoTexto
        {
            get => _precoTexto;
            set { if (SetField(ref _precoTexto, value)) Recalcular(); }
        }

        public string DescontoTexto
        {
            get => _descontoTexto;
            set { if (SetField(ref _descontoTexto, value)) Recalcular(); }
        }

        public decimal Quantidade => DecimalInputHelper.ConverterOuPadrao(QuantidadeTexto);
        public decimal Preco => DecimalInputHelper.ConverterOuPadrao(PrecoTexto);
        public decimal Desconto => DecimalInputHelper.ConverterOuPadrao(DescontoTexto);

        public decimal Total => OrcPro.Domain.Common.Calculos.OrcamentoCalculo.TotalItem(Quantidade, Preco, Desconto);

        /// <summary>Preenche a linha a partir de uma peça do cadastro.</summary>
        public void AplicarPeca(PecaDto peca)
        {
            PecaId = peca.Id;
            Codigo = peca.Codigo;
            Descricao = peca.Descricao;
            Unidade = string.IsNullOrWhiteSpace(peca.UnidadeMedida) ? "UN" : peca.UnidadeMedida;

            // O PREÇO É COPIADO: alterar a peça depois não reescreve este orçamento.
            PrecoTexto = DecimalInputHelper.FormatarMonetario(peca.PrecoVenda, 2).Replace(".", string.Empty);

            if (Quantidade <= 0)
                QuantidadeTexto = "1";

            Recalcular();
        }

        private void Recalcular()
        {
            OnPropertyChanged(nameof(Quantidade));
            OnPropertyChanged(nameof(Preco));
            OnPropertyChanged(nameof(Desconto));
            OnPropertyChanged(nameof(Total));
            _recalcular();
        }
    }

    /// <summary>
    /// Linha editável de serviço/mão de obra. Assim como na peça, o valor do serviço é
    /// copiado para o orçamento e permanece independente do cadastro.
    /// </summary>
    public sealed class LinhaServico : ViewModelBase
    {
        private readonly Action _recalcular;

        private int? _servicoId;
        private string _descricao = string.Empty;
        private string _horasTexto = "1";
        private string _valorTexto = string.Empty;
        private string _descontoTexto = string.Empty;

        public LinhaServico(Action recalcular) => _recalcular = recalcular;

        public int? ServicoId
        {
            get => _servicoId;
            set => SetField(ref _servicoId, value);
        }

        public string Descricao
        {
            get => _descricao;
            set => SetField(ref _descricao, value);
        }

        public string HorasTexto
        {
            get => _horasTexto;
            set { if (SetField(ref _horasTexto, value)) Recalcular(); }
        }

        public string ValorTexto
        {
            get => _valorTexto;
            set { if (SetField(ref _valorTexto, value)) Recalcular(); }
        }

        public string DescontoTexto
        {
            get => _descontoTexto;
            set { if (SetField(ref _descontoTexto, value)) Recalcular(); }
        }

        public decimal Horas => DecimalInputHelper.ConverterOuPadrao(HorasTexto);
        public decimal Valor => DecimalInputHelper.ConverterOuPadrao(ValorTexto);
        public decimal Desconto => DecimalInputHelper.ConverterOuPadrao(DescontoTexto);

        public decimal Total => OrcPro.Domain.Common.Calculos.OrcamentoCalculo.TotalMaoDeObra(Horas, Valor, Desconto);

        /// <summary>Técnicos associados especificamente a esta linha de mão de obra.</summary>
        public ObservableCollection<TecnicoOrcamentoDto> TecnicosLinha { get; } = new();

        /// <summary>Resumo exibido na célula de técnicos da linha.</summary>
        public string TecnicosLinhaResumo => string.Join(" | ", TecnicosLinha.Select(t => t.Nome));

        /// <summary>Ids dos técnicos da linha, usados ao salvar a mão de obra.</summary>
        public List<int> TecnicosLinhaIds => TecnicosLinha.Select(t => t.TecnicoId).ToList();

        public void AdicionarTecnicoLinha(TecnicoOrcamentoDto? tecnico)
        {
            if (tecnico is null || TecnicosLinha.Any(t => t.TecnicoId == tecnico.TecnicoId))
                return;

            TecnicosLinha.Add(tecnico);
            OnPropertyChanged(nameof(TecnicosLinhaResumo));
        }

        public void RemoverTecnicoLinha(TecnicoOrcamentoDto? tecnico)
        {
            if (tecnico is null || !TecnicosLinha.Remove(tecnico))
                return;

            OnPropertyChanged(nameof(TecnicosLinhaResumo));
        }

        public void AplicarServico(OrcPro.Application.DTOs.Servico.ServicoDto servico)
        {
            ServicoId = servico.Id;
            Descricao = servico.Descricao;

            // Valor copiado do cadastro; ajustável no orçamento sem afetar o serviço.
            ValorTexto = DecimalInputHelper.FormatarMonetario(servico.Valor, 2).Replace(".", string.Empty);

            if (servico.TempoEstimado > 0)
                HorasTexto = DecimalInputHelper.FormatarQuantidade(servico.TempoEstimado).Replace(".", string.Empty);

            Recalcular();
        }

        private void Recalcular()
        {
            OnPropertyChanged(nameof(Horas));
            OnPropertyChanged(nameof(Valor));
            OnPropertyChanged(nameof(Desconto));
            OnPropertyChanged(nameof(Total));
            _recalcular();
        }
    }
    // ======================= Editor do orçamento =======================

    public ObservableCollection<LinhaItem> Itens { get; } = new();
    public ObservableCollection<LinhaServico> MaosDeObra { get; } = new();
    public ObservableCollection<TecnicoOrcamentoDto> TecnicosSelecionados { get; } = new();
    public ObservableCollection<OrcamentoHistoricoDto> Historicos { get; } = new();

    private int _orcamentoId;
    private string _numero = string.Empty;
    private int _clienteId;
    private int _diasValidade = 15;
    private string _descontoGeralTexto = string.Empty;
    private string _acrescimoTexto = string.Empty;
    private string _condicoesPagamento = string.Empty;
    private string _prazoEntrega = string.Empty;
    private string _garantia = string.Empty;
    private string _observacoes = string.Empty;
    private string _statusNome = string.Empty;
    private int _statusId;

    /// <summary>Técnico vinculado ao orçamento (veio do cadastro, nunca duplicado).</summary>
    public sealed class TecnicoOrcamentoDto
    {
        public int TecnicoId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? Funcao { get; set; }
    }

    public int OrcamentoId
    {
        get => _orcamentoId;
        private set => SetField(ref _orcamentoId, value);
    }

    public string Numero
    {
        get => _numero;
        private set => SetField(ref _numero, value);
    }

    public bool EditorNovo => OrcamentoId == 0;

    public int ClienteId
    {
        get => _clienteId;
        set => SetField(ref _clienteId, value);
    }

    public bool ClienteTemErro => CampoInvalido(CampoCliente);

    public int DiasValidade
    {
        get => _diasValidade;
        set => SetField(ref _diasValidade, value);
    }

    public bool ValidadeTemErro => CampoInvalido(CampoValidade);

    public string DescontoGeralTexto
    {
        get => _descontoGeralTexto;
        set { if (SetField(ref _descontoGeralTexto, value)) RecalcularTotais(); }
    }

    public bool DescontoTemErro => CampoInvalido(CampoDesconto);

    public string AcrescimoTexto
    {
        get => _acrescimoTexto;
        set { if (SetField(ref _acrescimoTexto, value)) RecalcularTotais(); }
    }

    public string CondicoesPagamento
    {
        get => _condicoesPagamento;
        set => SetField(ref _condicoesPagamento, value);
    }

    public string PrazoEntrega
    {
        get => _prazoEntrega;
        set => SetField(ref _prazoEntrega, value);
    }

    public string Garantia
    {
        get => _garantia;
        set => SetField(ref _garantia, value);
    }

    public string Observacoes
    {
        get => _observacoes;
        set => SetField(ref _observacoes, value);
    }

    public string StatusNome
    {
        get => _statusNome;
        private set => SetField(ref _statusNome, value);
    }

    // ---------- Totais (recalculados a cada edição, via regra centralizada do Domain) ----------

    public decimal SubtotalPecas => Itens.Sum(i => i.Total);

    public decimal SubtotalMaoDeObra => MaosDeObra.Sum(m => m.Total);

    public decimal DescontoGeral => DecimalInputHelper.ConverterOuPadrao(DescontoGeralTexto);

    public decimal Acrescimo => DecimalInputHelper.ConverterOuPadrao(AcrescimoTexto);

    public decimal TotalGeral => OrcPro.Domain.Common.Calculos.OrcamentoCalculo.TotalGeral(
        SubtotalPecas, SubtotalMaoDeObra, Acrescimo, DescontoGeral);

    private void RecalcularTotais()
    {
        OnPropertyChanged(nameof(SubtotalPecas));
        OnPropertyChanged(nameof(SubtotalMaoDeObra));
        OnPropertyChanged(nameof(DescontoGeral));
        OnPropertyChanged(nameof(Acrescimo));
        OnPropertyChanged(nameof(TotalGeral));
    }

    // ---------- Ações de linha ----------

    public void AdicionarLinhaItem()
    {
        var linha = new LinhaItem(RecalcularTotais);
        Itens.Add(linha);
        RecalcularTotais();
    }

    public void RemoverLinhaItem(LinhaItem? linha)
    {
        if (linha == null)
            return;

        Itens.Remove(linha);
        RecalcularTotais();
    }

    public void AdicionarLinhaServico()
    {
        var linha = new LinhaServico(RecalcularTotais);
        MaosDeObra.Add(linha);
        RecalcularTotais();
    }

    public void RemoverLinhaServico(LinhaServico? linha)
    {
        if (linha == null)
            return;

        MaosDeObra.Remove(linha);
        RecalcularTotais();
    }

    public void AdicionarTecnico(TecnicoDto? tecnico)
    {
        if (tecnico == null || TecnicosSelecionados.Any(t => t.TecnicoId == tecnico.Id))
            return;

        TecnicosSelecionados.Add(new TecnicoOrcamentoDto { TecnicoId = tecnico.Id, Nome = tecnico.Nome });
    }

    public void RemoverTecnico(TecnicoOrcamentoDto? tecnico)
    {
        if (tecnico == null)
            return;

        // Remove apenas o VÍNCULO: o técnico continua no cadastro global.
        TecnicosSelecionados.Remove(tecnico);
    }

    protected override void AoAlterarValidacao(string campo)
    {
        base.AoAlterarValidacao(campo);

        switch (campo)
        {
            case CampoCliente:
                OnPropertyChanged(nameof(ClienteTemErro));
                break;
            case CampoValidade:
                OnPropertyChanged(nameof(ValidadeTemErro));
                break;
            case CampoDesconto:
                OnPropertyChanged(nameof(DescontoTemErro));
                break;
            case CampoItens:
                OnPropertyChanged(nameof(ItensTemErro));
                break;
        }
    }

    public bool ItensTemErro => CampoInvalido(CampoItens);
    // ---------- Abrir / editar / visualizar ----------

    private void LimparEditor()
    {
        OrcamentoId = 0;
        Numero = string.Empty;
        ClienteId = 0;
        DiasValidade = 15;
        DescontoGeralTexto = string.Empty;
        AcrescimoTexto = string.Empty;
        CondicoesPagamento = string.Empty;
        PrazoEntrega = string.Empty;
        Garantia = string.Empty;
        Observacoes = string.Empty;
        StatusNome = string.Empty;
        StatusFiltroId = 0;
        _statusId = 0;

        Itens.Clear();
        MaosDeObra.Clear();
        TecnicosSelecionados.Clear();
        Historicos.Clear();

        LimparErrosValidacao();
        EditorMensagem = string.Empty;
        RecalcularTotais();
        OnPropertyChanged(nameof(EditorNovo));
    }

    private async Task AbrirNovoAsync()
    {
        // Reconfirma o emitente: ele pode ter sido configurado depois da abertura da tela.
        Emitente = await _empresaService.ObterAsync();
        EmitenteConfigurado = Emitente is not null;
        OnPropertyChanged(nameof(Emitente));
        OnPropertyChanged(nameof(EmitenteResumo));
        OnPropertyChanged(nameof(EmitenteConfigurado));

        // Sem emitente configurado o orçamento não pode existir.
        if (!EmitenteConfigurado)
        {
            _reportStatus?.Invoke("Configure Minha Empresa (Configurações → Minha Empresa) antes de criar um orçamento.");
            return;
        }

        LimparEditor();
        EditorAberto = true;
        OnPropertyChanged(nameof(EditorNovo));
        OnPropertyChanged(nameof(EditorTitulo));
    }

        /// <summary>Abre o formulário de novo orçamento (usado pelo botão "Novo" do Ribbon).</summary>
    public async Task AbrirNovoOrcamentoAsync()
    {
        await CarregarCatalogosAsync();
        await AbrirNovoAsync();
    }

    public string EditorTitulo => EditorNovo ? "Novo orçamento" : $"Editar orçamento {Numero}";

    private async Task CarregarParaEdicaoAsync(OrcamentoResumoDto? resumo, bool somenteLeitura)
    {
        if (resumo == null)
            return;

        try
        {
            var dto = await _orcamentoService.ObterPorIdAsync(resumo.Id);

            OrcamentoId = dto.Id;
            Numero = dto.Numero;
            ClienteId = dto.ClienteId;
            DiasValidade = dto.DiasValidade;
            DescontoGeralTexto = ParaTexto(dto.ValorDesconto);
            AcrescimoTexto = ParaTexto(dto.ValorAcrescimo);
            CondicoesPagamento = dto.CondicoesPagamento ?? string.Empty;
            PrazoEntrega = dto.PrazoEntrega ?? string.Empty;
            Garantia = dto.Garantia ?? string.Empty;
            Observacoes = dto.Observacoes ?? string.Empty;
            StatusNome = dto.StatusNome;
            _statusId = dto.StatusId;

            Itens.Clear();
            foreach (var item in dto.Itens)
            {
                var linha = new LinhaItem(RecalcularTotais)
                {
                    PecaId = item.PecaId,
                    Codigo = item.CodigoPeca,
                    Descricao = item.Descricao,
                    Unidade = item.UnidadeMedida,
                    QuantidadeTexto = ParaTexto(item.Quantidade),
                    PrecoTexto = ParaTexto(item.PrecoUnitario),
                    DescontoTexto = ParaTexto(item.ValorDesconto)
                };
                Itens.Add(linha);
            }

            MaosDeObra.Clear();
            foreach (var mo in dto.MaosDeObra)
            {
                var linha = new LinhaServico(RecalcularTotais)
                {
                    ServicoId = mo.ServicoId,
                    Descricao = mo.Descricao,
                    HorasTexto = ParaTexto(mo.QuantidadeHoras),
                    ValorTexto = ParaTexto(mo.ValorUnitario),
                    DescontoTexto = ParaTexto(mo.ValorDesconto)
                };

                foreach (var t in mo.Tecnicos)
                    linha.AdicionarTecnicoLinha(new TecnicoOrcamentoDto { TecnicoId = t.TecnicoId, Nome = t.TecnicoNome });

                MaosDeObra.Add(linha);
            }

            TecnicosSelecionados.Clear();
            foreach (var t in dto.Tecnicos)
                TecnicosSelecionados.Add(new TecnicoOrcamentoDto { TecnicoId = t.TecnicoId, Nome = t.TecnicoNome, Funcao = t.Funcao });

            Historicos.Clear();
            foreach (var h in dto.Historicos.OrderByDescending(h => h.DataRegistro))
                Historicos.Add(h);

            LimparErrosValidacao();
            EditorMensagem = string.Empty;
            RecalcularTotais();

            OnPropertyChanged(nameof(EditorNovo));
            OnPropertyChanged(nameof(EditorTitulo));

            if (somenteLeitura)
                VisualizacaoAberta = true;
            else
                EditorAberto = true;
        }
        catch (Exception ex)
        {
            _reportStatus?.Invoke($"Falha ao abrir o orçamento: {ex.Message}");
        }
    }

    private void AbrirEdicao(OrcamentoResumoDto? orcamento)
    {
        if (orcamento == null)
            return;

        VisualizacaoAberta = false;
        _ = CarregarParaEdicaoAsync(orcamento, somenteLeitura: false);
    }

    private void AbrirVisualizacao(OrcamentoResumoDto? orcamento)
    {
        if (orcamento == null)
            return;

        EditorAberto = false;
        _ = CarregarParaEdicaoAsync(orcamento, somenteLeitura: true);
    }

    private void FecharEditor()
    {
        EditorAberto = false;
        LimparErrosValidacao();
        EditorMensagem = string.Empty;
    }

    /// <summary>Decimal para texto de edição, sem separador de milhar.</summary>
    private static string ParaTexto(decimal valor)
        => DecimalInputHelper.FormatarQuantidade(valor).Replace(".", string.Empty);

    // ---------- Salvar ----------

    private async Task SalvarAsync()
    {
        var validacao = ValidarEditor();

        if (!validacao)
        {
            EditorMensagem = EditorMensagem;
            SolicitarFocoPrimeiroCampoInvalido();
            return;
        }

        try
        {
            var itens = Itens.Select(i => new AdicionarItemDto
            {
                PecaId = i.PecaId,
                CodigoPeca = i.Codigo.Trim(),
                Descricao = i.Descricao.Trim(),
                UnidadeMedida = i.Unidade,
                Quantidade = i.Quantidade,
                PrecoUnitario = i.Preco,
                ValorDesconto = i.Desconto
            }).ToList();

            var maosDeObra = MaosDeObra.Select(m => new AdicionarMaoDeObraDto
            {
                ServicoId = m.ServicoId,
                Descricao = m.Descricao.Trim(),
                QuantidadeHoras = m.Horas,
                ValorUnitario = m.Valor,
                ValorDesconto = m.Desconto,
                TecnicoIds = m.TecnicosLinhaIds
            }).ToList();

            var tecnicos = TecnicosSelecionados
                .Select(t => new AssociarTecnicoDto { TecnicoId = t.TecnicoId, Funcao = t.Funcao })
                .ToList();

            if (EditorNovo)
            {
                await _orcamentoService.CriarAsync(new CriarOrcamentoDto
                {
                    ClienteId = ClienteId,
                    UsuarioId = UsuarioAtualId,
                    DiasValidade = DiasValidade,
                    ValorDesconto = DescontoGeral,
                    ValorAcrescimo = Acrescimo,
                    CondicoesPagamento = CondicoesPagamento,
                    PrazoEntrega = PrazoEntrega,
                    Garantia = Garantia,
                    Observacoes = Observacoes,
                    ItensIniciais = itens,
                    MaosDeObraIniciais = maosDeObra,
                    TecnicosIniciais = tecnicos
                });
            }
            else
            {
                await EditarExistenteAsync(itens, maosDeObra, tecnicos);
            }

            FecharEditor();
            await CarregarAsync();
            Report("Orçamento salvo com sucesso.");
        }
        catch (Exception ex)
        {
            EditorMensagem = ex.Message;
        }
    }

    /// <summary>
    /// Edição de orçamento existente: o serviço atualiza o cabeçalho; as linhas são
    /// sincronizadas item a item (adiciona, remove e recria), tudo transacionado pelo
    /// serviço a cada operação.
    /// </summary>
    private async Task EditarExistenteAsync(
        List<AdicionarItemDto> itens,
        List<AdicionarMaoDeObraDto> maosDeObra,
        List<AssociarTecnicoDto> tecnicos)
    {
        var atual = await _orcamentoService.ObterPorIdAsync(OrcamentoId);

        await _orcamentoService.EditarAsync(new AtualizarOrcamentoDto
        {
            Id = OrcamentoId,
            ClienteId = ClienteId,
            DiasValidade = DiasValidade,
            ValorDesconto = DescontoGeral,
            ValorAcrescimo = Acrescimo,
            CondicoesPagamento = CondicoesPagamento,
            PrazoEntrega = PrazoEntrega,
            Garantia = Garantia,
            Observacoes = Observacoes
        }, UsuarioAtualId);

        // Itens que ainda existem permanecem; novos são adicionados; saídos são removidos.
        foreach (var existente in atual.Itens)
        {
            if (!Itens.Any(i => i.PecaId == existente.PecaId && i.Codigo == existente.CodigoPeca && i.Descricao == existente.Descricao))
                await _orcamentoService.RemoverItemAsync(OrcamentoId, existente.Id, UsuarioAtualId);
        }

        foreach (var novo in itens)
        {
            var jaExiste = atual.Itens.Any(i =>
                i.CodigoPeca == novo.CodigoPeca && i.Descricao == novo.Descricao
                && i.Quantidade == novo.Quantidade && i.PrecoUnitario == novo.PrecoUnitario);

            if (!jaExiste)
                await _orcamentoService.AdicionarItemAsync(OrcamentoId, novo, UsuarioAtualId);
        }

        foreach (var existente in atual.MaosDeObra)
        {
            var permanece = MaosDeObra.Any(m =>
                m.Descricao == existente.Descricao
                && m.Horas == existente.QuantidadeHoras
                && m.Valor == existente.ValorUnitario
                && MesmosTecnicos(existente, m.TecnicosLinhaIds));

            if (!permanece)
                await _orcamentoService.RemoverMaoDeObraAsync(OrcamentoId, existente.Id, UsuarioAtualId);
        }

        foreach (var novo in maosDeObra)
        {
            var jaExiste = atual.MaosDeObra.Any(m =>
                m.Descricao == novo.Descricao
                && m.QuantidadeHoras == novo.QuantidadeHoras
                && m.ValorUnitario == novo.ValorUnitario
                && MesmosTecnicos(m, novo.TecnicoIds));

            if (!jaExiste)
                await _orcamentoService.AdicionarMaoDeObraAsync(OrcamentoId, novo, UsuarioAtualId);
        }

        foreach (var existente in atual.Tecnicos)
        {
            if (!tecnicos.Any(t => t.TecnicoId == existente.TecnicoId))
                await _orcamentoService.DesassociarTecnicoAsync(OrcamentoId, existente.TecnicoId, UsuarioAtualId);
        }

        foreach (var novo in tecnicos)
        {
            if (!atual.Tecnicos.Any(t => t.TecnicoId == novo.TecnicoId))
                await _orcamentoService.AssociarTecnicoAsync(OrcamentoId, novo, UsuarioAtualId);
        }
    }

    /// <summary>Compara os técnicos de uma linha existente com os da linha que será enviada.</summary>
    private static bool MesmosTecnicos(OrcamentoMaoDeObraDto existente, List<int>? novos)
    {
        var atuais = existente.Tecnicos.Select(t => t.TecnicoId).OrderBy(id => id);
        var enviados = (novos ?? new List<int>()).OrderBy(id => id);
        return atuais.SequenceEqual(enviados);
    }

    private bool ValidarEditor()
    {
        var erros = new List<string>();

        if (ClienteId <= 0)
        {
            erros.Add("Selecione o cliente do orçamento.");
            DefinirErroValidacao(CampoCliente, "Selecione o cliente do orçamento.");
        }
        else
            LimparErroValidacao(CampoCliente);

        if (DiasValidade <= 0)
        {
            erros.Add("A validade deve ser maior que zero.");
            DefinirErroValidacao(CampoValidade, "A validade deve ser maior que zero.");
        }
        else
            LimparErroValidacao(CampoValidade);

        if (DescontoGeral < 0)
        {
            erros.Add("O desconto não pode ser negativo.");
            DefinirErroValidacao(CampoDesconto, "O desconto não pode ser negativo.");
        }
        else
            LimparErroValidacao(CampoDesconto);

        if (Itens.Count == 0 && MaosDeObra.Count == 0)
        {
            erros.Add("Adicione ao menos um item de peça ou um serviço.");
            DefinirErroValidacao(CampoItens, "Adicione ao menos um item de peça ou um serviço.");
        }
        else
            LimparErroValidacao(CampoItens);

        foreach (var item in Itens)
        {
            if (item.Quantidade <= 0)
                erros.Add($"A quantidade do item '{item.Descricao}' deve ser maior que zero.");

            if (item.Preco < 0)
                erros.Add($"O valor do item '{item.Descricao}' não pode ser negativo.");

            if (item.Desconto > item.Quantidade * item.Preco)
                erros.Add($"O desconto do item '{item.Descricao}' é maior que o total.");
        }

        foreach (var mo in MaosDeObra)
        {
            if (mo.Horas <= 0)
                erros.Add($"As horas do serviço '{mo.Descricao}' devem ser maiores que zero.");

            if (mo.Valor < 0)
                erros.Add($"O valor do serviço '{mo.Descricao}' não pode ser negativo.");

            if (mo.Desconto > mo.Horas * mo.Valor)
                erros.Add($"O desconto do serviço '{mo.Descricao}' é maior que o total.");
        }

        EditorMensagem = erros.FirstOrDefault() ?? string.Empty;
        return erros.Count == 0;
    }

    // ---------- Alterar status ----------

    private OrcamentoResumoDto? _orcamentoParaStatus;
    private int _novoStatusId;
    private string _statusObservacao = string.Empty;

    /// <summary>Status que podem ser aplicados a partir do status atual (regra do Domain).</summary>
    public IReadOnlyList<OrcamentoStatusDto> StatusDisponiveisParaSelecao
    {
        get
        {
            if (_orcamentoParaStatus is null)
                return StatusDisponiveis.ToList();

            return StatusDisponiveis
                .Where(s => OrcamentoStatus.PodeTransicionarPara(_orcamentoParaStatus.StatusCodigo, s.Codigo))
                .ToList();
        }
    }

    public int NovoStatusId
    {
        get => _novoStatusId;
        set => SetField(ref _novoStatusId, value);
    }

    public string StatusObservacao
    {
        get => _statusObservacao;
        set => SetField(ref _statusObservacao, value);
    }

    private bool _statusAberto;

    public bool StatusAberto
    {
        get => _statusAberto;
        private set => SetField(ref _statusAberto, value);
    }

    public string StatusMensagem => _orcamentoParaStatus is null
        ? string.Empty
        : $"Alterar o status do orçamento {_orcamentoParaStatus.Numero} de '{_orcamentoParaStatus.StatusNome}'.";

    private void AbrirAlteracaoStatus(OrcamentoResumoDto? orcamento)
    {
        if (orcamento == null)
            return;

        _orcamentoParaStatus = orcamento;
        _novoStatusId = 0;
        _statusObservacao = string.Empty;
        StatusAberto = true;
        OnPropertyChanged(nameof(StatusDisponiveisParaSelecao));
        OnPropertyChanged(nameof(StatusMensagem));
    }

    private void FecharStatus()
    {
        StatusAberto = false;
        _orcamentoParaStatus = null;
    }

    /// <summary>Fecha o diálogo de status sem alterar nada.</summary>
    public void CancelarAlteracaoStatus() => FecharStatus();

    private async Task ConfirmarAlteracaoStatusAsync()
    {
        if (_orcamentoParaStatus is null || NovoStatusId <= 0)
        {
            Report("Selecione o novo status.");
            return;
        }

        try
        {
            await _orcamentoService.AlterarStatusAsync(new AlterarStatusOrcamentoDto
            {
                OrcamentoId = _orcamentoParaStatus.Id,
                NovoStatusId = NovoStatusId,
                UsuarioId = UsuarioAtualId,
                ObservacaoMotivo = StatusObservacao
            });

            FecharStatus();
            await CarregarAsync();
            Report("Status do orçamento alterado.");
        }
        catch (Exception ex)
        {
            FecharStatus();
            Report(ex.Message);
        }
    }

    public string UltimaMensagemStatus { get; private set; } = string.Empty;

    public int StatusVersion { get; private set; }

    private void Report(string mensagem)
    {
        UltimaMensagemStatus = mensagem;
        StatusVersion++;
        OnPropertyChanged(nameof(UltimaMensagemStatus));
        OnPropertyChanged(nameof(StatusVersion));
        _reportStatus?.Invoke(mensagem);
    }
}

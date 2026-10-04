using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using Microsoft.Extensions.DependencyInjection;
using OrcPro.App.ViewModels;
using OrcPro.App.Views;
using OrcPro.Application.DTOs.Auth;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Common;
using OrcPro.Infrastructure.DependencyInjection;
using OrcPro.Infrastructure.Persistence.Providers;
using Xunit;

namespace OrcPro.Tests;

/// <summary>
/// Cobertura funcional do módulo de Peças / Itens sobre um banco SQLite real (schema criado
/// pelo <see cref="DatabaseInitializer"/>, exatamente o caminho usado pelo aplicativo).
/// Cobre: abrir o módulo, listar, novo, salvar, editar, visualizar, ativar/inativar,
/// excluir, pesquisar, filtrar, paginar, ordenar e reabrir o módulo após sair dele.
/// </summary>
[Collection(WpfTestSupport.Colecao)]
public sealed class PecasModuloTests : IDisposable
{
    private readonly ServiceProvider _provedor;
    private readonly IServiceScope _escopo;
    private readonly string _caminhoBanco;

    public PecasModuloTests()
    {
        _caminhoBanco = Path.Combine(Path.GetTempPath(), $"pecas-{Guid.NewGuid():N}.db");

        var services = new ServiceCollection();
        services.AddInfrastructure(DatabaseConnectionOptions.DefaultSqlite(_caminhoBanco));
        _provedor = services.BuildServiceProvider();

        // Mesmo caminho de inicialização do App: cria schema, aplica upgrades e permissões.
        DatabaseInitializer.InitializeAsync(_provedor).GetAwaiter().GetResult();

        _escopo = _provedor.CreateScope();
    }

    private IPecaService Servico => _escopo.ServiceProvider.GetRequiredService<IPecaService>();

    /// <summary>Sessão com todas as permissões do catálogo (equivalente ao perfil Administrador).</summary>
    private static UsuarioSessaoDto SessaoAdministrador => new()
    {
        Id = 1,
        Username = "admin",
        NomeCompleto = "Administrador",
        PerfilNome = PermissaoCatalogo.PerfilAdministrador,
        Permissoes = PermissaoCatalogo.Definicoes.Select(d => d.Codigo).ToList()
    };

    /// <summary>
    /// Cria o ViewModel na thread de UI — o <c>DispatcherTimer</c> da busca com debounce e o
    /// <c>CommandManager</c> dos comandos exigem um <c>Dispatcher</c>.
    /// </summary>
    private PecasViewModel CriarViewModel(UsuarioSessaoDto? sessao = null, Action<string>? status = null)
        => WpfTestSupport.RunOnStaThread<PecasViewModel>(
            () => new PecasViewModel(Servico, sessao ?? SessaoAdministrador, status ?? (_ => { })));

    private Task<PecasViewModel> CriarViewModelAsync(UsuarioSessaoDto? sessao = null, Action<string>? status = null)
        => Task.Run(() => CriarViewModel(sessao, status));

    private static OrcPro.Application.DTOs.Peca.CriarPecaDto Nova(
        string codigo, string descricao, bool ativo = true, string? marca = null, string? categoria = null)
        => new()
        {
            Codigo = codigo,
            Descricao = descricao,
            Categoria = categoria ?? "FERRAGENS",
            Marca = marca ?? "3M",
            Modelo = "M1",
            CodigoBarras = "789" + Math.Abs(codigo.GetHashCode()).ToString("000000000"),
            UnidadeMedida = "UN",
            PrecoCusto = 1.00m,
            PrecoVenda = 2.50m,
            EstoqueAtual = 10m,
            EstoqueMinimo = 2m,
            Ativo = ativo
        };

    /// <summary>Executa um comando assíncrono e aguarda a conclusão real da operação.</summary>
    private static void ExecutarComando(ICommand comando, object? parametro, Func<bool> concluido)
    {
        var podeExecutar = WpfTestSupport.RunOnStaThread(() => comando.CanExecute(parametro));
        Assert.True(podeExecutar, "O comando não está habilitado para esta operação.");

        WpfTestSupport.RunOnStaThreadUntil(() => comando.Execute(parametro), concluido);
    }

    /// <summary>
    /// Carrega o <see cref="DataTemplate"/> do módulo diretamente do dicionário do App.
    /// Não depende do <see cref="System.Windows.Application"/> já criado por outros testes
    /// (o Application é singleton por AppDomain e cada suíte monta seus próprios recursos).
    /// </summary>
    private static DataTemplate ObterDataTemplateDoModulo()
    {
        var dicionario = new ResourceDictionary
        {
            Source = new Uri("/OrcPro.App;component/Resources/DataTemplates.xaml", UriKind.Relative)
        };

        // Implicit DataTemplates são registrados sob DataTemplateKey; buscamos pelo DataType.
        return dicionario.Values.OfType<DataTemplate>()
            .First(t => Equals(t.DataType, typeof(PecasViewModel)));
    }

    /// <summary>Salva o formulário de Peças e aguarda o fim do comando.</summary>
    private static void Salvar(PecasViewModel vm)
        => ExecutarComando(vm.SalvarCommand, null, () => !vm.EditorAberto || vm.EditorMensagemVisivel);

    /// <summary>
    /// Executa o botão "Atualizar" e aguarda a conclusão real da recarga. A conclusão é
    /// detectada pelo PropertyChanged de <c>ResumoPaginacao</c>, emitido ao final de cada
    /// carregamento bem-sucedido da listagem.
    /// </summary>
    private static void Recarregar(PecasViewModel vm)
    {
        var recarregamentos = 0;

        void AoMudar(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PecasViewModel.ResumoPaginacao))
                recarregamentos++;
        }

        vm.PropertyChanged += AoMudar;
        try
        {
            ExecutarComando(vm.AtualizarListaCommand, null, () => recarregamentos > 0);
        }
        finally
        {
            vm.PropertyChanged -= AoMudar;
        }
    }

    // ============================ Abrir o módulo ============================

    [Fact]
    public async Task AbrirPecas_DeveCarregarSemExcecaoComBancoReal()
    {
        var vm = await CriarViewModelAsync();
        await vm.InitializeAsync();

        Assert.NotNull(vm);
        Assert.Equal(0, vm.TotalRegistros);
        Assert.Empty(vm.Pecas);
        Assert.Equal(1, vm.TotalPaginas);
        Assert.False(vm.TemRegistros);
    }

    [Fact]
    public void AbrirPecas_ViewDeveSerCriadaSemExcecao()
    {
        // Regressão: Thickness inválido no XAML (ex.: Margin="0,0,8", três valores) fazia o
        // construtor de PecasView lançar XamlParseException e encerrar o aplicativo.
        WpfTestSupport.RunOnStaThread(() => Assert.NotNull(new PecasView()));
    }

    [Fact]
    public void AbrirPecas_ViewDeveSerResolvidaPeloDataTemplateDoApp()
    {
        // Mesmo caminho do ContentControl do shell: DataTemplate do ViewModel -> View.
        WpfTestSupport.RunOnStaThread(() =>
            Assert.IsType<PecasView>(ObterDataTemplateDoModulo().LoadContent()));
    }

    [Fact]
    public void AbrirPecas_ViewDeveConterOsElementosDaTela()
    {
        WpfTestSupport.RunOnStaThread(() =>
        {
            var arvore = ObterArvoreLogica(new PecasView()).ToList();

            // Barra de ferramentas, grid e rodapé de paginação.
            foreach (var id in new[]
            {
                "PecasBusca", "PecasFiltroSituacao", "PecasLimparBusca", "PecasAtualizar",
                "PecasNovo", "PecasGrid", "PecasResumo"
            })
            {
                Assert.NotNull(arvore.OfType<FrameworkElement>()
                    .FirstOrDefault(e => AutomationProperties.GetAutomationId(e) == id));
            }

            var grid = Assert.Single(arvore.OfType<DataGrid>(),
                g => AutomationProperties.GetAutomationId(g) == "PecasGrid");
            Assert.False(grid.AutoGenerateColumns);

            // Ordenação pelo cabeçalho: as colunas ordenáveis expõem SortMemberPath.
            var colunas = grid.Columns.Cast<DataGridColumn>().ToList();
            Assert.Contains(colunas, c => c.SortMemberPath == "Codigo");
            Assert.Contains(colunas, c => c.SortMemberPath == "Descricao");
            Assert.Contains(colunas, c => c.SortMemberPath == "Categoria");
            Assert.Contains(colunas, c => c.SortMemberPath == "Marca");

            // Coluna de ações (visualizar, editar, ativar/inativar, excluir): o CellTemplate
            // só é instanciado quando há linhas, então validamos a coluna e os comandos.
            var acoes = Assert.IsType<DataGridTemplateColumn>(
                Assert.Single(colunas, c => c.Header?.ToString() == "AÇÕES"));
            Assert.NotNull(acoes.CellTemplate);

            // Code-behind registra ordenação por cabeçalho e duplo clique para editar.
            var metodos = typeof(PecasView)
                .GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Select(m => m.Name)
                .ToList();
            Assert.Contains("OnGridPecasSorting", metodos);
            Assert.Contains("OnPecasGridDoubleClick", metodos);
        });
    }

    [Fact]
    public void NenhumXaml_DeveUsarThicknessComTresValores()
    {
        // ThicknessConverter aceita 1, 2 ou 4 valores; três valores lança FormatException em
        // tempo de execução e derrubava o aplicativo ao abrir a tela. Guardamos o padrão.
        var pastaViews = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "OrcPro.App"));
        Assert.True(Directory.Exists(pastaViews), $"Pasta de XAML não encontrada: {pastaViews}");

        var invalidos = new List<string>();

        foreach (var arquivo in Directory.EnumerateFiles(pastaViews, "*.xaml", SearchOption.AllDirectories))
        {
            var linhas = File.ReadAllLines(arquivo);

            for (int i = 0; i < linhas.Length; i++)
            {
                foreach (System.Text.RegularExpressions.Match valor in
                    System.Text.RegularExpressions.Regex.Matches(linhas[i], @"(?:Margin|Padding|BorderThickness)=""([^""]*)"""))
                {
                    var partes = valor.Groups[1].Value.Split(',');
                    if (partes.Length is not (1 or 2 or 4))
                    {
                        invalidos.Add($"{Path.GetFileName(arquivo)}:{i + 1} -> \"{valor.Groups[1].Value}\"");
                    }
                }
            }
        }

        Assert.True(invalidos.Count == 0,
            "Thickness inválido (3 valores) encontrado: " + string.Join("; ", invalidos));
    }

    // ============================ Listar / novo / salvar ============================

    [Fact]
    public async Task Listar_DeveRetornarRegistrosDaBase()
    {
        await Servico.CriarAsync(Nova("PEC-001", "PARAFUSO"));
        await Servico.CriarAsync(Nova("PEC-002", "PORCA"));

        var vm = await CriarViewModelAsync();
        await vm.InitializeAsync();

        Assert.Equal(2, vm.TotalRegistros);
        Assert.Equal(2, vm.Pecas.Count);
        Assert.True(vm.TemRegistros);
        Assert.Equal(1, vm.TotalPaginas); // 2 registros cabem na página de 20
        Assert.Contains("2 registros", vm.ResumoPaginacao);
    }

    [Fact]
    public async Task AbrirNovoESalvar_DeveCadastrarComCamposEmMaiusculo()
    {
        var vm = await CriarViewModelAsync();

        vm.NovoCommand.Execute(null);
        Assert.True(vm.EditorAberto);
        Assert.True(vm.EditorNovo);
        Assert.Equal("Nova peça", vm.EditorTitulo);

        vm.FormCodigo = "pec-900";
        vm.FormDescricao = "chave de fenda";
        vm.FormCategoria = "ferragens";
        vm.FormMarca = "tramontina";
        vm.FormUnidadeMedida = "pc";
        vm.FormPrecoVendaTexto = "12,34";
        vm.FormEstoqueAtualTexto = "5";
        vm.FormEstoqueMinimoTexto = "1";

        Salvar(vm);

        Assert.False(vm.EditorAberto);

        var dto = Assert.Single(await Servico.ListarTodasAtivasAsync());
        Assert.Equal("PEC-900", dto.Codigo);
        Assert.Equal("CHAVE DE FENDA", dto.Descricao);
        Assert.Equal("FERRAGENS", dto.Categoria);
        Assert.Equal("TRAMONTINA", dto.Marca);
        Assert.Equal("PC", dto.UnidadeMedida);
        Assert.Equal(12.34m, dto.PrecoVenda);
        Assert.Equal(5m, dto.EstoqueAtual);
        Assert.Equal(1m, dto.EstoqueMinimo);
    }

    [Fact]
    public async Task Salvar_SemCodigo_DeveExibirMensagemDeValidacaoGlobal()
    {
        var vm = await CriarViewModelAsync();

        vm.NovoCommand.Execute(null);
        vm.FormCodigo = string.Empty;
        vm.FormDescricao = "SEM CODIGO";

        Salvar(vm);

        Assert.True(vm.EditorAberto); // o editor permanece aberto
        Assert.True(vm.EditorMensagemVisivel);
        Assert.NotEmpty(vm.EditorMensagem);
        Assert.Empty(await Servico.ListarTodasAtivasAsync());
    }

    [Fact]
    public async Task Salvar_CodigoDuplicado_DeveExibirMensagemDeErro()
    {
        await Servico.CriarAsync(Nova("PEC-001", "PARAFUSO"));

        var vm = await CriarViewModelAsync();
        vm.NovoCommand.Execute(null);
        vm.FormCodigo = "PEC-001";
        vm.FormDescricao = "OUTRA PECA";

        Salvar(vm);

        Assert.True(vm.EditorAberto);
        Assert.True(vm.EditorMensagemVisivel);
        Assert.Contains("PEC-001", vm.EditorMensagem);
    }

    // ============================ Editar / visualizar ============================

    [Fact]
    public async Task Editar_DeveCarregarFormularioESalvarAlteracoes()
    {
        await Servico.CriarAsync(Nova("PEC-010", "BROCA"));
        var vm = await CriarViewModelAsync();
        await vm.InitializeAsync();

        vm.PecaSelecionada = vm.Pecas.Single();
        vm.EditarCommand.Execute(vm.PecaSelecionada);

        Assert.True(vm.EditorAberto);
        Assert.False(vm.EditorNovo);
        Assert.Equal("Editar peça", vm.EditorTitulo);
        Assert.Equal("PEC-010", vm.FormCodigo);
        Assert.Equal("BROCA", vm.FormDescricao);

        vm.FormDescricao = "broca de impacto";
        vm.FormPrecoVendaTexto = "199,90";
        Salvar(vm);

        Assert.False(vm.EditorAberto);
        Assert.Equal("BROCA DE IMPACTO", vm.Pecas.Single().Descricao);
        Assert.Equal(199.90m, vm.Pecas.Single().PrecoVenda);
    }

    [Fact]
    public async Task Visualizar_DeveAbrirFichaEmModoLeitura()
    {
        await Servico.CriarAsync(Nova("PEC-020", "REBOLTADE", marca: "Bosch"));
        var vm = await CriarViewModelAsync();
        await vm.InitializeAsync();

        vm.VisualizarCommand.Execute(vm.Pecas.Single());

        Assert.True(vm.VisualizacaoAberta);
        Assert.False(vm.EditorAberto); // somente leitura: não abre o editor
        Assert.Equal("PEC-020", vm.FormCodigo);
        Assert.Equal("BOSCH", vm.FormMarca);

        vm.FecharVisualizacaoCommand.Execute(null);
        Assert.False(vm.VisualizacaoAberta);
    }

    // ============================ Ativar / inativar ============================

    [Fact]
    public async Task AtivarInativar_DeveAlternarSituacaoDaPeca()
    {
        await Servico.CriarAsync(Nova("PEC-030", "ARRUELA"));
        var vm = await CriarViewModelAsync();
        await vm.InitializeAsync();

        ExecutarComando(vm.AlternarSituacaoCommand, vm.Pecas.Single(),
            () => vm.Pecas.All(p => !p.Ativo));
        Assert.False(vm.Pecas.Single().Ativo);

        ExecutarComando(vm.AlternarSituacaoCommand, vm.Pecas.Single(),
            () => vm.Pecas.All(p => p.Ativo));
        Assert.True(vm.Pecas.Single().Ativo);

        Assert.Equal(1, vm.TotalRegistros); // inativar não exclui
    }

    // ============================ Excluir ============================

    [Fact]
    public async Task Excluir_ComPermissao_DeveRemoverRegistroAposConfirmacao()
    {
        await Servico.CriarAsync(Nova("PEC-040", "ARRUELA"));
        var vm = await CriarViewModelAsync();
        await vm.InitializeAsync();

        vm.ExcluirCommand.Execute(vm.Pecas.Single());
        Assert.True(vm.ConfirmacaoAberta);
        Assert.Contains("PEC-040", vm.ConfirmacaoMensagem);

        ExecutarComando(vm.ConfirmarExclusaoCommand, null, () => !vm.ConfirmacaoAberta);

        Assert.False(vm.ConfirmacaoAberta);
        Assert.Equal(0, vm.TotalRegistros);
        Assert.Empty(vm.Pecas);
    }

    [Fact]
    public async Task Excluir_Cancelamento_DeveManterRegistro()
    {
        await Servico.CriarAsync(Nova("PEC-041", "PARAFUSO M8"));
        var vm = await CriarViewModelAsync();
        await vm.InitializeAsync();

        vm.ExcluirCommand.Execute(vm.Pecas.Single());
        vm.CancelarExclusaoCommand.Execute(null);

        Assert.False(vm.ConfirmacaoAberta);
        Assert.Equal(1, vm.TotalRegistros);
    }

    [Fact]
    public async Task Excluir_SemPermissao_DeveSerBloqueadoPeloCanExecute()
    {
        await Servico.CriarAsync(Nova("PEC-042", "VEDA ROSCA"));

        var semExcluir = new UsuarioSessaoDto
        {
            Id = 2,
            Username = "operador",
            PerfilNome = "Operador",
            Permissoes = PermissaoCatalogo.CodigosDoModulo(PermissaoCatalogo.ModuloPecas)
                .Where(c => c != PermissaoCatalogo.Codigos.Pecas.Excluir)
                .ToList()
        };

        var vm = await CriarViewModelAsync(semExcluir);
        await vm.InitializeAsync();

        Assert.False(vm.PodeExcluir);

        // Sem PECAS.EXCLUIR o botão fica desabilitado (o WPF não executa comandos
        // cujo CanExecute é falso), então a confirmação nunca chega a abrir.
        Assert.False(vm.ExcluirCommand.CanExecute(vm.Pecas.Single()));
        Assert.False(vm.ConfirmacaoAberta);
        Assert.Equal(1, vm.TotalRegistros);
        Assert.Single(vm.Pecas);
    }

    // ============================ Pesquisar / filtrar / ordenar ============================

    [Theory]
    [InlineData("PEC-001")]   // por código
    [InlineData("parafuso")]   // por descrição
    [InlineData("FERRAGENS")]  // por categoria
    [InlineData("3m")]         // por marca
    public async Task Pesquisar_DeveFiltrarPorCodigoDescricaoCategoriaOuMarca(string termo)
    {
        await Servico.CriarAsync(Nova("PEC-001", "PARAFUSO", marca: "3M", categoria: "FERRAGENS"));
        await Servico.CriarAsync(Nova("PEC-002", "PORCA", marca: "BOSCH", categoria: "ELEMENTOS"));

        var vm = await CriarViewModelAsync();
        await vm.InitializeAsync();

        vm.Busca = termo;
        await vm.CarregarAsync();

        Assert.Equal(1, vm.TotalRegistros);
        Assert.Equal("PEC-001", vm.Pecas.Single().Codigo);
        Assert.Equal(1, vm.Pagina);
    }

    [Fact]
    public async Task Pesquisar_SemResultado_DeveExibirEstadoVazio()
    {
        await Servico.CriarAsync(Nova("PEC-001", "PARAFUSO"));

        var vm = await CriarViewModelAsync();
        await vm.InitializeAsync();

        vm.Busca = "xyz-inexistente";
        await vm.CarregarAsync();

        Assert.Equal(0, vm.TotalRegistros);
        Assert.False(vm.TemRegistros);

        vm.LimparBuscaCommand.Execute(null);
        await vm.CarregarAsync();
        Assert.Equal(1, vm.TotalRegistros);
    }

    [Fact]
    public async Task Filtrar_Situacao_DeveAplicarSomenteAtivosEInativos()
    {
        await Servico.CriarAsync(Nova("PEC-001", "PARAFUSO", ativo: true));
        await Servico.CriarAsync(Nova("PEC-002", "PORCA", ativo: false));

        var vm = await CriarViewModelAsync();
        await vm.InitializeAsync();
        Assert.Equal(2, vm.TotalRegistros);

        vm.SituacaoFiltro = 1; // somente ativos
        await vm.CarregarAsync();
        Assert.Equal(1, vm.TotalRegistros);
        Assert.True(vm.Pecas.Single().Ativo);

        vm.SituacaoFiltro = 2; // somente inativos
        await vm.CarregarAsync();
        Assert.Equal(1, vm.TotalRegistros);
        Assert.False(vm.Pecas.Single().Ativo);

        vm.SituacaoFiltro = 0; // todos
        await vm.CarregarAsync();
        Assert.Equal(2, vm.TotalRegistros);
    }

    [Fact]
    public async Task Paginar_DeveNavegarEntrePaginasRespeitandoOTotal()
    {
        for (int i = 1; i <= 25; i++)
            await Servico.CriarAsync(Nova($"PEC-{i:D3}", $"PECA {i:D3}"));

        var vm = await CriarViewModelAsync();
        await vm.InitializeAsync();

        Assert.Equal(25, vm.TotalRegistros);
        Assert.Equal(2, vm.TotalPaginas);
        Assert.Equal(1, vm.Pagina);
        Assert.False(vm.PodePaginaAnterior);
        Assert.True(vm.PodePaginaProxima);
        Assert.Equal(20, vm.Pecas.Count); // PageSize do módulo

        ExecutarComando(vm.PaginaProximaCommand, null, () => vm.Pagina == 2);
        Assert.Equal(2, vm.Pagina);
        Assert.Equal(5, vm.Pecas.Count);
        Assert.True(vm.PodePaginaAnterior);
        Assert.False(vm.PodePaginaProxima);
        Assert.Contains("Página 2 de 2", vm.ResumoPaginacao);

        ExecutarComando(vm.PaginaAnteriorCommand, null, () => vm.Pagina == 1);
        Assert.Equal(1, vm.Pagina);
        Assert.Equal(20, vm.Pecas.Count);
    }

    [Fact]
    public async Task Ordenar_DeveAlternarAscendenteEDescendente()
    {
        await Servico.CriarAsync(Nova("PEC-003", "PARAFUSO"));
        await Servico.CriarAsync(Nova("PEC-001", "ARRUELA"));
        await Servico.CriarAsync(Nova("PEC-002", "PORCA"));

        var vm = await CriarViewModelAsync();
        await vm.InitializeAsync();

        await vm.AplicarOrdenacaoAsync("Codigo");
        Assert.Equal(new[] { "PEC-001", "PEC-002", "PEC-003" }, vm.Pecas.Select(p => p.Codigo).ToArray());

        await vm.AplicarOrdenacaoAsync("Codigo"); // segundo clique inverte
        Assert.Equal(new[] { "PEC-003", "PEC-002", "PEC-001" }, vm.Pecas.Select(p => p.Codigo).ToArray());

        await vm.AplicarOrdenacaoAsync("Descricao");
        Assert.Equal("ARRUELA", vm.Pecas[0].Descricao);
        Assert.Equal("PEC-001", vm.Pecas[0].Codigo);
    }

    // ============================ Permissões ============================

    [Fact]
    public async Task Permissoes_Administrador_DeveLiberarTodasAsAcoes()
    {
        var vm = await CriarViewModelAsync();
        await vm.InitializeAsync();

        Assert.True(vm.PodeVisualizar);
        Assert.True(vm.PodeCriar);
        Assert.True(vm.PodeEditar);
        Assert.True(vm.PodeExcluir);
        Assert.True(vm.PodeAtivarInativar);
        Assert.True(vm.NovoCommand.CanExecute(null));
        Assert.True(vm.EditarCommand.CanExecute(vm.Pecas.FirstOrDefault()));
        Assert.True(vm.VisualizarCommand.CanExecute(vm.Pecas.FirstOrDefault()));
    }

    [Fact]
    public async Task Permissoes_SomenteVisualizar_DeveDesabilitarAcoes()
    {
        var somenteVisualizar = new UsuarioSessaoDto
        {
            Id = 3,
            Username = "leitor",
            PerfilNome = "Leitor",
            Permissoes = new List<string> { PermissaoCatalogo.Codigos.Pecas.Visualizar }
        };

        var restrito = await CriarViewModelAsync(somenteVisualizar);
        await restrito.InitializeAsync();

        Assert.True(restrito.PodeVisualizar);
        Assert.False(restrito.PodeCriar);
        Assert.False(restrito.PodeEditar);
        Assert.False(restrito.PodeExcluir);
        Assert.False(restrito.PodeAtivarInativar);
        Assert.False(restrito.NovoCommand.CanExecute(null));
        Assert.False(restrito.EditarCommand.CanExecute(null));
        Assert.False(restrito.AlternarSituacaoCommand.CanExecute(null));
    }

    // ============================ Reabrir o módulo ============================

    [Fact]
    public async Task ReabrirModulo_DeveFuncionarAposSairDele()
    {
        var vm = await CriarViewModelAsync();
        await vm.InitializeAsync();

        vm.NovoCommand.Execute(null);
        vm.FormCodigo = "PEC-050";
        vm.FormDescricao = "RESMA";
        Salvar(vm);

        var primeiraAbertura = vm.Pecas.Single();

        vm.NovoCommand.Execute(null);
        Assert.True(vm.EditorAberto);

        // Simula sair do módulo (voltar ao Dashboard) e abrir Peças novamente.
        var reaberto = await CriarViewModelAsync();
        await reaberto.InitializeAsync();

        Assert.Single(reaberto.Pecas);
        Assert.Equal(primeiraAbertura.Codigo, reaberto.Pecas.Single().Codigo);
        Assert.False(reaberto.EditorAberto);

        // Reabrir várias vezes seguidas continua estável.
        for (int i = 0; i < 3; i++)
        {
            var instancia = await CriarViewModelAsync();
            await instancia.InitializeAsync();
            Assert.Single(instancia.Pecas);
        }
    }

    [Fact]
    public async Task ReabrirModulo_AposExclusao_DeveMostrarListaVaziaSemExcecao()
    {
        await Servico.CriarAsync(Nova("PEC-060", "EIXO"));
        var vm = await CriarViewModelAsync();
        await vm.InitializeAsync();

        vm.ExcluirCommand.Execute(vm.Pecas.Single());
        ExecutarComando(vm.ConfirmarExclusaoCommand, null, () => !vm.ConfirmacaoAberta);

        var reaberto = await CriarViewModelAsync();
        await reaberto.InitializeAsync();

        Assert.Empty(reaberto.Pecas);
        Assert.Equal(0, reaberto.TotalRegistros);
        Assert.False(reaberto.TemRegistros);
    }

    [Fact]
    public async Task RecarregarLista_AtualizarCommand_DeveReprocessarSemErro()
    {
        await Servico.CriarAsync(Nova("PEC-070", "CATRACA"));

        var status = new List<string>();
        var vm = await CriarViewModelAsync(status: status.Add);
        await vm.InitializeAsync();

        Recarregar(vm);

        Assert.Single(vm.Pecas);
        Assert.DoesNotContain(status, s => s.Contains("Falha", StringComparison.OrdinalIgnoreCase));
    }

    // ==================== Navegação pelo Ribbon (MainWindow) ====================

    [Fact]
    public async Task AbrirPecasPeloRibbon_DeveSelecionarOModuloSemExcecao()
    {
        // Reproduz o caminho real do botão: OpenPecasCommand -> OpenPecas -> Navigate.
        // Inclui a criação da View pelo DataTemplate do ContentControl do shell.
        var dbPath = Path.Combine(Path.GetTempPath(), $"pecas-nav-{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddInfrastructure(DatabaseConnectionOptions.DefaultSqlite(dbPath));
        var provedor = services.BuildServiceProvider();

        try
        {
            await DatabaseInitializer.InitializeAsync(provedor);

            WpfTestSupport.RunOnStaThread(() =>
            {
                var shell = new MainWindowViewModel(SessaoAdministrador, provedor);
                Assert.IsType<DashboardViewModel>(shell.CurrentView);

                shell.OpenPecasCommand.Execute(null);

                Assert.IsType<PecasViewModel>(shell.CurrentView);
                Assert.Equal("Peças / Itens", shell.ModuleTitle);

                // A View do módulo é instanciada pelo mesmo DataTemplate do shell.
                Assert.IsType<PecasView>(ObterDataTemplateDoModulo().LoadContent());
            });
        }
        finally
        {
            provedor.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            try { if (File.Exists(dbPath)) File.Delete(dbPath); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task AbrirPecasSemPermissao_DeveBloquearComMensagemNoRodape()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"pecas-nav2-{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddInfrastructure(DatabaseConnectionOptions.DefaultSqlite(dbPath));
        var provedor = services.BuildServiceProvider();

        try
        {
            await DatabaseInitializer.InitializeAsync(provedor);

            var semAcesso = new UsuarioSessaoDto
            {
                Id = 4,
                Username = "sem-pecas",
                PerfilNome = "Básico",
                Permissoes = new List<string> { PermissaoCatalogo.Codigos.Clientes.Visualizar }
            };

            WpfTestSupport.RunOnStaThread(() =>
            {
                var shell = new MainWindowViewModel(semAcesso, provedor);

                Assert.False(shell.PodeAcessarPecas);

                shell.OpenPecasCommand.Execute(null);

                Assert.IsType<DashboardViewModel>(shell.CurrentView); // módulo não abre
                Assert.Contains("PECAS.VISUALIZAR", shell.StatusMessage);
            });
        }
        finally
        {
            provedor.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            try { if (File.Exists(dbPath)) File.Delete(dbPath); } catch (IOException) { }
        }
    }

// ======================= Geração automática de código =======================

    [Fact]
    public async Task Codigo_AoAbrirNovo_DeveSerGeradoAutomaticamente()
    {
        var vm = await CriarViewModelAsync();

        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);

        Assert.True(vm.EditorAberto);
        Assert.True(vm.EditorNovo);
        Assert.Equal("PEC-0001", vm.FormCodigo);
    }

    [Fact]
    public void Codigo_DeveSerSomenteLeituraNaView()
    {
        WpfTestSupport.RunOnStaThread(() =>
        {
            var arvore = ObterArvoreLogica(new PecasView()).ToList();

            var codigo = arvore.OfType<TextBox>()
                .FirstOrDefault(e => AutomationProperties.GetAutomationId(e) == "PecaCodigoInput");
            Assert.NotNull(codigo);

            Assert.True(codigo!.IsReadOnly, "O código é gerado pelo sistema e não pode ser digitado.");

            var binding = codigo.GetBindingExpression(TextBox.TextProperty)?.ParentBinding;
            Assert.NotEqual(BindingMode.TwoWay, binding?.Mode);
        });
    }

    [Fact]
    public async Task Codigo_DeveSequenciarAposExclusaoEInativacao()
    {
        await Servico.CriarAsync(Nova("PEC-0001", "PARAFUSO"));
        await Servico.CriarAsync(Nova("PEC-0002", "PORCA"));
        await Servico.CriarAsync(Nova("PEC-0003", "ARRUELA"));

        var vm = await CriarViewModelAsync();
        await vm.InitializeAsync();

        // Exclui um código INTERMEDIÁRIO: a sequência não pode voltar a usá-lo.
        vm.ExcluirCommand.Execute(vm.Pecas.First(p => p.Codigo == "PEC-0002"));
        ExecutarComando(vm.ConfirmarExclusaoCommand, null, () => !vm.ConfirmacaoAberta);

        // Inativa o maior código (PEC-0003): inativos continuam ocupando número.
        ExecutarComando(vm.AlternarSituacaoCommand, vm.Pecas.First(p => p.Codigo == "PEC-0003"),
            () => vm.Pecas.Any(p => p.Codigo == "PEC-0003" && !p.Ativo));

        // Novo cadastro avança a partir do maior código existente.
        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);
        Assert.Equal("PEC-0004", vm.FormCodigo);
    }

    [Fact]
    public async Task Codigo_NuncaDeveReutilizarCodigoExistente()
    {
        // Gera e salva várias peças; nenhum código pode se repetir nem colidir.
        var usados = new HashSet<string>();

        for (int i = 1; i <= 5; i++)
        {
            var vm = await CriarViewModelAsync();
            await vm.InitializeAsync();

            ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);

            Assert.True(usados.Add(vm.FormCodigo), $"Código repetido: {vm.FormCodigo}");

            vm.FormDescricao = $"PECA {i}";
            Salvar(vm);
        }

        Assert.Equal(5, usados.Count);
        Assert.Contains("PEC-0001", usados);
        Assert.Contains("PEC-0005", usados);
    }

    [Fact]
    public async Task Codigo_EditarNaoAlteraOCodigo()
    {
        await Servico.CriarAsync(Nova("PEC-0001", "PARAFUSO"));
        var vm = await CriarViewModelAsync();
        await vm.InitializeAsync();

        vm.EditarCommand.Execute(vm.Pecas.Single());
        Assert.Equal("PEC-0001", vm.FormCodigo);

        vm.FormDescricao = "PARAFUSO EDITADO";
        vm.FormPrecoVendaTexto = "9,99";
        Salvar(vm);

        Assert.Equal("PEC-0001", vm.Pecas.Single().Codigo);
        Assert.Equal("PARAFUSO EDITADO", vm.Pecas.Single().Descricao);
    }

    [Fact]
    public async Task Codigo_DeveConsiderarMaiorNumeroExistente()
    {
        // Lacuna proposital: com "quantidade + 1" o próximo seria PEC-0003, reaproveitando
        // PEC-0002 (já excluída). O correto é avançar a partir do maior código, PEC-0005.
        var repo = _escopo.ServiceProvider
            .GetRequiredService<OrcPro.Application.Interfaces.Repositories.IPecaRepository>();

        await Servico.CriarAsync(Nova("PEC-0001", "A"));
        await Servico.CriarAsync(Nova("PEC-0003", "C"));
        await Servico.CriarAsync(Nova("PEC-0005", "E"));

        var primeira = (await Servico.ListarTodasAtivasAsync()).First(p => p.Codigo == "PEC-0001");
        await Servico.ExcluirAsync(primeira.Id);

        Assert.Equal("PEC-0006", await repo.GerarProximoCodigoAsync());
    }

    [Fact]
    public async Task Codigo_DeveIgnorarCodigosNaoNumericos()
    {
        await Servico.CriarAsync(Nova("PEC-0007", "A"));
        await Servico.CriarAsync(Nova("MANUAL-XYZ", "B"));

        Assert.Equal("PEC-0008", await Servico.GerarProximoCodigoAsync());
    }

    [Fact]
    public async Task Codigo_DevePermanecerEstavelAoFecharEAbrirOModulo()
    {
        await Servico.CriarAsync(Nova("PEC-0001", "A"));

        // Simula fechar e abrir o módulo: nova instância do ViewModel, mesma base.
        for (int i = 0; i < 3; i++)
        {
            var instancia = await CriarViewModelAsync();
            await instancia.InitializeAsync();

            ExecutarComando(instancia.NovoCommand, null, () => instancia.EditorAberto);
            Assert.Equal("PEC-0002", instancia.FormCodigo);

            instancia.CancelarEditorCommand.Execute(null);
        }
    }
// ======================= MAIÚSCULO =======================

    [Fact]
    public async Task Maiusculo_TodosOsCamposTextuais_SaoNormalizadosAoSalvar()
    {
        var vm = await CriarViewModelAsync();

        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);

        vm.FormDescricao = "parafuso philips";
        vm.FormCategoria = "ferragens";
        vm.FormMarca = "tramontina";
        vm.FormModelo = "philips x";
        vm.FormCodigoBarras = "7891234567890";
        vm.FormUnidadeMedida = "pc";
        vm.FormObservacoes = "uso geral";

        Salvar(vm);

        var dto = Assert.Single(await Servico.ListarTodasAtivasAsync());
        Assert.Equal("PARAFUSO PHILIPS", dto.Descricao);
        Assert.Equal("FERRAGENS", dto.Categoria);
        Assert.Equal("TRAMONTINA", dto.Marca);
        Assert.Equal("PHILIPS X", dto.Modelo);
        Assert.Equal("7891234567890", dto.CodigoBarras);
        Assert.Equal("PC", dto.UnidadeMedida);
        Assert.Equal("USO GERAL", dto.Observacoes);
    }

    [Fact]
    public void Maiusculo_ComportamentoGlobal_ConverteDuranteDigitacao()
    {
        // Usa a infraestrutura global (TextFormattingBehavior), como as demais telas.
        WpfTestSupport.RunOnStaThread(() =>
        {
            var box = new TextBox();
            OrcPro.App.Behaviors.TextFormattingBehavior.SetFormatMode(
                box, OrcPro.App.Behaviors.TextFormattingBehavior.FormatMode.UpperCase);

            box.Text = "parafuso philips";
            Assert.Equal("PARAFUSO PHILIPS", box.Text);

            box.Text = string.Empty;
            box.Text = "b osch";
            Assert.Equal("B OSCH", box.Text);
        });
    }

    [Fact]
    public void Maiusculo_CamposDaView_EstaoConfiguradosParaMaiusculo()
    {
        WpfTestSupport.RunOnStaThread(() =>
        {
            var arvore = ObterArvoreLogica(new PecasView()).ToList();

            foreach (var id in new[]
            {
                "PecaDescricaoInput", "PecaCategoriaInput", "PecaMarcaInput",
                "PecaModeloInput", "PecaCodigoBarrasInput", "PecaObservacoesInput"
            })
            {
                var campo = arvore.OfType<TextBox>()
                    .FirstOrDefault(e => AutomationProperties.GetAutomationId(e) == id);
                Assert.NotNull(campo);

                Assert.Equal(OrcPro.App.Behaviors.TextFormattingBehavior.FormatMode.UpperCase,
                    OrcPro.App.Behaviors.TextFormattingBehavior.GetFormatMode(campo!));
            }
        });
    }
[Fact]
    public async Task Valores_ApagarSubstituirEcolar()
    {
        var vm = await CriarViewModelAsync();
        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);

        vm.FormDescricao = "PECA";

        vm.FormPrecoVendaTexto = "999";
        Assert.Equal(999m, vm.PrecoVendaValor);

        vm.FormPrecoVendaTexto = string.Empty;          // apagar
        Assert.Equal(0m, vm.PrecoVendaValor);

        vm.FormPrecoVendaTexto = "1250,75";            // colar / substituir
        Assert.Equal(1250.75m, vm.PrecoVendaValor);

        Salvar(vm);
        Assert.Equal(1250.75m, vm.Pecas.Single().PrecoVenda);
    }

    [Fact]
    public async Task Valores_ComSalvarEVerificarPersistenciaAposReabrir()
    {
        var vm = await CriarViewModelAsync();
        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);

        vm.FormDescricao = "PECA DE TESTE";
        vm.FormPrecoCustoTexto = "1250,75";
        vm.FormPrecoVendaTexto = "10,50";
        vm.FormEstoqueAtualTexto = "1000";
        vm.FormEstoqueMinimoTexto = "1,5";

        Salvar(vm);

        // Reabre o módulo (nova instância) e edita: valores devem voltar corretos.
        var reaberto = await CriarViewModelAsync();
        await reaberto.InitializeAsync();

        reaberto.EditarCommand.Execute(reaberto.Pecas.Single());

        // O texto volta pronto para edição e representa exatamente o valor gravado.
        Assert.Equal("1250,75", reaberto.FormPrecoCustoTexto);
        Assert.Equal(1250.75m, reaberto.PrecoCustoValor);
        Assert.Equal(10.50m, reaberto.PrecoVendaValor);
        Assert.Equal(1000m, reaberto.EstoqueAtualValor);
        Assert.Equal(1.5m, reaberto.EstoqueMinimoValor);

        // Os campos de texto continuam preenchidos para nova edição.
        Assert.Equal("1000", reaberto.FormEstoqueAtualTexto);
        Assert.Equal("1,5", reaberto.FormEstoqueMinimoTexto);

        // A ficha de visualização mantém os decimais.
        Assert.Equal(1250.75m, reaberto.FormPrecoCusto);
        Assert.Equal(10.50m, reaberto.FormPrecoVenda);
    }

    [Fact]
    public async Task Valores_Invalidos_DeveExibirMensagemENaoSalvar()
    {
        var vm = await CriarViewModelAsync();
        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);

        vm.FormDescricao = "PECA";
        vm.FormPrecoVendaTexto = "abc";

        Salvar(vm);

        Assert.True(vm.EditorAberto);
        Assert.True(vm.EditorMensagemVisivel);
        Assert.Empty(vm.Pecas);
    }

    [Fact]
    public async Task Valores_Negativos_DeveSerRejeitados()
    {
        var vm = await CriarViewModelAsync();
        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);

        vm.FormDescricao = "PECA";
        vm.FormPrecoVendaTexto = "-5";

        Salvar(vm);

        Assert.True(vm.EditorAberto);
        Assert.Empty(vm.Pecas);
    }
// ======================= Entrada numérica =======================

    [Theory]
    [InlineData("1", 1.0)]
    [InlineData("10", 10.0)]
    [InlineData("10,50", 10.50)]
    [InlineData("1000", 1000.0)]
    [InlineData("1250,75", 1250.75)]
    public async Task Valores_Custo_DeveConverterSemAlterarODigitado(string digitado, double esperado)
    {
        var vm = await CriarViewModelAsync();
        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);

        vm.FormDescricao = "PECA";
        vm.FormPrecoCustoTexto = digitado;

        // O texto permanece exatamente como digitado.
        Assert.Equal(digitado, vm.FormPrecoCustoTexto);
        Assert.Equal((decimal)esperado, vm.PrecoCustoValor);

        Salvar(vm);

        Assert.Equal((decimal)esperado, vm.Pecas.Single().PrecoCusto);
    }

    [Theory]
    [InlineData("1", 1.0)]
    [InlineData("10", 10.0)]
    [InlineData("10,50", 10.50)]
    [InlineData("1000", 1000.0)]
    [InlineData("1250,75", 1250.75)]
    public async Task Valores_Venda_DeveConverterSemAlterarODigitado(string digitado, double esperado)
    {
        var vm = await CriarViewModelAsync();
        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);

        vm.FormDescricao = "PECA";
        vm.FormPrecoVendaTexto = digitado;

        Assert.Equal(digitado, vm.FormPrecoVendaTexto);
        Assert.Equal((decimal)esperado, vm.PrecoVendaValor);

        Salvar(vm);

        Assert.Equal((decimal)esperado, vm.Pecas.Single().PrecoVenda);
    }

    [Theory]
    [InlineData("1", 1.0)]
    [InlineData("10", 10.0)]
    [InlineData("100", 100.0)]
    [InlineData("1000", 1000.0)]
    [InlineData("1,5", 1.5)]
    [InlineData("10,50", 10.50)]
    public async Task Valores_Quantidade_DeveConverterSemAlterarODigitado(string digitado, double esperado)
    {
        var vm = await CriarViewModelAsync();
        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);

        vm.FormDescricao = "PECA";
        vm.FormEstoqueAtualTexto = digitado;
        vm.FormEstoqueMinimoTexto = digitado;

        Assert.Equal(digitado, vm.FormEstoqueAtualTexto);
        Assert.Equal((decimal)esperado, vm.EstoqueAtualValor);
        Assert.Equal((decimal)esperado, vm.EstoqueMinimoValor);

        Salvar(vm);

        Assert.Equal((decimal)esperado, vm.Pecas.Single().EstoqueAtual);
        Assert.Equal((decimal)esperado, vm.Pecas.Single().EstoqueMinimo);
    }

    /// <summary>Regressão direta do sintoma reportado: digitar "1" não pode virar "1000".</summary>
    [Fact]
    public async Task Regressao_DigitarUm_NaoPodeVirarMil()
    {
        var vm = await CriarViewModelAsync();
        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);

        vm.FormDescricao = "PECA";
        vm.FormPrecoCustoTexto = "1";
        Assert.Equal("1", vm.FormPrecoCustoTexto);
        Assert.Equal(1m, vm.PrecoCustoValor);

        vm.FormPrecoVendaTexto = "1";
        Assert.Equal("1", vm.FormPrecoVendaTexto);
        Assert.Equal(1m, vm.PrecoVendaValor);

        vm.FormEstoqueAtualTexto = "1";
        Assert.Equal("1", vm.FormEstoqueAtualTexto);
        Assert.Equal(1m, vm.EstoqueAtualValor);

        Salvar(vm);

        var dto = vm.Pecas.Single();
        Assert.Equal(1m, dto.PrecoCusto);
        Assert.Equal(1m, dto.PrecoVenda);
        Assert.Equal(1m, dto.EstoqueAtual);
    }

    [Fact]
    public async Task Regressao_DezComVirgulaNaoVirarMilQuinhentos()
    {
        var vm = await CriarViewModelAsync();
        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);

        vm.FormDescricao = "PECA";
        vm.FormPrecoCustoTexto = "10,50";
        vm.FormPrecoVendaTexto = "10,50";

        Assert.Equal(10.50m, vm.PrecoCustoValor);
        Assert.Equal(10.50m, vm.PrecoVendaValor);

        Salvar(vm);

        Assert.Equal(10.50m, vm.Pecas.Single().PrecoCusto);
        Assert.Equal(10.50m, vm.Pecas.Single().PrecoVenda);
    }
    // ============================ Auxiliares ============================

    /// <summary>Percorre a árvore lógica (elementos declarados no XAML) sem exigir layout.</summary>
    private static IEnumerable<DependencyObject> ObterArvoreLogica(DependencyObject raiz)
    {
        foreach (var filho in LogicalTreeHelper.GetChildren(raiz).OfType<DependencyObject>())
        {
            yield return filho;

            foreach (var neto in ObterArvoreLogica(filho))
                yield return neto;
        }
    }

    public void Dispose()
    {
        _escopo.Dispose();
        _provedor.Dispose();

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        try
        {
            if (File.Exists(_caminhoBanco)) File.Delete(_caminhoBanco);
        }
        catch (IOException)
        {
            // Banco temporário: ignorar falhas de limpeza.
        }
    }
}
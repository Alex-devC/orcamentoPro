using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrcPro.App.ViewModels;
using OrcPro.App.Views;
using OrcPro.Application.DTOs.Auth;
using OrcPro.Application.DTOs.Servico;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Common;
using OrcPro.Domain.Common.Formatters;
using OrcPro.Infrastructure.DependencyInjection;
using OrcPro.Infrastructure.Persistence.Providers;
using Xunit;

namespace OrcPro.Tests;

/// <summary>
/// Cobertura funcional do módulo de Serviços / Mão de Obra sobre um banco SQLite real (schema
/// criado pelo <see cref="DatabaseInitializer"/>, exatamente o caminho usado pelo aplicativo).
/// Cobre: abrir o módulo, listar, novo, código automático, entrada natural do valor, salvar,
/// editar, visualizar, pesquisar, filtrar, ordenar, paginar, ativar/inativar, excluir, validar,
/// permissões, reabrir o módulo e a criação da tabela em base já existente.
/// </summary>
[Collection(WpfTestSupport.Colecao)]
public sealed class ServicosModuloTests : IDisposable
{
    private readonly ServiceProvider _provedor;
    private readonly IServiceScope _escopo;
    private readonly string _caminhoBanco;

    public ServicosModuloTests()
    {
        _caminhoBanco = Path.Combine(Path.GetTempPath(), $"servicos-{Guid.NewGuid():N}.db");

        var services = new ServiceCollection();
        services.AddInfrastructure(DatabaseConnectionOptions.DefaultSqlite(_caminhoBanco));
        _provedor = services.BuildServiceProvider();

        // Mesmo caminho de inicialização do App: cria schema, aplica upgrades e permissões.
        DatabaseInitializer.InitializeAsync(_provedor).GetAwaiter().GetResult();

        _escopo = _provedor.CreateScope();
    }

    public void Dispose()
    {
        _escopo.Dispose();
        _provedor.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        try
        {
            if (File.Exists(_caminhoBanco))
                File.Delete(_caminhoBanco);
        }
        catch (IOException)
        {
            // Arquivo ainda aberto pelo pool: irrelevante para um arquivo temporário.
        }
    }

    private IServicoService Servico => _escopo.ServiceProvider.GetRequiredService<IServicoService>();

    /// <summary>Sessão com todas as permissões do catálogo (equivalente ao perfil Administrador).</summary>
    private static UsuarioSessaoDto SessaoAdministrador => new()
    {
        Id = 1,
        Username = "admin",
        NomeCompleto = "Administrador",
        PerfilNome = PermissaoCatalogo.PerfilAdministrador,
        Permissoes = PermissaoCatalogo.Definicoes.Select(d => d.Codigo).ToList()
    };

    /// <summary>Sessão sem nenhuma permissão de serviços.</summary>
    private static UsuarioSessaoDto SessaoSemPermissao => new()
    {
        Id = 2,
        Username = "sempermissao",
        NomeCompleto = "Sem Permissão",
        PerfilNome = "Visitante",
        Permissoes = new List<string>()
    };

    private ServicosViewModel CriarViewModel(UsuarioSessaoDto? sessao = null, Action<string>? status = null)
        => WpfTestSupport.RunOnStaThread<ServicosViewModel>(
            () => new ServicosViewModel(Servico, sessao ?? SessaoAdministrador, status ?? (_ => { })));

    private static CriarServicoDto Novo(
        string codigo, string descricao, bool ativo = true, string? categoria = null, decimal valor = 100m)
        => new()
        {
            Codigo = codigo,
            Descricao = descricao,
            Categoria = categoria ?? "INSTALAÇÃO",
            Valor = valor,
            Unidade = "HR",
            TempoEstimado = 1m,
            Ativo = ativo
        };

    /// <summary>Executa um comando assíncrono e aguarda a conclusão real da operação.</summary>
    private static void ExecutarComando(ICommand comando, object? parametro, Func<bool> concluido)
    {
        var podeExecutar = WpfTestSupport.RunOnStaThread(() => comando.CanExecute(parametro));
        Assert.True(podeExecutar, "O comando não está habilitado para esta operação.");

        WpfTestSupport.RunOnStaThreadUntil(() => comando.Execute(parametro), concluido);
    }

    private static DataTemplate ObterDataTemplateDoModulo()
    {
        var dicionario = new ResourceDictionary
        {
            Source = new Uri("/OrcPro.App;component/Resources/DataTemplates.xaml", UriKind.Relative)
        };

        return dicionario.Values.OfType<DataTemplate>()
            .First(t => Equals(t.DataType, typeof(ServicosViewModel)));
    }

    private static void Salvar(ServicosViewModel vm)
        => ExecutarComando(vm.SalvarCommand, null, () => !vm.EditorAberto || vm.EditorMensagemVisivel);

    // ---------- View e template ----------

    [Fact]
    public void ServicosView_Instantiation_DeveCriarSemException()
    {
        WpfTestSupport.RunOnStaThread(() =>
        {
            var view = new ServicosView();
            Assert.NotNull(view);
        });
    }

    [Fact]
    public void ServicosView_DeveConterGridResumoDeValidacaoETagsDeFoco()
    {
        WpfTestSupport.RunOnStaThread(() =>
        {
            var view = new ServicosView();
            var arvore = Percorrer(view).ToList();

            var grid = arvore.OfType<DataGrid>()
                .FirstOrDefault(g => AutomationProperties.GetAutomationId(g) == "ServicosGrid");
            Assert.NotNull(grid);

            var resumo = arvore.OfType<ItemsControl>()
                .FirstOrDefault(e => AutomationProperties.GetAutomationId(e) == "ServicoResumoValidacao");
            Assert.NotNull(resumo);
            Assert.NotNull(resumo.Style);
            Assert.NotNull(resumo.Style.BasedOn);

            // Tags usadas pelo FormFocusHelper para focar o primeiro campo inválido.
            var tags = arvore.OfType<FrameworkElement>()
                .Select(e => e.Tag as string)
                .Where(t => t is not null)
                .ToList();
            Assert.Contains("Codigo", tags);
            Assert.Contains("Descricao", tags);
            Assert.Contains("Valor", tags);
            Assert.Contains("TempoEstimado", tags);
        });
    }

    [Fact]
    public void ServicosView_DataTemplateDoModulo_DeveResolverParaServicosView()
    {
        WpfTestSupport.RunOnStaThread(() =>
        {
            var template = ObterDataTemplateDoModulo();
            Assert.NotNull(template);

            var vm = CriarViewModel();
            var conteudo = template.LoadContent();

            Assert.NotNull(conteudo);
            Assert.IsType<ServicosView>(conteudo);
            Assert.NotNull(vm);
        });
    }

    private static IEnumerable<DependencyObject> Percorrer(DependencyObject raiz)
    {
        foreach (var filho in LogicalTreeHelper.GetChildren(raiz).OfType<DependencyObject>())
        {
            yield return filho;
            foreach (var neto in Percorrer(filho))
                yield return neto;
        }
    }
    // ---------- Código automático ----------

    [Fact]
    public async Task ServicosModulo_Novo_DevePreencherCodigoAutomaticamenteEmLeitura()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);

        Assert.True(vm.EditorAberto);
        Assert.True(vm.EditorNovo);
        Assert.Equal("SRV-0001", vm.FormCodigo);
        Assert.DoesNotContain("SRV-0001", string.Empty);
    }


    [Fact]
    public void ServicosModulo_CodigoNaoPodeSerDigitadoPeloUsuario()
    {
        WpfTestSupport.RunOnStaThread(() =>
        {
            var arvore = Percorrer(new ServicosView()).ToList();

            var codigo = arvore.OfType<TextBox>()
                .FirstOrDefault(e => AutomationProperties.GetAutomationId(e) == "ServicoCodigoInput");
            Assert.NotNull(codigo);

            Assert.True(codigo!.IsReadOnly, "O código é gerado pelo sistema e não pode ser digitado.");

            var binding = codigo.GetBindingExpression(TextBox.TextProperty)?.ParentBinding;
            Assert.NotEqual(BindingMode.TwoWay, binding?.Mode);
        });
    }

    [Fact]
    public async Task ServicosModulo_SemCodigo_DeveExibirMensagemDeValidacaoNoTopo()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);

        vm.FormCodigo = string.Empty;
        vm.FormDescricao = "Teste";
        Salvar(vm);

        Assert.True(vm.EditorAberto);
        Assert.True(vm.TemResumoValidacao);
        Assert.Equal("Informe o código do serviço.", vm.EditorMensagem);
        Assert.Equal("Codigo", vm.PrimeiroCampoInvalido);
    }

    [Fact]
    public async Task ServicosModulo_CodigosDevemAvancarSequencialmente()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        var codigos = new List<string>();

        for (int i = 0; i < 3; i++)
        {
            ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);
            codigos.Add(vm.FormCodigo);

            vm.FormDescricao = $"Serviço {i + 1}";
            vm.FormValorTexto = "10";
            Salvar(vm);
        }

        Assert.Equal(new[] { "SRV-0001", "SRV-0002", "SRV-0003" }, codigos);
        Assert.Equal(3, vm.TotalRegistros);
    }

    // ---------- Entrada natural de valor ----------

    [Fact]
    public async Task ServicosModulo_Digitar1NoValorNaoPodeResultarEm1000_Regressao()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);

        vm.FormDescricao = "Diagnóstico";
        vm.FormValorTexto = "1";

        // A digitação permanece exatamente como o usuário digitou...
        Assert.Equal("1", vm.FormValorTexto);
        Assert.True(DecimalInputHelper.TentarConverter(vm.FormValorTexto, out var valor));
        Assert.Equal(1m, valor);
        Assert.NotEqual(1000m, valor);

        Salvar(vm);

        // ... e o valor gravado é 1, não 1.000.
        Assert.False(vm.EditorAberto);
        Assert.Equal(1m, vm.Servicos[0].Valor);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("10", 10)]
    [InlineData("10,50", 10.50)]
    [InlineData("1000", 1000)]
    [InlineData("1250,75", 1250.75)]
    public async Task ServicosModulo_ValoresDigitadosNoPadraoBrasileiro_DevemSerGravadosComoDigitados(string digitado, double esperado)
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);

        vm.FormDescricao = "Serviço de teste";
        vm.FormValorTexto = digitado;
        Salvar(vm);

        Assert.False(vm.EditorAberto);
        Assert.Equal((decimal)esperado, vm.Servicos[0].Valor);
    }

    [Fact]
    public async Task ServicosModulo_ValorNaoNumerico_DeveMostrarValidacaoNoTopoEFocarCampo()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);

        vm.FormDescricao = "Serviço inválido";
        vm.FormValorTexto = "abc";
        Salvar(vm);

        Assert.True(vm.EditorAberto, "O formulário deveria permanecer aberto.");
        Assert.True(vm.TemResumoValidacao);
        Assert.NotEmpty(vm.ResumoValidacao);
        Assert.Contains(vm.ResumoValidacao, e => e.Contains("valor"));
        Assert.Equal("Valor", vm.PrimeiroCampoInvalido);
        Assert.True(vm.ValorTemErro);
    }

    [Fact]
    public async Task ServicosModulo_DescricaoVazia_DeveFalharComMensagemNoTopo()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);

        vm.FormDescricao = "   ";
        vm.FormValorTexto = "10";
        Salvar(vm);

        Assert.True(vm.EditorAberto);
        Assert.True(vm.TemResumoValidacao);
        Assert.Contains(vm.ResumoValidacao, e => e.Contains("descrição"));
        Assert.Equal("Descricao", vm.PrimeiroCampoInvalido);
        Assert.True(vm.DescricaoTemErro);
    }

    // ---------- MAIÚSCULAS durante a edição ----------

    [Fact]
    public async Task ServicosModulo_TextosDevemGravarEmMaiusculas()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);

        vm.FormDescricao = "instalação de câmera";
        vm.FormCategoria = "segurança";
        vm.FormUnidade = "hr";
        vm.FormObservacoes = "inclui material";
        vm.FormValorTexto = "100";
        Salvar(vm);

        var salvo = vm.Servicos[0];
        Assert.Equal("INSTALAÇÃO DE CÂMERA", salvo.Descricao);
        Assert.Equal("SEGURANÇA", salvo.Categoria);
        Assert.Equal("HR", salvo.Unidade);
        Assert.Equal("INCLUI MATERIAL", salvo.Observacoes);
    }
    // ---------- Editar / visualizar ----------

    [Fact]
    public async Task ServicosModulo_EditarServicoExistente_DevePersistirAlteracoes()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);
        vm.FormDescricao = "Descrição original";
        vm.FormValorTexto = "100";
        Salvar(vm);

        var alvo = vm.Servicos[0];
        vm.EditarCommand.Execute(alvo);

        Assert.True(vm.EditorAberto);
        Assert.False(vm.EditorNovo);

        vm.FormDescricao = "Descrição alterada";
        vm.FormValorTexto = "250,50";
        Salvar(vm);

        Assert.False(vm.EditorAberto);
        var atualizado = vm.Servicos.First(s => s.Id == alvo.Id);
        Assert.Equal("DESCRIÇÃO ALTERADA", atualizado.Descricao);
        Assert.Equal(250.50m, atualizado.Valor);
        Assert.Equal(alvo.Codigo, atualizado.Codigo);
    }

    [Fact]
    public async Task ServicosModulo_Visualizar_DeveAbrirFichaEmModoLeitura()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);
        vm.FormDescricao = "Para visualizar";
        vm.FormValorTexto = "75";
        Salvar(vm);

        var alvo = vm.Servicos[0];
        vm.VisualizarCommand.Execute(alvo);

        Assert.True(vm.VisualizacaoAberta);
        Assert.False(vm.EditorAberto);
        Assert.Equal(alvo.Descricao, vm.FormDescricao);

        vm.FecharVisualizacaoCommand.Execute(null);
        Assert.False(vm.VisualizacaoAberta);
    }

    // ---------- Pesquisa, filtro, ordenação e paginação ----------

    [Fact]
    public async Task ServicosModulo_Pesquisar_DeveFiltrarPorCodigoDescricaoECategoria()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);
        vm.FormDescricao = "Instalação de câmera";
        vm.FormCategoria = "segurança";
        vm.FormValorTexto = "100";

        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);
        vm.FormDescricao = "Reparo de monitor";
        vm.FormCategoria = "hardware";
        vm.FormValorTexto = "100";
        Salvar(vm);

        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);
        vm.FormDescricao = "Formatacao de planilha";
        vm.FormCategoria = "automacao";
        vm.FormValorTexto = "100";
        Salvar(vm);

        Assert.Equal(2, vm.TotalRegistros);

        // Pesquisa por descricao.
        vm.Busca = "monitor";
        await vm.CarregarAsync();
        Assert.Single(vm.Servicos);
        Assert.Equal("REPARO DE MONITOR", vm.Servicos[0].Descricao);

        // Pesquisa por categoria.
        vm.Busca = "automacao";
        await vm.CarregarAsync();
        Assert.Single(vm.Servicos);
        Assert.Equal("SRV-0002", vm.Servicos[0].Codigo);

        // Pesquisa por codigo.
        vm.Busca = "SRV-0001";
        await vm.CarregarAsync();
        Assert.Single(vm.Servicos);
        Assert.Equal("SRV-0001", vm.Servicos[0].Codigo);

        // Pesquisa sem resultado mostra o estado vazio.
        vm.Busca = "inexistente";
        await vm.CarregarAsync();
        Assert.Empty(vm.Servicos);
        Assert.False(vm.TemRegistros);

        vm.LimparBuscaCommand.Execute(null);
        await vm.CarregarAsync();
        Assert.Equal(2, vm.TotalRegistros);
    }
    [Fact]
    public async Task ServicosModulo_FiltroPorSituacao_DeveSepararAtivosEInativos()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);
        vm.FormDescricao = "Ativo um";
        Salvar(vm);

        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);
        vm.FormDescricao = "Ativo dois";
        Salvar(vm);

        var alvo = vm.Servicos[0];
        ExecutarComando(vm.AlternarSituacaoCommand, alvo, () => !vm.Servicos[0].Ativo);

        Assert.Equal(2, vm.TotalRegistros);

        vm.SituacaoFiltro = 1;
        await vm.CarregarAsync();
        Assert.Single(vm.Servicos);
        Assert.True(vm.Servicos[0].Ativo);

        vm.SituacaoFiltro = 2;
        await vm.CarregarAsync();
        Assert.Single(vm.Servicos);
        Assert.False(vm.Servicos[0].Ativo);

        vm.SituacaoFiltro = 0;
        await vm.CarregarAsync();
        Assert.Equal(2, vm.Servicos.Count);
    }

    [Fact]
    public async Task ServicosModulo_OrdenarPorCabecalho_DeveOrdenarNoBanco()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        foreach (var descricao in new[] { "Bravo", "Alfa", "Charlie" })
        {
            ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);
            vm.FormDescricao = descricao;
            Salvar(vm);
        }

        await vm.AplicarOrdenacaoAsync("Descricao");
        Assert.Equal(new[] { "ALFA", "BRAVO", "CHARLIE" }, vm.Servicos.Select(s => s.Descricao).ToArray());

        // Segundo clique inverte a ordenação.
        await vm.AplicarOrdenacaoAsync("Descricao");
        Assert.Equal(new[] { "CHARLIE", "BRAVO", "ALFA" }, vm.Servicos.Select(s => s.Descricao).ToArray());
    }

    [Fact]
    public async Task ServicosModulo_Paginacao_DeveNavegarEntrePaginas()
    {
        for (int i = 1; i <= 25; i++)
            await Servico.CriarAsync(Novo($"SRV-{i:D4}", $"Serviço {i:00}"));

        var vm = CriarViewModel();
        await vm.InitializeAsync();

        Assert.Equal(25, vm.TotalRegistros);
        Assert.Equal(2, vm.TotalPaginas);
        Assert.Equal(1, vm.Pagina);
        Assert.Equal(20, vm.Servicos.Count);

        Assert.True(vm.PodePaginaProxima);
        ExecutarComando(vm.PaginaProximaCommand, null, () => vm.Pagina == 2);

        Assert.Equal(2, vm.Pagina);
        Assert.Equal(5, vm.Servicos.Count);
        Assert.False(vm.PodePaginaProxima);
        Assert.Contains("Página 2 de 2", vm.ResumoPaginacao);
    }

    // ---------- Situação e exclusão ----------

    [Fact]
    public async Task ServicosModulo_AlternarSituacao_DeveInativarEReativar()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);
        vm.FormDescricao = "Situação";
        Salvar(vm);

        var alvo = vm.Servicos[0];

        ExecutarComando(vm.AlternarSituacaoCommand, alvo, () => !vm.Servicos[0].Ativo);
        Assert.False(vm.Servicos[0].Ativo);

        ExecutarComando(vm.AlternarSituacaoCommand, vm.Servicos[0], () => vm.Servicos[0].Ativo);
        Assert.True(vm.Servicos[0].Ativo);
    }

    [Fact]
    public async Task ServicosModulo_Excluir_DevePedirConfirmacaoERemoverRegistro()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);
        vm.FormDescricao = "Para excluir";
        Salvar(vm);

        var alvo = vm.Servicos[0];

        vm.ExcluirCommand.Execute(alvo);
        Assert.True(vm.ConfirmacaoAberta);
        Assert.Contains("SRV-0001", vm.ConfirmacaoMensagem);

        // Cancelar não exclui.
        vm.CancelarExclusaoCommand.Execute(null);
        Assert.False(vm.ConfirmacaoAberta);
        Assert.Single(vm.Servicos);

        // Confirmar exclui.
        vm.ExcluirCommand.Execute(vm.Servicos[0]);
        Assert.True(vm.ConfirmacaoAberta);
        ExecutarComando(vm.ConfirmarExclusaoCommand, null, () => !vm.ConfirmacaoAberta && vm.TotalRegistros == 0);

        Assert.Empty(vm.Servicos);
        Assert.False(vm.TemRegistros);
    }

    [Fact]
    public async Task ServicosModulo_DepoisDeExcluirOCodigoNaoVoltaAoMudarFiltroDeSituacao()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);
        vm.FormDescricao = "Original";
        Salvar(vm);

        vm.ExcluirCommand.Execute(vm.Servicos[0]);
        ExecutarComando(vm.ConfirmarExclusaoCommand, null, () => vm.TotalRegistros == 0);

        // Inativos não somem ao excluir: a listagem volta vazia e sem erro.
        vm.SituacaoFiltro = 1;
        await vm.CarregarAsync();
        Assert.Empty(vm.Servicos);
    }

    // ---------- Permissões ----------

    [Fact]
    public async Task ServicosModulo_SemPermissao_DeveDesabilitarAsAcoes()
    {
        var vm = CriarViewModel(SessaoSemPermissao);
        await vm.InitializeAsync();

        Assert.False(vm.PodeVisualizar);
        Assert.False(vm.PodeCriar);
        Assert.False(vm.PodeEditar);
        Assert.False(vm.PodeExcluir);
        Assert.False(vm.PodeAtivarInativar);

        Assert.False(vm.NovoCommand.CanExecute(null));
        Assert.False(vm.EditarCommand.CanExecute(vm.Servicos.FirstOrDefault()));
        Assert.False(vm.VisualizarCommand.CanExecute(vm.Servicos.FirstOrDefault()));
        Assert.False(vm.ExcluirCommand.CanExecute(vm.Servicos.FirstOrDefault()));
        Assert.False(vm.AlternarSituacaoCommand.CanExecute(vm.Servicos.FirstOrDefault()));
    }

    [Fact]
    public async Task ServicosModulo_ComPermissao_DeveLiberarAsAcoes()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        Assert.True(vm.PodeVisualizar);
        Assert.True(vm.PodeCriar);
        Assert.True(vm.PodeEditar);
        Assert.True(vm.PodeExcluir);
        Assert.True(vm.PodeAtivarInativar);

        Assert.True(vm.NovoCommand.CanExecute(null));
    }
    // ---------- Reabrir o módulo ----------

    [Fact]
    public async Task ServicosModulo_Reabrir_DeveRecarregarSemErro()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        ExecutarComando(vm.NovoCommand, null, () => vm.EditorAberto);
        vm.FormDescricao = "Persistente";
        Salvar(vm);
        Assert.Single(vm.Servicos);

        // Sair do módulo e abrir de novo (nova instância, mesma base).
        var nova = CriarViewModel();
        await nova.InitializeAsync();

        Assert.Single(nova.Servicos);
        Assert.Equal("PERSISTENTE", nova.Servicos[0].Descricao);
        Assert.False(nova.EditorAberto);
    }

    [Fact]
    public async Task ServicosModulo_DadosDevemPersistirAposReabrirAplicacao()
    {

        var servico = await Servico.CriarAsync(Novo("SRV-0007", "PERSISTIDO APOS REINICIO", valor: 1250.75m));

        // Novo provedor sobre o MESMO arquivo: equivale a reiniciar o aplicativo.
        var services = new ServiceCollection();
        services.AddInfrastructure(DatabaseConnectionOptions.DefaultSqlite(_caminhoBanco));
        using var novoProvedor = services.BuildServiceProvider();

        await DatabaseInitializer.InitializeAsync(novoProvedor);

        using var novoEscopo = novoProvedor.CreateScope();
        var novoServico = novoEscopo.ServiceProvider.GetRequiredService<IServicoService>();

        var recarregado = await novoServico.ObterPorIdAsync(servico.Id);

        Assert.Equal("SRV-0007", recarregado.Codigo);
        Assert.Equal("PERSISTIDO APOS REINICIO", recarregado.Descricao);
        Assert.Equal(1250.75m, recarregado.Valor);
        Assert.True(recarregado.Ativo);
    }

    [Fact]
    public async Task SchemaUpgrade_DeveCriarTabelaServicosEmBaseJaExistente()
    {
        // Simula uma instalação anterior ao módulo: base criada e tabela Servicos removida.
        var caminho = Path.Combine(Path.GetTempPath(), $"schema-{Guid.NewGuid():N}.db");
        try
        {
            var services = new ServiceCollection();
            services.AddInfrastructure(DatabaseConnectionOptions.DefaultSqlite(caminho));
            await using var provedor = services.BuildServiceProvider();

            await DatabaseInitializer.InitializeAsync(provedor);

            await using (var escopo = provedor.CreateAsyncScope())
            {
                var ctx = escopo.ServiceProvider.GetRequiredService<OrcPro.Infrastructure.Persistence.OrcProDbContext>();
                await ctx.Database.ExecuteSqlRawAsync("DROP TABLE \"Servicos\"");
            }

            // O EnsureCreated não recria tabela em base existente: quem cria é o SchemaUpgrade.
            await DatabaseInitializer.InitializeAsync(provedor);

            await using (var escopo = provedor.CreateAsyncScope())
            {
                var ctx = escopo.ServiceProvider.GetRequiredService<OrcPro.Infrastructure.Persistence.OrcProDbContext>();
                Assert.False(await ctx.Servicos.AnyAsync());

                await ctx.Database.ExecuteSqlRawAsync(
                    "INSERT INTO \"Servicos\" (\"Codigo\",\"Descricao\",\"Categoria\",\"Valor\",\"Unidade\",\"TempoEstimado\",\"Ativo\",\"DataCriacao\") " +
                    "VALUES ('SRV-0001','TESTE','TESTE','10','HR','1',1,'2026-01-01 00:00:00')");
            }

            // Idempotente: rodar de novo não pode falhar nem duplicar nada.
            await DatabaseInitializer.InitializeAsync(provedor);

            await using (var escopo = provedor.CreateAsyncScope())
            {
                var novoServico = escopo.ServiceProvider.GetRequiredService<IServicoService>();
                var lista = await novoServico.ListarTodosAtivosAsync();

                Assert.Single(lista);
                Assert.Equal("SRV-0001", lista[0].Codigo);
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try
            {
                if (File.Exists(caminho))
                    File.Delete(caminho);
            }
            catch (IOException)
            {
                // Arquivo temporário ainda bloqueado: irrelevante.
            }
        }
    }
}

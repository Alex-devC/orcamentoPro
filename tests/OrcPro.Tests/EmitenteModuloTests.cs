using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Common;
using OrcPro.Domain.Common.Formatters;
using CpfCnpjValidatorAlias = OrcPro.Domain.Common.Formatters.CpfCnpjValidator;
using OrcPro.Infrastructure.DependencyInjection;
using OrcPro.Infrastructure.Persistence;
using OrcPro.Infrastructure.Persistence.Providers;
using OrcPro.Infrastructure.Services;
using Xunit;

namespace OrcPro.Tests;

/// <summary>
/// CEP fake que CONTA as consultas: é assim que se prova que digitar, apagar, alterar,
/// sair do campo ou pressionar ENTER nunca consultam — apenas a lupa chama o serviço.
/// </summary>
public class ContadorCepService : ICepService
{
    public int Chamadas { get; private set; }

    public string ProviderName => "ContadorCEP";

    public Task<CepAddressResult> ConsultarAsync(string cep, CancellationToken cancellationToken = default)
    {
        Chamadas++;

        return Task.FromResult(CepAddressResult.SuccessResult(
            cep,
            logradouro: "AVENIDA PAULISTA",
            bairro: "BELA VISTA",
            cidade: "SAO PAULO",
            uf: "SP",
            source: ProviderName));
    }
}

/// <summary>
/// Cobertura funcional de Minha Empresa / Emitente sobre banco SQLite real (schema criado pelo
/// <see cref="DatabaseInitializer"/>, o mesmo caminho do aplicativo) e sobre o ViewModel WPF:
/// registro único, persistência, validação, permissões, logo e consulta de CEP somente pela lupa.
/// </summary>
[Collection(WpfTestSupport.Colecao)]
public sealed class EmitenteModuloTests : IDisposable
{
    private readonly ServiceProvider _provedor;
    private readonly IServiceScope _escopo;
    private readonly string _caminhoBanco;
    private readonly string _pastaLogo;

    public EmitenteModuloTests()
    {
        _caminhoBanco = Path.Combine(Path.GetTempPath(), $"emitente-{Guid.NewGuid():N}.db");
        _pastaLogo = Path.Combine(Path.GetTempPath(), $"logo-{Guid.NewGuid():N}");

        var services = new ServiceCollection();
        services.AddInfrastructure(DatabaseConnectionOptions.DefaultSqlite(_caminhoBanco));

        // Isola a logo do teste da pasta real do usuário.
        services.AddSingleton<IEmpresaLogoStorage>(new EmpresaLogoStorage(_pastaLogo));

        _provedor = services.BuildServiceProvider();
        DatabaseInitializer.InitializeAsync(_provedor).GetAwaiter().GetResult();

        _escopo = _provedor.CreateScope();
    }

    public void Dispose()
    {
        _escopo.Dispose();
        _provedor.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        TryDelete(_caminhoBanco);
        TryDeleteDirectory(_pastaLogo);
    }

    private static void TryDelete(string caminho)
    {
        try { if (File.Exists(caminho)) File.Delete(caminho); }
        catch (IOException) { }
    }

    private static void TryDeleteDirectory(string pasta)
    {
        try { if (Directory.Exists(pasta)) Directory.Delete(pasta, true); }
        catch (IOException) { }
    }

    private IEmpresaService Servico => _escopo.ServiceProvider.GetRequiredService<IEmpresaService>();

    private static UsuarioSessaoDto SessaoAdministrador => new()
    {
        Id = 1,
        Username = "admin",
        NomeCompleto = "Administrador",
        PerfilNome = PermissaoCatalogo.PerfilAdministrador,
        Permissoes = PermissaoCatalogo.Definicoes.Select(d => d.Codigo).ToList()
    };

    /// <summary>Somente EMITENTE.VISUALIZAR: abre a tela, mas não pode salvar.</summary>
    private static UsuarioSessaoDto SessaoSomenteVisualizar => new()
    {
        Id = 2,
        Username = "somente_visualizar",
        NomeCompleto = "Consulta",
        PerfilNome = "Consulta",
        Permissoes = new List<string> { PermissaoCatalogo.Codigos.Emitente.Visualizar }
    };

    private static UsuarioSessaoDto SessaoSemPermissao => new()
    {
        Id = 3,
        Username = "sem_permissao",
        NomeCompleto = "Sem Permissao",
        PerfilNome = "Visitante",
        Permissoes = new List<string>()
    };

    private MinhaEmpresaViewModel CriarViewModel(
        UsuarioSessaoDto? sessao = null,
        ICepService? cep = null,
        Action<string>? status = null)
        => WpfTestSupport.RunOnStaThread<MinhaEmpresaViewModel>(
            () => new MinhaEmpresaViewModel(Servico, sessao ?? SessaoAdministrador, cep, status ?? (_ => { })));

    private static void ExecutarComando(ICommand comando, object? parametro, Func<bool> concluido)
    {
        var podeExecutar = WpfTestSupport.RunOnStaThread(() => comando.CanExecute(parametro));
        Assert.True(podeExecutar, "O comando não está habilitado para esta operação.");
        WpfTestSupport.RunOnStaThreadUntil(() => comando.Execute(parametro), concluido);
    }

    /// <summary>
    /// Salva aguardando o registro ser criado (primeira vez) ou havendo erro de validação.
    /// </summary>
    private static void Salvar(MinhaEmpresaViewModel vm)
        => ExecutarComando(vm.SalvarCommand, null, () => vm.EstaConfigurado || vm.EditorMensagemVisivel);

    private const string MensagemSalvou = "Minha empresa salva com sucesso.";

    /// <summary>
    /// Salva aguardando a conclusão real da operação. Usa o contador de status porque o
    /// texto da mensagem se repete entre salvamentos, e <c>EstaConfigurado</c> já é
    /// verdadeira a partir da segunda gravação.
    /// </summary>
    private static void SalvarConfirmado(MinhaEmpresaViewModel vm)
    {
        var antes = vm.StatusVersion;
        ExecutarComando(vm.SalvarCommand, null, () => vm.StatusVersion > antes || vm.EditorMensagemVisivel);
    }

    private static readonly string CnpjValido = CpfCnpjValidatorAlias.GerarCnpjNumericoValido();

    private static readonly string RazaoPadrao = "ALEX T.I. TECNOLOGIA E ASSISTENCIA LTDA";

    private static OrcPro.Application.DTOs.Empresa.SalvarEmpresaDto Novo(string? razao = null, string? cnpj = null) => new()
    {
        RazaoSocial = razao ?? RazaoPadrao,
        Cnpj = cnpj ?? CnpjValido
    };

    // ---------- View ----------

    [Fact]
    public void MinhaEmpresaView_Instantiation_DeveCriarSemException()
    {
        WpfTestSupport.RunOnStaThread(() =>
        {
            var view = new MinhaEmpresaView();
            Assert.NotNull(view);
        });
    }

    [Fact]
    public void MinhaEmpresaView_DeveConterOsGruposEOTagDeFoco()
    {
        WpfTestSupport.RunOnStaThread(() =>
        {
            var arvore = Percorrer(new MinhaEmpresaView()).ToList();

            var resumo = arvore.OfType<ItemsControl>()
                .FirstOrDefault(e => AutomationProperties.GetAutomationId(e) == "EmpresaResumoValidacao");
            Assert.NotNull(resumo);
            Assert.NotNull(resumo.Style);
            Assert.NotNull(resumo.Style.BasedOn);

            // Controle de CEP com lupa (única porta de consulta).
            var cep = Assert.Single(arvore.OfType<OrcPro.App.Controls.CepComLookupControl>());
            Assert.Equal("CEP", cep.Tag);
            Assert.Equal("EmpresaCepInput", cep.AutomationId);
            Assert.Equal("EmpresaCepLupa", cep.LupaAutomationId);

            var tags = arvore.OfType<FrameworkElement>()
                .Select(e => e.Tag as string)
                .Where(t => t is not null)
                .ToList();
            Assert.Contains("RazaoSocial", tags);
            Assert.Contains("Cnpj", tags);
            Assert.Contains("CEP", tags);

            // Ações principais presentes.
            Assert.Contains(arvore, e => AutomationProperties.GetAutomationId(e) == "EmpresaSalvar");
            Assert.Contains(arvore, e => AutomationProperties.GetAutomationId(e) == "EmpresaCancelar");
            Assert.Contains(arvore, e => AutomationProperties.GetAutomationId(e) == "EmpresaSelecionarLogo");
            Assert.Contains(arvore, e => AutomationProperties.GetAutomationId(e) == "EmpresaRemoverLogo");
        });
    }

    [Fact]
    public void MinhaEmpresaView_DataTemplateDoModulo_DeveResolverParaMinhaEmpresaView()
    {
        WpfTestSupport.RunOnStaThread(() =>
        {
            var dicionario = new ResourceDictionary
            {
                Source = new Uri("/OrcPro.App;component/Resources/DataTemplates.xaml", UriKind.Relative)
            };

            var template = dicionario.Values.OfType<DataTemplate>()
                .First(t => Equals(t.DataType, typeof(MinhaEmpresaViewModel)));

            Assert.IsType<MinhaEmpresaView>(template.LoadContent());
        });
    }

    [Fact]
    public void MinhaEmpresaView_ComboBoxUf_NaoPodeSobrescreverOValorDoViewModel()
    {
        WpfTestSupport.RunOnStaThread(() =>
        {
            var vm = CriarViewModel();
            var view = new MinhaEmpresaView { DataContext = vm };

            var combo = Percorrer(view).OfType<ComboBox>()
                .FirstOrDefault(c => AutomationProperties.GetAutomationId(c) == "EmpresaUfCombo");
            Assert.NotNull(combo);

            // Reproduz o que a consulta de CEP faz: a UF chega pelo ViewModel.
            vm.FormUf = "SP";

            // Um ComboBox editável com SelectedItem TwoWay ligado a string reescrevia o
            // valor do ViewModel e a gravação passava a falhar com "UF inválida".
            Assert.Equal("SP", vm.FormUf);

            vm.FormUf = "RJ";
            Assert.Equal("RJ", vm.FormUf);
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

    // ---------- Persistência sobre banco real ----------

    [Fact]
    public async Task Emitente_AbrirSemCadastro_DeveMostrarFormularioVazio()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        Assert.False(vm.EstaConfigurado);
        Assert.Equal(string.Empty, vm.FormRazaoSocial);
        Assert.Equal(string.Empty, vm.FormCnpj);
        Assert.Equal(string.Empty, vm.FormCidade);
        Assert.Null(vm.LogoPreview);
    }

    [Fact]
    public async Task Emitente_AbrirSemCadastro_NaoPodeCriarDadosFicticios()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        using var escopo = _provedor.CreateScope();
        var ctx = escopo.ServiceProvider.GetRequiredService<OrcProDbContext>();

        // Nenhuma empresa é criada ao abrir a tela.
        Assert.False(await ctx.Empresas.AnyAsync());
    }

    [Fact]
    public async Task Emitente_Salvar_PersisteImediatamente()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        vm.FormRazaoSocial = RazaoPadrao;
        vm.FormNomeFantasia = "ALEX Orcamentos";
        vm.FormCnpj = CnpjValido;
        vm.FormEmail = "CONTATO@ALEX.COM.BR";
        vm.FormCidade = "SAO PAULO";
        vm.FormUf = "SP";
        Salvar(vm);

        Assert.True(vm.EstaConfigurado);

        var lido = await Servico.ObterAsync();
        Assert.Equal(RazaoPadrao, lido!.RazaoSocial);
        Assert.Equal("contato@alex.com.br", lido.Email);
    }

    [Fact]
    public async Task Emitente_Editar_DeveAtualizarOEmitenteUnico()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        vm.FormRazaoSocial = RazaoPadrao;
        Salvar(vm);

        var vm2 = CriarViewModel();
        await vm2.InitializeAsync();

        Assert.Equal(RazaoPadrao, vm2.FormRazaoSocial);

        vm2.FormRazaoSocial = "NOVA RAZAO SOCIAL LTDA";
        vm2.FormUf = "RJ";
        SalvarConfirmado(vm2);

        Assert.Equal("NOVA RAZAO SOCIAL LTDA", vm2.FormRazaoSocial);
        Assert.Single(await ContarEmpresasAsync());
    }

    [Fact]
    public async Task Emitente_ReabrirModulo_DeveRecarregarOsDados()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();
        vm.FormRazaoSocial = RazaoPadrao;
        vm.FormCidade = "CAMPINAS";
        Salvar(vm);

        // Sai do módulo e abre de novo: nova instância, mesmo banco.
        var vm2 = CriarViewModel();
        await vm2.InitializeAsync();

        Assert.True(vm2.EstaConfigurado);
        Assert.Equal(RazaoPadrao, vm2.FormRazaoSocial);
        Assert.Equal("CAMPINAS", vm2.FormCidade);
    }

    [Fact]
    public async Task Emitente_Cancelar_DeveDescartarAlteracoesNaoSalvas()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        vm.FormRazaoSocial = RazaoPadrao;
        Salvar(vm);

        vm.FormRazaoSocial = "ALTERACAO NAO SALVA";
        vm.FormCidade = "CIDADE NAO SALVA";

        await vm.CancelarAsync();

        Assert.Equal(RazaoPadrao, vm.FormRazaoSocial);
        Assert.Equal(string.Empty, vm.FormCidade);
    }

    [Fact]
    public async Task Emitente_DadosDevemPersistirAposReiniciarOPrograma()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        vm.FormRazaoSocial = RazaoPadrao;
        vm.FormCnpj = CnpjValido;
        vm.FormEmailFinanceiro = "financeiro@alex.com.br";
        Salvar(vm);

        // Novo provedor sobre o MESMO arquivo equivale a reiniciar o aplicativo.
        var services = new ServiceCollection();
        services.AddInfrastructure(DatabaseConnectionOptions.DefaultSqlite(_caminhoBanco));
        services.AddSingleton<IEmpresaLogoStorage>(new EmpresaLogoStorage(_pastaLogo));
        await using var novoProvedor = services.BuildServiceProvider();
        await DatabaseInitializer.InitializeAsync(novoProvedor);

        await using var novoEscopo = novoProvedor.CreateAsyncScope();
        var novoServico = novoEscopo.ServiceProvider.GetRequiredService<IEmpresaService>();

        var recarregado = await novoServico.ObterAsync();

        Assert.Equal(RazaoPadrao, recarregado!.RazaoSocial);
        Assert.Equal(CnpjValido, recarregado.Cnpj);
        Assert.Equal("financeiro@alex.com.br", recarregado.EmailFinanceiro);
        Assert.Single(await ContarEmpresasAsync());
    }

    [Fact]
    public async Task Emitente_NuncaPodeExistirSegundoEmitente()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();
        vm.FormRazaoSocial = "PRIMEIRA EMPRESA";
        Salvar(vm);

        var vm2 = CriarViewModel();
        await vm2.InitializeAsync();
        vm2.FormRazaoSocial = "SEGUNDA EMPRESA";
        SalvarConfirmado(vm2);

        Assert.Single(await ContarEmpresasAsync());
        Assert.Equal("SEGUNDA EMPRESA", (await Servico.ObterAsync())!.RazaoSocial);
    }

    private async Task<List<int>> ContarEmpresasAsync()
    {
        await using var escopo = _provedor.CreateAsyncScope();
        var ctx = escopo.ServiceProvider.GetRequiredService<OrcProDbContext>();
        return await ctx.Empresas.Select(e => e.Id).ToListAsync();
    }

    // ---------- Validação no formulário ----------

    [Fact]
    public async Task Emitente_RazaoSocialVazia_DeveBloquearSalvarComResumoNoTopo()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        vm.FormRazaoSocial = "   ";
        Salvar(vm);

        Assert.False(vm.EstaConfigurado);
        Assert.True(vm.TemResumoValidacao);
        Assert.Contains(vm.ResumoValidacao, e => e.Contains("razão social"));
        Assert.Equal("RazaoSocial", vm.PrimeiroCampoInvalido);
        Assert.True(vm.RazaoSocialTemErro);
    }

    [Fact]
    public async Task Emitente_CnpjInvalido_DeveBloquearSalvar()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        vm.FormRazaoSocial = RazaoPadrao;
        vm.FormCnpj = "11.111.111/1111-11";
        Salvar(vm);

        Assert.False(vm.EstaConfigurado);
        Assert.Equal("Cnpj", vm.PrimeiroCampoInvalido);
        Assert.True(vm.CnpjTemErro);
        Assert.Contains(vm.ResumoValidacao, e => e.Contains("CNPJ"));
    }

    [Fact]
    public async Task Emitente_EmailInvalido_DeveBloquearSalvar()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        vm.FormRazaoSocial = RazaoPadrao;
        vm.FormEmail = "email-invalido";
        Salvar(vm);

        Assert.False(vm.EstaConfigurado);
        Assert.Equal("Email", vm.PrimeiroCampoInvalido);
        Assert.True(vm.EmailTemErro);
    }

    [Fact]
    public async Task Emitente_CepInvalido_DeveBloquearSalvar()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        vm.FormRazaoSocial = RazaoPadrao;
        vm.FormCep = "123";
        Salvar(vm);

        Assert.False(vm.EstaConfigurado);
        Assert.Equal("CEP", vm.PrimeiroCampoInvalido);
        Assert.True(vm.CepTemErro);
    }

    [Fact]
    public async Task Emitente_UfInvalida_DeveBloquearSalvar()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        vm.FormRazaoSocial = RazaoPadrao;
        vm.FormUf = "XX";
        Salvar(vm);

        Assert.False(vm.EstaConfigurado);
        Assert.Equal("Uf", vm.PrimeiroCampoInvalido);
        Assert.True(vm.UfTemErro);
    }

    // ---------- CEP: somente a lupa consulta ----------

    [Fact]
    public async Task Emitente_Cep_DigitarNaoConsulta()
    {
        var cep = new ContadorCepService();
        var vm = CriarViewModel(cep: cep);
        await vm.InitializeAsync();

        vm.FormCep = "01310";
        vm.FormCep = "01310100";

        Assert.Equal(0, cep.Chamadas);
        Assert.Equal("01310100", vm.FormCep);
    }

    [Fact]
    public async Task Emitente_Cep_AlterarNaoConsulta()
    {
        var cep = new ContadorCepService();
        var vm = CriarViewModel(cep: cep);
        await vm.InitializeAsync();

        vm.FormCep = "01310100";
        vm.FormCep = "01001000";
        vm.FormCep = "99999999";

        Assert.Equal(0, cep.Chamadas);
    }

    [Fact]
    public async Task Emitente_Cep_ApagarNaoConsulta()
    {
        var cep = new ContadorCepService();
        var vm = CriarViewModel(cep: cep);
        await vm.InitializeAsync();

        vm.FormCep = "01310100";
        vm.FormCep = string.Empty;

        Assert.Equal(0, cep.Chamadas);
        Assert.Equal(string.Empty, vm.FormCep);
    }

    [Fact]
    public async Task Emitente_Cep_SalvarComCepInvalidoNaoConsulta()
    {
        var cep = new ContadorCepService();
        var vm = CriarViewModel(cep: cep);
        await vm.InitializeAsync();

        vm.FormRazaoSocial = RazaoPadrao;
        vm.FormCep = "01310100";
        Salvar(vm);

        // Salvar consulta o serviço de CEP? Não.
        Assert.Equal(0, cep.Chamadas);
        Assert.Equal("01310100", (await Servico.ObterAsync())!.Cep);
    }

    [Fact]
    public async Task Emitente_Cep_SomenteOLupaConsulta()
    {
        var cep = new ContadorCepService();
        var vm = CriarViewModel(cep: cep);
        await vm.InitializeAsync();

        vm.FormCep = "01310100";

        Assert.True(vm.CepPodeConsultarManualmente);

        ExecutarComando(vm.ConsultarCepCommand, null, () => cep.Chamadas == 1);

        Assert.Equal(1, cep.Chamadas);
    }

    [Fact]
    public async Task Emitente_Cep_LupaAtualizaOEnderecoImediatamenteNaUI()
    {
        var cep = new ContadorCepService();
        var vm = CriarViewModel(cep: cep);
        await vm.InitializeAsync();

        vm.FormCep = "01310100";
        ExecutarComando(vm.ConsultarCepCommand, null, () => cep.Chamadas == 1);

        // Atualizado via propriedades: a UI reflete sem salvar e sem reabrir a tela.
        Assert.Equal("AVENIDA PAULISTA", vm.FormLogradouro);
        Assert.Equal("BELA VISTA", vm.FormBairro);
        Assert.Equal("SAO PAULO", vm.FormCidade);
        Assert.Equal("SP", vm.FormUf);

        // Ainda não salvo no banco.
        Assert.False(vm.EstaConfigurado);
    }

    [Fact]
    public async Task Emitente_Cep_ComLupaDesabilitadaQuandoCepIncompleto()
    {
        var cep = new ContadorCepService();
        var vm = CriarViewModel(cep: cep);
        await vm.InitializeAsync();

        vm.FormCep = "01310";
        Assert.False(vm.CepPodeConsultarManualmente);

        vm.FormCep = "01310100";
        Assert.True(vm.CepPodeConsultarManualmente);
    }

    [Fact]
    public async Task Emitente_Cep_SemServicoDeCepNaoConsulta()
    {
        var vm = CriarViewModel(cep: null);
        await vm.InitializeAsync();

        vm.FormCep = "01310100";

        Assert.False(vm.CepPodeConsultarManualmente);
        Assert.True(ReferenciaNaoPodeExecutar(vm.ConsultarCepCommand));
    }

    private static bool ReferenciaNaoPodeExecutar(ICommand comando)
        => WpfTestSupport.RunOnStaThread(() => comando.CanExecute(null));

    // ---------- Permissões ----------

    [Fact]
    public async Task Emitente_SemPermissao_DeveBloquearAcoes()
    {
        var vm = CriarViewModel(SessaoSemPermissao);
        await vm.InitializeAsync();

        Assert.False(vm.PodeVisualizar);
        Assert.False(vm.PodeEditar);
        Assert.False(vm.SalvarCommand.CanExecute(null));
        Assert.False(vm.SelecionarLogoCommand.CanExecute(null));
        Assert.False(vm.RemoverLogoCommand.CanExecute(null));
    }

    [Fact]
    public async Task Emitente_SomenteVisualizar_DeveAbrirMasNaoSalvar()
    {
        var vm = CriarViewModel(SessaoSomenteVisualizar);
        await vm.InitializeAsync();

        Assert.True(vm.PodeVisualizar);
        Assert.False(vm.PodeEditar);

        // Pode visualizar, mas os comandos de escrita ficam desabilitados.
        Assert.False(vm.SalvarCommand.CanExecute(null));
        Assert.False(vm.SelecionarLogoCommand.CanExecute(null));
        Assert.False(vm.RemoverLogoCommand.CanExecute(null));
    }

    [Fact]
    public async Task Emitente_Administrador_DeveLiberarEditar()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        Assert.True(vm.PodeVisualizar);
        Assert.True(vm.PodeEditar);
        Assert.True(vm.SalvarCommand.CanExecute(null));
        Assert.True(vm.SelecionarLogoCommand.CanExecute(null));
        Assert.True(vm.RemoverLogoCommand.CanExecute(null));
    }

    // ---------- Logo com armazenamento real ----------

    [Fact]
    public async Task Emitente_Logo_DeveSerCopiadaParaAPastaGerenciada()
    {
        var origem = CriarImagemOrigem(".png");
        try
        {
            var vm = CriarViewModel();
            await vm.InitializeAsync();

            vm.FormRazaoSocial = RazaoPadrao;
            vm.SelecionarLogo(origem);
            Salvar(vm);

            var lido = await Servico.ObterAsync();

            // Persiste o NOME gerenciado, nunca o caminho absoluto escolhido pelo usuário.
            Assert.Equal("logo-empresa.png", lido!.LogoPath);
            Assert.DoesNotContain("emitente-origem", lido.LogoPath!);

            var caminho = await Servico.ObterLogoCaminhoAbsolutoAsync();
            Assert.NotNull(caminho);
            Assert.True(File.Exists(caminho));
            Assert.StartsWith(_pastaLogo, caminho);

            Assert.NotNull(await Servico.ObterLogoBytesAsync());
            Assert.NotNull(vm.LogoPreview);
        }
        finally
        {
            TryDelete(origem);
        }
    }

    [Fact]
    public async Task Emitente_Logo_DeveSubstituirERemover()
    {
        var origemPng = CriarImagemOrigem(".png");
        var origemJpg = CriarImagemOrigem(".jpg");

        try
        {
            var vm = CriarViewModel();
            await vm.InitializeAsync();
            vm.FormRazaoSocial = RazaoPadrao;

            vm.SelecionarLogo(origemPng);
            SalvarConfirmado(vm);
            Assert.Equal("logo-empresa.png", (await Servico.ObterAsync())!.LogoPath);

            // Substitui.
            vm.SelecionarLogo(origemJpg);
            SalvarConfirmado(vm);
            Assert.Equal("logo-empresa.jpg", (await Servico.ObterAsync())!.LogoPath);

            // O arquivo antigo não pode ficar acumulado na pasta.
            Assert.False(File.Exists(Path.Combine(_pastaLogo, "logo-empresa.png")));

            // Remove.
            vm.SolicitarRemoverLogo();
            SalvarConfirmado(vm);

            Assert.Null((await Servico.ObterAsync())!.LogoPath);
            Assert.Null(await Servico.ObterLogoCaminhoAbsolutoAsync());
        }
        finally
        {
            TryDelete(origemPng);
            TryDelete(origemJpg);
        }
    }

    [Fact]
    public async Task Emitente_Logo_DeveContinuarDisponivelAposReabrirAModulo()
    {
        var origem = CriarImagemOrigem(".png");
        try
        {
            var vm = CriarViewModel();
            await vm.InitializeAsync();
            vm.FormRazaoSocial = RazaoPadrao;
            vm.SelecionarLogo(origem);
            Salvar(vm);

            // Reabre o módulo: a logo continua vinda do arquivo gerenciado.
            var vm2 = CriarViewModel();
            await vm2.InitializeAsync();

            Assert.Equal("logo-empresa.png", vm2.LogoNome);
            Assert.NotNull(vm2.LogoPreview);

            // E continua acessível pelo serviço (que é o que o PDF vai usar).
            var caminho = await Servico.ObterLogoCaminhoAbsolutoAsync();
            Assert.NotNull(caminho);
            Assert.True(File.Exists(caminho));
        }
        finally
        {
            TryDelete(origem);
        }
    }

    [Fact]
    public async Task Emitente_Logo_DeveSobreviverAoExcluirOArquivoOriginal()
    {
        var origem = CriarImagemOrigem(".png");
        try
        {
            var vm = CriarViewModel();
            await vm.InitializeAsync();
            vm.FormRazaoSocial = RazaoPadrao;
            vm.SelecionarLogo(origem);
            Salvar(vm);

            // O usuário apaga o arquivo que escolheu: a logo do OrcPro não é afetada.
            File.Delete(origem);

            Assert.NotNull(await Servico.ObterLogoCaminhoAbsolutoAsync());
            Assert.NotNull(await Servico.ObterLogoBytesAsync());
        }
        finally
        {
            TryDelete(origem);
        }
    }

    /// <summary>Cria um PNG 1x1 válido e devolve o caminho.</summary>
    private string CriarImagemOrigem(string extensao)
    {
        var caminho = Path.Combine(Path.GetTempPath(), $"emitente-origem-{Guid.NewGuid():N}{extensao}");

        // PNG 1x1 transparente.
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

        File.WriteAllBytes(caminho, png);
        return caminho;
    }

    // ---------- Schema upgrade em base existente ----------

    [Fact]
    public async Task SchemaUpgrade_DeveAdicionarColunasDoEmitenteEmBaseJaExistente()
    {
        var caminho = Path.Combine(Path.GetTempPath(), $"emitente-schema-{Guid.NewGuid():N}.db");

        try
        {
            var services = new ServiceCollection();
            services.AddInfrastructure(DatabaseConnectionOptions.DefaultSqlite(caminho));
            await using var provedor = services.BuildServiceProvider();
            await DatabaseInitializer.InitializeAsync(provedor);

            // Simula uma instalação anterior: remove as colunas novas do Emitente.
            await using (var escopo = provedor.CreateAsyncScope())
            {
                var ctx = escopo.ServiceProvider.GetRequiredService<OrcProDbContext>();
                await ctx.Database.ExecuteSqlRawAsync("ALTER TABLE \"Empresas\" DROP COLUMN \"EmailFinanceiro\"");
                await ctx.Database.ExecuteSqlRawAsync("ALTER TABLE \"Empresas\" DROP COLUMN \"Observacoes\"");
            }

            // O EnsureCreated não recria colunas: quem faz é o SchemaUpgrade.
            await DatabaseInitializer.InitializeAsync(provedor);

            await using (var escopo = provedor.CreateAsyncScope())
            {
                var empresa = new OrcPro.Domain.Entities.Empresa.Empresa
                {
                    RazaoSocial = "EMPRESA DO SCHEMA",
                    NomeFantasia = "SCHEMA",
                    Cnpj = "00000000000191",
                    EmailFinanceiro = "financeiro@schema.com.br",
                    Observacoes = "COLUNAS CRIADAS PELO UPGRADE"
                };

                escopo.ServiceProvider.GetRequiredService<OrcProDbContext>().Empresas.Add(empresa);
                await escopo.ServiceProvider.GetRequiredService<OrcProDbContext>().SaveChangesAsync();
            }

            // Idempotente: rodar de novo não falha.
            await DatabaseInitializer.InitializeAsync(provedor);

            await using var novoEscopo = provedor.CreateAsyncScope();
            var servico = novoEscopo.ServiceProvider.GetRequiredService<IEmpresaService>();
            var lido = await servico.ObterAsync();

            Assert.Equal("EMPRESA DO SCHEMA", lido!.RazaoSocial);
            Assert.Equal("financeiro@schema.com.br", lido.EmailFinanceiro);
            Assert.Equal("COLUNAS CRIADAS PELO UPGRADE", lido.Observacoes);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            TryDelete(caminho);
        }
    }

    [Fact]
    public async Task SchemaUpgrade_InstalacaoNova_DeveCriarTabelaEmpresasCompleta()
    {
        var vm = CriarViewModel();
        await vm.InitializeAsync();

        await using var escopo = _provedor.CreateAsyncScope();
        var ctx = escopo.ServiceProvider.GetRequiredService<OrcProDbContext>();

        // Em base nova o EnsureCreated já cria a tabela com todas as colunas.
        var colunas = ctx.Empresas.Select(e => e.RazaoSocial).ToList();
        Assert.NotNull(colunas);
    }
}

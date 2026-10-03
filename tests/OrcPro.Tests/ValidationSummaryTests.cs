using System.Collections.Generic;
using System.Linq;
using OrcPro.App.ViewModels;
using Xunit;

namespace OrcPro.Tests;

/// <summary>
/// Testes da infraestrutura compartilhada de validação do <see cref="ViewModelBase"/>
/// (resumo no topo do formulário, ordem dos campos inválidos e pedido de foco).
/// </summary>
public class ValidationSummaryTests
{
    /// <summary>Expostor dos métodos protegidos para teste.</summary>
    private sealed class ModeloValidacao : ViewModelBase
    {
        public void Definir(string campo, string mensagem) => DefinirErroValidacao(campo, mensagem);
        public void Limpar(string campo) => LimparErroValidacao(campo);
        public void LimparTudo() => LimparErrosValidacao();
        public void FocarPrimeiroInvalido() => SolicitarFocoPrimeiroCampoInvalido();

        public List<string> CamposNotificados { get; } = new();

        protected override void AoAlterarValidacao(string campo) => CamposNotificados.Add(campo);
    }

    [Fact]
    public void DefinirErroValidacao_DeveAdicionarAoResumo()
    {
        var vm = new ModeloValidacao();

        vm.Definir("Nome", "O Nome é obrigatório.");

        Assert.True(vm.CampoInvalido("Nome"));
        Assert.True(vm.TemResumoValidacao);
        Assert.Equal("O Nome é obrigatório.", Assert.Single(vm.ResumoValidacao));
        Assert.Equal("O Nome é obrigatório.", vm.ErrosValidacao["Nome"]);
        Assert.Equal("Nome", vm.PrimeiroCampoInvalido);
    }

    [Fact]
    public void DefinirErroValidacao_CamposDuplicados_NaoDeveDuplicarNoResumo()
    {
        var vm = new ModeloValidacao();

        vm.Definir("CEP", "Erro do CEP.");
        vm.Definir("CEP", "Erro atualizado do CEP.");

        Assert.Equal("Erro atualizado do CEP.", Assert.Single(vm.ResumoValidacao));
        Assert.Equal("Erro atualizado do CEP.", vm.ErrosValidacao["CEP"]);
    }

    [Fact]
    public void ResumoValidacao_DeveManterOrdemDeInsercao_DosCampos()
    {
        var vm = new ModeloValidacao();

        vm.Definir("CEP", "Erro do CEP.");
        vm.Definir("Nome", "Erro do Nome.");
        vm.Definir("Email", "Erro do Email.");

        Assert.Equal(new[] { "Erro do CEP.", "Erro do Nome.", "Erro do Email." }, vm.ResumoValidacao);
        Assert.Equal("CEP", vm.PrimeiroCampoInvalido);
    }

    [Fact]
    public void LimparErroValidacao_DeveRemoverDoResumo()
    {
        var vm = new ModeloValidacao();
        vm.Definir("Nome", "Erro do Nome.");
        vm.Definir("CEP", "Erro do CEP.");

        vm.Limpar("Nome");

        Assert.False(vm.CampoInvalido("Nome"));
        Assert.True(vm.CampoInvalido("CEP"));
        Assert.Equal("Erro do CEP.", Assert.Single(vm.ResumoValidacao));
        Assert.Equal("CEP", vm.PrimeiroCampoInvalido);
    }

    [Fact]
    public void LimparErroValidacao_CampoNaoRegistrado_NaoLancaErro()
    {
        var vm = new ModeloValidacao();

        vm.Limpar("Inexistente");

        Assert.False(vm.TemResumoValidacao);
        Assert.Null(vm.PrimeiroCampoInvalido);
    }

    [Fact]
    public void LimparErrosValidacao_DeveLimparResumoEDicionario()
    {
        var vm = new ModeloValidacao();
        vm.Definir("Nome", "Erro do Nome.");
        vm.Definir("CEP", "Erro do CEP.");

        vm.LimparTudo();

        Assert.False(vm.TemResumoValidacao);
        Assert.Empty(vm.ResumoValidacao);
        Assert.Empty(vm.ErrosValidacao);
        Assert.Null(vm.PrimeiroCampoInvalido);
        Assert.False(vm.CampoInvalido("Nome"));
    }

    [Fact]
    public void FocoCampoSolicitado_DeveDispararComPrimeiroCampoInvalido()
    {
        var vm = new ModeloValidacao();
        var campoRecebido = (string?)null;
        vm.FocoCampoSolicitado += (_, campo) => campoRecebido = campo;

        vm.Definir("CEP", "Erro do CEP.");
        vm.Definir("Nome", "Erro do Nome.");
        vm.FocarPrimeiroInvalido();

        Assert.Equal("CEP", campoRecebido);
    }

    [Fact]
    public void FocoCampoSolicitado_SemErros_NaoDispara()
    {
        var vm = new ModeloValidacao();
        var disparos = 0;
        vm.FocoCampoSolicitado += (_, _) => disparos++;

        vm.FocarPrimeiroInvalido();

        Assert.Equal(0, disparos);
    }

    [Fact]
    public void AoAlterarValidacao_DeveNotificarCadaAlteracao()
    {
        var vm = new ModeloValidacao();

        vm.Definir("CEP", "Erro do CEP.");
        vm.Limpar("CEP");
        vm.Definir("CEP", "Erro novo.");
        vm.LimparTudo();

        Assert.Equal(new[] { "CEP", "CEP", "CEP", "CEP" }, vm.CamposNotificados);
    }

    [Fact]
    public void ResumoValidacao_DeveRefletirExclusivamenteErrosAtivos()
    {
        var vm = new ModeloValidacao();

        vm.Definir("Nome", "Erro do Nome.");
        vm.Definir("Celular", "Erro do Celular.");
        vm.Limpar("Nome");

        Assert.True(vm.TemResumoValidacao);
        Assert.True(vm.ResumoValidacao.All(m => !string.IsNullOrWhiteSpace(m)));
        Assert.Equal(new[] { "Erro do Celular." }, vm.ResumoValidacao);
    }
}

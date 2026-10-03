using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using OrcPro.App.Controls;
using OrcPro.App.Views;
using Xunit;

namespace OrcPro.Tests;

/// <summary>
/// Smoke tests das telas de cadastro: instanciam as Views em thread STA para detectar
/// <c>XamlParseException</c> (recursos estáticos ausentes, XAML malformado) — falhas que
/// só apareceriam em execução manual — e validam a estrutura de validação de CEP
/// (resumo no topo, CepComLookupControl e Tags usadas pelo foco do primeiro campo inválido).
/// </summary>
[Collection(WpfTestSupport.Colecao)]
public class ViewsValidationSmokeTests
{
    [Fact]
    public void TecnicosView_Instantiation_DeveCriarSemException()
    {
        WpfTestSupport.RunOnStaThread(() =>
        {
            var view = new TecnicosView();
            Assert.NotNull(view);
        });
    }

    [Fact]
    public void ClientesView_Instantiation_DeveCriarSemException()
    {
        WpfTestSupport.RunOnStaThread(() =>
        {
            var view = new ClientesView();
            Assert.NotNull(view);
        });
    }

    [Fact]
    public void TecnicosView_DeveConterResumoDeValidacaoECampoDeCep()
    {
        WpfTestSupport.RunOnStaThread(() =>
        {
            var view = new TecnicosView();
            var arvore = ObterArvoreLogica(view).ToList();

            // Resumo de validação no topo do formulário (borda vermelha com os erros).
            var resumo = arvore.OfType<ItemsControl>()
                .FirstOrDefault(e => AutomationProperties.GetAutomationId(e) == "TecnicoResumoValidacao");
            Assert.NotNull(resumo);
            Assert.NotNull(resumo.Style);
            Assert.NotNull(resumo.Style.BasedOn);

            // Controle de CEP com lupa e estado de erro ligado ao ViewModel.
            var cep = Assert.Single(arvore.OfType<CepComLookupControl>());
            Assert.Equal("CEP", cep.Tag);
            Assert.Equal("TecnicoCepInput", cep.AutomationId);
            Assert.Equal("TecnicoCepLupa", cep.LupaAutomationId);

            // Tags usadas pelo FormFocusHelper para focar o primeiro campo inválido.
            var tags = arvore.OfType<FrameworkElement>()
                .Select(e => e.Tag as string)
                .Where(t => t is not null)
                .ToList();
            Assert.Contains("Nome", tags);
            Assert.Contains("Cpf", tags);
            Assert.Contains("CEP", tags);
        });
    }

    [Fact]
    public void ClientesView_DeveConterResumoDeValidacaoECampoDeCep()
    {
        WpfTestSupport.RunOnStaThread(() =>
        {
            var view = new ClientesView();
            var arvore = ObterArvoreLogica(view).ToList();

            var resumo = arvore.OfType<ItemsControl>()
                .FirstOrDefault(e => AutomationProperties.GetAutomationId(e) == "ClienteResumoValidacao");
            Assert.NotNull(resumo);
            Assert.NotNull(resumo.Style);
            Assert.NotNull(resumo.Style.BasedOn);

            var cep = Assert.Single(arvore.OfType<CepComLookupControl>());
            Assert.Equal("CEP", cep.Tag);

            var tags = arvore.OfType<FrameworkElement>()
                .Select(e => e.Tag as string)
                .Where(t => t is not null)
                .ToList();
            Assert.Contains("Nome", tags);
            Assert.Contains("CEP", tags);
        });
    }

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
}

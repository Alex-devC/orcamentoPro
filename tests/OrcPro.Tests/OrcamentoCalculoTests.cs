using OrcPro.Domain.Common.Calculos;
using OrcPro.Domain.Entities.Orcamento;
using Xunit;

namespace OrcPro.Tests;

/// <summary>
/// Testes do cálculo financeiro e das regras de status do orçamento.
/// O objetivo é garantir que o dinheiro nunca é calculado em <c>double</c> e que
/// transições absurdas são recusadas.
/// </summary>
public class OrcamentoCalculoTests
{
    // ---------- Cenário exigido: 2 x 100,00 + 150,00 - 50,00 = 300,00 ----------

    [Fact]
    public void Calculo_CenarioDeReferencia_DeveTotalizar300()
    {
        // Peça: quantidade 2, valor 100,00 => 200,00
        var peca = OrcamentoCalculo.TotalItem(2m, 100.00m, 0m);
        Assert.Equal(200.00m, peca);

        // Serviço: 1 hora x 150,00 => 150,00
        var servico = OrcamentoCalculo.TotalMaoDeObra(1m, 150.00m, 0m);
        Assert.Equal(150.00m, servico);

        var subtotal = peca + servico;
        Assert.Equal(350.00m, subtotal);

        var total = OrcamentoCalculo.TotalGeral(subtotal, 0m, 0m, 50.00m);
        Assert.Equal(300.00m, total);
    }

    [Fact]
    public void Entidade_ComCenarioDeReferencia_DeveGravarOsTotais()
    {
        var orcamento = new Orcamento
        {
            Itens = { new OrcamentoItem { Quantidade = 2m, PrecoUnitario = 100.00m } },
            MaosDeObra = { new OrcamentoMaoDeObra { QuantidadeHoras = 1m, ValorUnitario = 150.00m } },
            ValorDesconto = 50.00m
        };

        orcamento.RecalcularTotais();

        Assert.Equal(200.00m, orcamento.ValorTotalItens);
        Assert.Equal(150.00m, orcamento.ValorTotalMaoDeObra);
        Assert.Equal(300.00m, orcamento.ValorTotal);
    }

    // ---------- Valores decimais ----------

    [Fact]
    public void Calculo_ComValoresDecimais_DeveManterPrecisaoEmCentavos()
    {
        var item = OrcamentoCalculo.TotalItem(3m, 19.99m, 2.49m);
        Assert.Equal(57.48m, item); // 59,97 - 2,49

        // 1,5 x 1250,75 = 1876,125 exatos: o decimal não perde precisão como o double.
        var servico = OrcamentoCalculo.TotalMaoDeObra(1.5m, 1250.75m, 0m);
        Assert.Equal(1876.125m, servico);
    }

    [Fact]
    public void Calculo_ComDescontoParcial_DeveDescontar()
    {
        var item = OrcamentoCalculo.TotalItem(2m, 100m, 20m);
        Assert.Equal(180m, item);
    }

    [Fact]
    public void Calculo_DescontoMaiorQueBruto_NaoPodeGerarValorNegativo()
    {
        Assert.Equal(0m, OrcamentoCalculo.TotalItem(1m, 100m, 150m));
        Assert.Equal(0m, OrcamentoCalculo.TotalMaoDeObra(1m, 100m, 500m));
        Assert.Equal(0m, OrcamentoCalculo.TotalGeral(100m, 100m, 0m, 1000m));
    }

    [Fact]
    public void Calculo_QuantidadeZeroOuNegativa_NaoPodeGerarValor()
    {
        Assert.Equal(0m, OrcamentoCalculo.TotalItem(0m, 100m, 0m));
        Assert.Equal(0m, OrcamentoCalculo.TotalItem(-1m, 100m, 0m));
        Assert.Equal(0m, OrcamentoCalculo.TotalMaoDeObra(0m, 100m, 0m));
    }

    [Fact]
    public void Calculo_ValorNegativo_NaoPodeGerarValorNegativo()
    {
        Assert.Equal(0m, OrcamentoCalculo.TotalItem(1m, -100m, 0m));
        Assert.Equal(0m, OrcamentoCalculo.TotalGeral(0m, 0m, -50m, 0m));
    }

    [Fact]
    public void Calculo_ComAcrecimo_DeveSomarAntesDoDesconto()
    {
        // 200 + 150 + 20 (acréscimo) - 50 = 320
        var total = OrcamentoCalculo.TotalGeral(200m, 150m, 20m, 50m);
        Assert.Equal(320m, total);
    }

    [Fact]
    public void Calculo_UsaDecimal_NuncaDouble()
    {
        // 0,1 + 0,2 em double seria 0,30000000000000004; em decimal é exato.
        var total = OrcamentoCalculo.TotalGeral(0.1m, 0.2m, 0m, 0m);
        Assert.Equal(0.3m, total);
    }

    // ---------- Regras de status ----------

    [Theory]
    [InlineData(OrcamentoStatus.CodigoRascunho, OrcamentoStatus.CodigoAguardandoAprovacao, true)]
    [InlineData(OrcamentoStatus.CodigoAguardandoAprovacao, OrcamentoStatus.CodigoAprovado, true)]
    [InlineData(OrcamentoStatus.CodigoAprovado, OrcamentoStatus.CodigoEmExecucao, true)]
    [InlineData(OrcamentoStatus.CodigoEmExecucao, OrcamentoStatus.CodigoFinalizado, true)]
    [InlineData(OrcamentoStatus.CodigoRascunho, OrcamentoStatus.CodigoCancelado, true)]
    [InlineData(OrcamentoStatus.CodigoAguardandoAprovacao, OrcamentoStatus.CodigoRecusado, true)]
    public void Status_TransicoesValidas_DevemSerPermitidas(string atual, string novo, bool esperado)
        => Assert.Equal(esperado, OrcamentoStatus.PodeTransicionarPara(atual, novo));

    [Theory]
    [InlineData(OrcamentoStatus.CodigoAprovado, OrcamentoStatus.CodigoRascunho)]
    [InlineData(OrcamentoStatus.CodigoFinalizado, OrcamentoStatus.CodigoEmExecucao)]
    [InlineData(OrcamentoStatus.CodigoCancelado, OrcamentoStatus.CodigoRascunho)]
    [InlineData(OrcamentoStatus.CodigoRecusado, OrcamentoStatus.CodigoAguardandoAprovacao)]
    [InlineData(OrcamentoStatus.CodigoRascunho, OrcamentoStatus.CodigoFinalizado)]
    public void Status_TransicoesAbsurdas_DevemSerRecusadas(string atual, string novo)
        => Assert.False(OrcamentoStatus.PodeTransicionarPara(atual, novo));

    [Fact]
    public void Status_AlterarParaOMesmoStatus_NaoDeveSerPermitido()
        => Assert.False(OrcamentoStatus.PodeTransicionarPara(OrcamentoStatus.CodigoAprovado, OrcamentoStatus.CodigoAprovado));

    [Fact]
    public void Status_Desconhecidos_NaoDevemSerPermitidos()
    {
        Assert.False(OrcamentoStatus.PodeTransicionarPara(null, OrcamentoStatus.CodigoAprovado));
        Assert.False(OrcamentoStatus.PodeTransicionarPara(OrcamentoStatus.CodigoRascunho, null));
        Assert.False(OrcamentoStatus.PodeTransicionarPara("STATUS_DESCONHECIDO", OrcamentoStatus.CodigoAprovado));
    }

    [Theory]
    [InlineData(OrcamentoStatus.CodigoFinalizado, true)]
    [InlineData(OrcamentoStatus.CodigoCancelado, true)]
    [InlineData(OrcamentoStatus.CodigoRascunho, false)]
    [InlineData(OrcamentoStatus.CodigoAprovado, false)]
    public void Status_EhTerminal_DeveIdentificarEncerramento(string codigo, bool esperado)
        => Assert.Equal(esperado, OrcamentoStatus.EhTerminal(codigo));

    [Theory]
    [InlineData(OrcamentoStatus.CodigoRascunho, true)]
    [InlineData(OrcamentoStatus.CodigoAprovado, true)]
    [InlineData(OrcamentoStatus.CodigoFinalizado, false)]
    [InlineData(OrcamentoStatus.CodigoCancelado, false)]
    [InlineData(OrcamentoStatus.CodigoRecusado, false)]
    public void Status_PermiteEdicao_DeveBloquearEncerrados(string codigo, bool esperado)
        => Assert.Equal(esperado, OrcamentoStatus.PermiteEdicao(codigo));

    [Fact]
    public void Status_StatusIniciais_DevemCobrirOFluxoObrigatorio()
    {
        var codigos = OrcamentoStatus.CriarStatusIniciais().Select(s => s.Codigo).ToList();

        Assert.Contains(OrcamentoStatus.CodigoAguardandoAprovacao, codigos);
        Assert.Contains(OrcamentoStatus.CodigoAprovado, codigos);
        Assert.Contains(OrcamentoStatus.CodigoFinalizado, codigos);
        Assert.Contains(OrcamentoStatus.CodigoCancelado, codigos);
    }
}
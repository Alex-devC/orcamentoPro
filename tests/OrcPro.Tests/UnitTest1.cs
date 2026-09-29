using OrcPro.Domain.Entities.Orcamento;

namespace OrcPro.Tests;

public class OrcamentoDomainTests
{
    [Fact]
    public void RecalcularTotais_DeveCalcularItensEMaoDeObraComDescontos()
    {
        // Arrange
        var orcamento = new Orcamento
        {
            Numero = "0001/2026",
            ValorAcrescimo = 50m,
            ValorDesconto = 20m
        };

        orcamento.Itens.Add(new OrcamentoItem
        {
            NumeroItem = 1,
            CodigoPeca = "CAB-01",
            Descricao = "Cabo de Rede Cat6",
            Quantidade = 10,
            PrecoUnitario = 5.0m,
            ValorDesconto = 5.0m
        });

        orcamento.MaosDeObra.Add(new OrcamentoMaoDeObra
        {
            NumeroItem = 1,
            Descricao = "Instalação de Rede",
            QuantidadeHoras = 2,
            ValorUnitario = 100m,
            ValorDesconto = 10m
        });

        // Act
        orcamento.RecalcularTotais();

        // Assert
        // Item: 10 * 5 - 5 = 45
        Assert.Equal(45m, orcamento.ValorTotalItens);
        // MaoDeObra: 2 * 100 - 10 = 190
        Assert.Equal(190m, orcamento.ValorTotalMaoDeObra);
        // Total: 45 + 190 + 50 (acrescimo) - 20 (desconto) = 265
        Assert.Equal(265m, orcamento.ValorTotal);
    }

    [Fact]
    public void CriarStatusIniciais_DeveConterOsSeteStatusObrigatorios()
    {
        // Act
        var statusList = OrcamentoStatus.CriarStatusIniciais().ToList();

        // Assert
        Assert.Equal(7, statusList.Count);
        Assert.Contains(statusList, s => s.Codigo == OrcamentoStatus.CodigoRascunho);
        Assert.Contains(statusList, s => s.Codigo == OrcamentoStatus.CodigoAguardandoAprovacao);
        Assert.Contains(statusList, s => s.Codigo == OrcamentoStatus.CodigoAprovado);
        Assert.Contains(statusList, s => s.Codigo == OrcamentoStatus.CodigoEmExecucao);
        Assert.Contains(statusList, s => s.Codigo == OrcamentoStatus.CodigoFinalizado);
        Assert.Contains(statusList, s => s.Codigo == OrcamentoStatus.CodigoCancelado);
        Assert.Contains(statusList, s => s.Codigo == OrcamentoStatus.CodigoRecusado);
    }
}

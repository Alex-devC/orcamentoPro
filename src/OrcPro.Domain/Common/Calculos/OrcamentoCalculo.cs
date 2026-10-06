namespace OrcPro.Domain.Common.Calculos;

/// <summary>
/// Cálculos financeiros do orçamento em uma única fonte de verdade.
/// </summary>
/// <remarks>
/// Vive no Domain porque é regra de negócio: as entidades (<c>Orcamento</c>,
/// <c>OrcamentoItem</c>, <c>OrcamentoMaoDeObra</c>) e a camada de apresentação usam
/// exatamente estes métodos, eliminando lógica duplicada. Todos os valores são
/// <see cref="decimal"/> — dinheiro nunca em <c>double</c>.
/// </remarks>
public static class OrcamentoCalculo
{
    /// <summary>
    /// Total de um item: (Quantidade × Valor Unitário) − Desconto.
    /// Desconto maior que o bruto zera o total em vez de gerar valor negativo.
    /// </summary>
    public static decimal TotalItem(decimal quantidade, decimal precoUnitario, decimal desconto)
    {
        if (quantidade <= 0 || precoUnitario < 0)
            return 0m;

        var descontoSeguro = desconto < 0 ? 0m : desconto;
        var bruto = quantidade * precoUnitario;

        return bruto >= descontoSeguro ? bruto - descontoSeguro : 0m;
    }

    /// <summary>
    /// Total de uma linha de serviço: (Horas × Valor Unitário) − Desconto.
    /// </summary>
    public static decimal TotalMaoDeObra(decimal horas, decimal valorUnitario, decimal desconto)
    {
        if (horas <= 0 || valorUnitario < 0)
            return 0m;

        var descontoSeguro = desconto < 0 ? 0m : desconto;
        var bruto = horas * valorUnitario;

        return bruto >= descontoSeguro ? bruto - descontoSeguro : 0m;
    }

    /// <summary>
    /// Total geral: Subtotal Peças + Subtotal Mão de Obra + Acréscimo − Desconto.
    /// Nunca devolve valor negativo.
    /// </summary>
    public static decimal TotalGeral(decimal subtotalPecas, decimal subtotalMaoDeObra, decimal acrescimo, decimal desconto)
    {
        var acrescimoSeguro = acrescimo < 0 ? 0m : acrescimo;
        var descontoSeguro = desconto < 0 ? 0m : desconto;

        var subtotal = subtotalPecas + subtotalMaoDeObra + acrescimoSeguro;

        return subtotal >= descontoSeguro ? subtotal - descontoSeguro : 0m;
    }
}
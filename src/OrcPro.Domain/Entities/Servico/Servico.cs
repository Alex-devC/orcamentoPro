using OrcPro.Domain.Common;

namespace OrcPro.Domain.Entities.Servico;

/// <summary>
/// Serviço / mão de obra prestado pela empresa. É o cadastro que o Orçamento irá
/// consultar futuramente para trazer código, descrição, valor, unidade e tempo estimado.
///
/// <para>A entidade não declara navegação para o Orçamento de propósito: o módulo de
/// Orçamentos ainda não existe e um vínculo fictício seria apenas ruído no modelo.
/// A proteção contra exclusão de serviços já utilizados fica no repositório
/// (<c>CountVinculosOrcamentoAsync</c>), que é o único ponto a ser preenchido quando
/// os itens de serviço do orçamento existirem.</para>
/// </summary>
public class Servico : BaseEntity
{
    /// <summary>Código sequencial gerado pelo sistema (SRV-0001). Único e imutável.</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Descrição do serviço. Obrigatória.</summary>
    public string Descricao { get; set; } = string.Empty;

    /// <summary>Agrupamento do serviço (ex.: INSTALAÇÃO, MANUTENÇÃO).</summary>
    public string? Categoria { get; set; }

    /// <summary>Valor de venda do serviço.</summary>
    public decimal Valor { get; set; }

    /// <summary>Unidade de cobrança (UN, HR, DIA, ...).</summary>
    public string Unidade { get; set; } = "UN";

    /// <summary>Tempo estimado de execução, em horas.</summary>
    public decimal TempoEstimado { get; set; } = 1m;

    public string? Observacoes { get; set; }

    public bool Ativo { get; set; } = true;
}
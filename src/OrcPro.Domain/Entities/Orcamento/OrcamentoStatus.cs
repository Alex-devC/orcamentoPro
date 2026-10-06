using OrcPro.Domain.Common;

namespace OrcPro.Domain.Entities.Orcamento;

public class OrcamentoStatus : BaseEntity
{
    // Códigos constantes para identificação semântica no sistema
    public const string CodigoRascunho = "RASCUNHO";
    public const string CodigoAguardandoAprovacao = "AGUARDANDO_APROVACAO";
    public const string CodigoAprovado = "APROVADO";
    public const string CodigoEmExecucao = "EM_EXECUCAO";
    public const string CodigoFinalizado = "FINALIZADO";
    public const string CodigoCancelado = "CANCELADO";
    public const string CodigoRecusado = "RECUSADO";

    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string? CorHex { get; set; }
    public int Ordem { get; set; }
    public bool Ativo { get; set; } = true;

    public ICollection<Orcamento> Orcamentos { get; set; } = new List<Orcamento>();

    public static IEnumerable<OrcamentoStatus> CriarStatusIniciais()
    {
        return new List<OrcamentoStatus>
        {
            new() { Id = 1, Codigo = CodigoRascunho, Nome = "Rascunho", CorHex = "#ECEFF3", Ordem = 1 },
            new() { Id = 2, Codigo = CodigoAguardandoAprovacao, Nome = "Aguardando aprovação", CorHex = "#FEF3C7", Ordem = 2 },
            new() { Id = 3, Codigo = CodigoAprovado, Nome = "Aprovado", CorHex = "#E0F2FE", Ordem = 3 },
            new() { Id = 4, Codigo = CodigoEmExecucao, Nome = "Em execução", CorHex = "#E0E7FF", Ordem = 4 },
            new() { Id = 5, Codigo = CodigoFinalizado, Nome = "Finalizado", CorHex = "#DCFCE7", Ordem = 5 },
            new() { Id = 6, Codigo = CodigoCancelado, Nome = "Cancelado", CorHex = "#FEE2E2", Ordem = 6 },
            new() { Id = 7, Codigo = CodigoRecusado, Nome = "Recusado", CorHex = "#FFDAD6", Ordem = 7 }
        };
    }

    /// <summary>
    /// Transições permitidas entre status, por código.
    /// </summary>
    /// <remarks>
    /// Regras de negócio do fluxo do orçamento:
    /// <list type="bullet">
    /// <item>Finalizado e cancelado são TERMINAIS — não voltam a outro estado.</item>
    /// <item>Rascunho e Aguardando aprovação podem ser cancelados ou recusados.</item>
    /// <item>Um orçamento recusado ou cancelado não pode ser reativado.</item>
    /// <item>Não é permitido "desfazer" a aprovação voltando para rascunho.</item>
    /// </list>
    /// Fluxo normal: RASCUNHO → AGUARDANDO_APROVACAO → APROVADO → EM_EXECUCAO → FINALIZADO,
    /// com CANCELADO/RECUSADO disponíveis enquanto o orçamento não for final.
    /// </remarks>
    private static readonly Dictionary<string, HashSet<string>> TransicoesPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        [CodigoRascunho] = new(StringComparer.OrdinalIgnoreCase)
        {
            CodigoAguardandoAprovacao, CodigoCancelado
        },
        [CodigoAguardandoAprovacao] = new(StringComparer.OrdinalIgnoreCase)
        {
            CodigoAprovado, CodigoRecusado, CodigoCancelado
        },
        [CodigoAprovado] = new(StringComparer.OrdinalIgnoreCase)
        {
            CodigoEmExecucao, CodigoCancelado
        },
        [CodigoEmExecucao] = new(StringComparer.OrdinalIgnoreCase)
        {
            CodigoFinalizado, CodigoCancelado
        },
        [CodigoFinalizado] = new(StringComparer.OrdinalIgnoreCase),
        [CodigoCancelado] = new(StringComparer.OrdinalIgnoreCase),
        [CodigoRecusado] = new(StringComparer.OrdinalIgnoreCase)
    };

    /// <summary>
    /// Indica se o orçamento pode sair de <paramref name="codigoAtual"/> para
    /// <paramref name="codigoNovo"/>.
    /// </summary>
    public static bool PodeTransicionarPara(string? codigoAtual, string? codigoNovo)
    {
        if (string.IsNullOrWhiteSpace(codigoAtual) || string.IsNullOrWhiteSpace(codigoNovo))
            return false;

        if (string.Equals(codigoAtual, codigoNovo, StringComparison.OrdinalIgnoreCase))
            return false;

        return TransicoesPermitidas.TryGetValue(codigoAtual, out var destinos)
            && destinos.Contains(codigoNovo);
    }

    /// <summary>Mensagem explicativa para a transição recusada.</summary>
    public static string MensagemTransicaoInvalida(string nomeAtual, string nomeNovo)
        => $"Não é possível alterar o status de '{nomeAtual}' para '{nomeNovo}'.";

    /// <summary>Indica se o orçamento está encerrado (não aceita mais alterações nem exclusão).</summary>
    public static bool EhTerminal(string? codigo)
        => string.Equals(codigo, CodigoFinalizado, StringComparison.OrdinalIgnoreCase)
        || string.Equals(codigo, CodigoCancelado, StringComparison.OrdinalIgnoreCase);

    /// <summary>Indica se o orçamento pode ser editado (exclui finalizados, cancelados e recusados).</summary>
    public static bool PermiteEdicao(string? codigo)
        => !EhTerminal(codigo) && !string.Equals(codigo, CodigoRecusado, StringComparison.OrdinalIgnoreCase);
}

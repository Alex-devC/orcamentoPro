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
}

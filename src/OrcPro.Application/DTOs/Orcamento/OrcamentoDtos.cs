namespace OrcPro.Application.DTOs.Orcamento;

public class OrcamentoStatusDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? CorHex { get; set; }
    public int Ordem { get; set; }
}

public class OrcamentoItemDto
{
    public int Id { get; set; }
    public int OrcamentoId { get; set; }
    public int? PecaId { get; set; }
    public int NumeroItem { get; set; }
    public string CodigoPeca { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string UnidadeMedida { get; set; } = "UN";
    public decimal Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal ValorDesconto { get; set; }
    public decimal ValorTotal { get; set; }
}

public class OrcamentoMaoDeObraTecnicoDto
{
    public int Id { get; set; }
    public int TecnicoId { get; set; }
    public string TecnicoNome { get; set; } = string.Empty;
    public string? Funcao { get; set; }
    public string? Observacoes { get; set; }
}

public class OrcamentoMaoDeObraDto
{
    public int Id { get; set; }
    public int OrcamentoId { get; set; }
    public int NumeroItem { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal QuantidadeHoras { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal ValorDesconto { get; set; }
    public decimal ValorTotal { get; set; }
    public string? Observacoes { get; set; }
    public List<OrcamentoMaoDeObraTecnicoDto> Tecnicos { get; set; } = new();
}

public class OrcamentoTecnicoDto
{
    public int Id { get; set; }
    public int OrcamentoId { get; set; }
    public int TecnicoId { get; set; }
    public string TecnicoNome { get; set; } = string.Empty;
    public string? Funcao { get; set; }
    public string? Observacoes { get; set; }
}

public class OrcamentoHistoricoDto
{
    public int Id { get; set; }
    public DateTime DataRegistro { get; set; }
    public string? NomeUsuario { get; set; }
    public string Acao { get; set; } = string.Empty;
    public string? StatusAnterior { get; set; }
    public string? StatusNovo { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string? Detalhes { get; set; }
}

public class OrcamentoResumoDto
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public DateTime DataEmissao { get; set; }
    public DateTime DataValidade { get; set; }
    public int ClienteId { get; set; }
    public string ClienteNome { get; set; } = string.Empty;
    public string ClienteCpfCnpj { get; set; } = string.Empty;
    public string ClienteCidade { get; set; } = string.Empty;
    public string ClienteUf { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public string StatusCodigo { get; set; } = string.Empty;
    public string StatusNome { get; set; } = string.Empty;
    public string? StatusCorHex { get; set; }
    public decimal ValorTotal { get; set; }
    public string VendedorNome { get; set; } = string.Empty;
}

public class OrcamentoDto
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public int Ano { get; set; }
    public int Sequencial { get; set; }
    public DateTime DataEmissao { get; set; }
    public int DiasValidade { get; set; }
    public DateTime DataValidade { get; set; }

    public int ClienteId { get; set; }
    public string ClienteNome { get; set; } = string.Empty;
    public string ClienteCpfCnpj { get; set; } = string.Empty;
    public string? ClienteTelefone { get; set; }
    public string? ClienteCelular { get; set; }
    public string? ClienteEmail { get; set; }
    public string? ClienteEnderecoCompleto { get; set; }

    public int EmpresaId { get; set; }
    public string EmpresaRazaoSocial { get; set; } = string.Empty;
    public string? EmpresaLogoPath { get; set; }

    public int UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = string.Empty;

    public int StatusId { get; set; }
    public string StatusCodigo { get; set; } = string.Empty;
    public string StatusNome { get; set; } = string.Empty;
    public string? StatusCorHex { get; set; }

    public decimal ValorTotalItens { get; set; }
    public decimal ValorTotalMaoDeObra { get; set; }
    public decimal ValorDesconto { get; set; }
    public decimal ValorAcrescimo { get; set; }
    public decimal ValorTotal { get; set; }

    public string? CondicoesPagamento { get; set; }
    public string? PrazoEntrega { get; set; }
    public string? Garantia { get; set; }
    public string? Observacoes { get; set; }
    public string? ObservacoesInternas { get; set; }

    public List<OrcamentoItemDto> Itens { get; set; } = new();
    public List<OrcamentoMaoDeObraDto> MaosDeObra { get; set; } = new();
    public List<OrcamentoTecnicoDto> Tecnicos { get; set; } = new();
    public List<OrcamentoHistoricoDto> Historicos { get; set; } = new();
}

public class CriarOrcamentoDto
{
    public int ClienteId { get; set; }
    public int EmpresaId { get; set; }
    public int UsuarioId { get; set; }
    public int DiasValidade { get; set; } = 15;
    public decimal ValorAcrescimo { get; set; }
    public decimal ValorDesconto { get; set; }
    public string? CondicoesPagamento { get; set; }
    public string? PrazoEntrega { get; set; }
    public string? Garantia { get; set; }
    public string? Observacoes { get; set; }
    public string? ObservacoesInternas { get; set; }

    public List<AdicionarItemDto> ItensIniciais { get; set; } = new();
    public List<AdicionarMaoDeObraDto> MaosDeObraIniciais { get; set; } = new();
    public List<AssociarTecnicoDto> TecnicosIniciais { get; set; } = new();
}

public class AtualizarOrcamentoDto
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public int DiasValidade { get; set; }
    public decimal ValorAcrescimo { get; set; }
    public decimal ValorDesconto { get; set; }
    public string? CondicoesPagamento { get; set; }
    public string? PrazoEntrega { get; set; }
    public string? Garantia { get; set; }
    public string? Observacoes { get; set; }
    public string? ObservacoesInternas { get; set; }
}

public class AdicionarItemDto
{
    public int? PecaId { get; set; }
    public string CodigoPeca { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string UnidadeMedida { get; set; } = "UN";
    public decimal Quantidade { get; set; } = 1;
    public decimal PrecoUnitario { get; set; }
    public decimal ValorDesconto { get; set; }
}

public class AdicionarMaoDeObraDto
{
    public string Descricao { get; set; } = string.Empty;
    public decimal QuantidadeHoras { get; set; } = 1;
    public decimal ValorUnitario { get; set; }
    public decimal ValorDesconto { get; set; }
    public string? Observacoes { get; set; }
    public List<int>? TecnicoIds { get; set; }
}

public class AssociarTecnicoDto
{
    public int TecnicoId { get; set; }
    public string? Funcao { get; set; }
    public string? Observacoes { get; set; }
}

public class AlterarStatusOrcamentoDto
{
    public int OrcamentoId { get; set; }
    public int NovoStatusId { get; set; }
    public int UsuarioId { get; set; }
    public string? ObservacaoMotivo { get; set; }
}

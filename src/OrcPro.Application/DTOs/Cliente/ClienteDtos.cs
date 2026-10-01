namespace OrcPro.Application.DTOs.Cliente;

public class ClienteDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string TipoPessoa { get; set; } = "PJ";
    public string NomeRazaoSocial { get; set; } = string.Empty;
    public string? NomeFantasia { get; set; }

    /// <summary>Somente dígitos quando informado (a máscara é aplicada na exibição).</summary>
    public string? CpfCnpj { get; set; }
    public string? RgIe { get; set; }
    public string? Telefone { get; set; }
    public string Celular { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? EmailFinanceiro { get; set; }
    public string? Cep { get; set; }
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }
    public string? Observacoes { get; set; }
    public bool Ativo { get; set; }
    public DateTime DataCriacao { get; set; }

    /// <summary>Orçamentos vinculados: define se o cliente pode ser excluído ou apenas inativado.</summary>
    public int QuantidadeOrcamentos { get; set; }
}

public class CriarClienteDto
{
    public string? Codigo { get; set; }
    public string TipoPessoa { get; set; } = "PJ";
    public string NomeRazaoSocial { get; set; } = string.Empty;
    public string? NomeFantasia { get; set; }

    /// <summary>Opcional: quando informado, é validado (dígitos verificadores) e único.</summary>
    public string? CpfCnpj { get; set; }
    public string? RgIe { get; set; }
    public string? Telefone { get; set; }
    public string Celular { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? EmailFinanceiro { get; set; }
    public string? Cep { get; set; }
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }
    public string? Observacoes { get; set; }
    public bool Ativo { get; set; } = true;
}

public class AtualizarClienteDto : CriarClienteDto
{
    public int Id { get; set; }
}

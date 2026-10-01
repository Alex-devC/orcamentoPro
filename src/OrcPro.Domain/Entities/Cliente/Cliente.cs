using OrcPro.Domain.Common;
using OrcPro.Domain.Entities.Orcamento;

namespace OrcPro.Domain.Entities.Cliente;

public class Cliente : BaseEntity
{
    public string Codigo { get; set; } = string.Empty;
    public string TipoPessoa { get; set; } = "PJ"; // "PJ" ou "PF"
    public string NomeRazaoSocial { get; set; } = string.Empty;
    public string? NomeFantasia { get; set; }

    /// <summary>CPF/CNPJ com apenas dígitos quando informado (opcional no cadastro).</summary>
    public string? CpfCnpj { get; set; }
    public string? RgIe { get; set; }
    public string? Telefone { get; set; }
    public string Celular { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? EmailFinanceiro { get; set; }

    // Endereço
    public string? Cep { get; set; }
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }

    public string? Observacoes { get; set; }
    public bool Ativo { get; set; } = true;

    public ICollection<Orcamento.Orcamento> Orcamentos { get; set; } = new List<Orcamento.Orcamento>();
}

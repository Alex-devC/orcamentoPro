using OrcPro.Domain.Common;
using OrcPro.Domain.Entities.Orcamento;

namespace OrcPro.Domain.Entities.Empresa;

public class Empresa : BaseEntity
{
    public string RazaoSocial { get; set; } = string.Empty;
    public string NomeFantasia { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public string? InscricaoEstadual { get; set; }
    public string? InscricaoMunicipal { get; set; }
    public string? Telefone { get; set; }
    public string? Celular { get; set; }
    public string? Email { get; set; }
    public string? EmailFinanceiro { get; set; }
    public string? Website { get; set; }

    // Endereço
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }
    public string? Cep { get; set; }

    // Caminho / referência do logotipo da empresa.
    // Persiste apenas o NOME do arquivo mantido pela aplicação (ver
    // IEmpresaLogoStorage); nunca o caminho absoluto da origem escolhida pelo usuário,
    // para que a logo sobreviva à mudança de pasta do executável e à exclusão do
    // arquivo original.
    public string? LogoPath { get; set; }

    public string? Observacoes { get; set; }

    public bool Ativo { get; set; } = true;

    public ICollection<Orcamento.Orcamento> Orcamentos { get; set; } = new List<Orcamento.Orcamento>();
}

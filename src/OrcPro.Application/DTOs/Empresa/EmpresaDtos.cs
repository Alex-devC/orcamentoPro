namespace OrcPro.Application.DTOs.Empresa;

/// <summary>
/// Ficha do Emitente / Minha Empresa. É o registro único da instalação e a fonte de dados
/// para Orçamentos, PDF e relatórios futuros.
/// </summary>
public class EmpresaDto
{
    public int Id { get; set; }
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
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }
    public string? Cep { get; set; }

    /// <summary>Nome do arquivo de logo mantido pela aplicação (não é caminho absoluto).</summary>
    public string? LogoPath { get; set; }

    public string? Observacoes { get; set; }
    public bool Ativo { get; set; }
}

/// <summary>
/// Dados do formulário de Minha Empresa. O <c>Id</c> é 0 enquanto o emitente ainda não
/// foi cadastrado; <see cref="LogoPath"/> recebe o caminho absoluto do arquivo escolhido
/// apenas na memória do formulário — o serviço copia a imagem e persiste o nome gerenciado.
/// </summary>
public class SalvarEmpresaDto
{
    public int Id { get; set; }
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
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }
    public string? Cep { get; set; }

    /// <summary>Caminho absoluto da imagem escolhida, usado na importação. Null = manter a logo atual.</summary>
    public string? NovoLogoCaminhoOrigem { get; set; }

    /// <summary>Quando true, a logo é removida ao salvar.</summary>
    public bool RemoverLogo { get; set; }

    public string? Observacoes { get; set; }
}
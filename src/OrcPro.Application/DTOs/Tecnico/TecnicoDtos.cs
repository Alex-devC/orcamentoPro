namespace OrcPro.Application.DTOs.Tecnico;

public class TecnicoDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Cpf { get; set; }
    public string? Telefone { get; set; }
    public string? Celular { get; set; }
    public string? Email { get; set; }
    public string? Especialidade { get; set; }
    public string? RegistroProfissional { get; set; }
    public string? Observacoes { get; set; }
    public bool Ativo { get; set; }
    public DateTime DataCriacao { get; set; }

    public string? Rg { get; set; }

    // Endereço
    public string? Cep { get; set; }
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }

    /// <summary>Orçamentos vinculados: define se o técnico pode ser excluído ou apenas inativado.</summary>
    public int QuantidadeOrcamentos { get; set; }
}

public class CriarTecnicoDto
{
    public string? Codigo { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Cpf { get; set; }
    public string? Rg { get; set; }
    public string? Telefone { get; set; }
    public string? Celular { get; set; }
    public string? Email { get; set; }
    public string? Especialidade { get; set; }
    public string? RegistroProfissional { get; set; }
    public string? Observacoes { get; set; }
    public bool Ativo { get; set; } = true;

    // Endereço
    public string? Cep { get; set; }
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }
}

public class AtualizarTecnicoDto : CriarTecnicoDto
{
    public int Id { get; set; }
}

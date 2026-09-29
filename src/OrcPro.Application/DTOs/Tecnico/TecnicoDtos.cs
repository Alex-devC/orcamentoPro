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
}

public class CriarTecnicoDto
{
    public string? Codigo { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Cpf { get; set; }
    public string? Telefone { get; set; }
    public string? Celular { get; set; }
    public string? Email { get; set; }
    public string? Especialidade { get; set; }
    public string? RegistroProfissional { get; set; }
    public string? Observacoes { get; set; }
    public bool Ativo { get; set; } = true;
}

public class AtualizarTecnicoDto : CriarTecnicoDto
{
    public int Id { get; set; }
}

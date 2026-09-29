using OrcPro.Domain.Entities.Cliente;
using OrcPro.Domain.Entities.Configuracao;
using OrcPro.Domain.Entities.Empresa;
using OrcPro.Domain.Entities.Peca;
using OrcPro.Domain.Entities.Tecnico;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OrcPro.Infrastructure.Persistence.Configurations;

public class EmpresaConfiguration : IEntityTypeConfiguration<Empresa>
{
    public void Configure(EntityTypeBuilder<Empresa> builder)
    {
        builder.ToTable("Empresas");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.RazaoSocial).IsRequired().HasMaxLength(150);
        builder.Property(e => e.NomeFantasia).IsRequired().HasMaxLength(150);
        builder.Property(e => e.Cnpj).IsRequired().HasMaxLength(20);
        builder.Property(e => e.InscricaoEstadual).HasMaxLength(30);
        builder.Property(e => e.InscricaoMunicipal).HasMaxLength(30);
        builder.Property(e => e.Telefone).HasMaxLength(25);
        builder.Property(e => e.Celular).HasMaxLength(25);
        builder.Property(e => e.Email).HasMaxLength(120);
        builder.Property(e => e.Website).HasMaxLength(150);
        builder.Property(e => e.LogoPath).HasMaxLength(350);

        builder.Property(e => e.Logradouro).HasMaxLength(150);
        builder.Property(e => e.Numero).HasMaxLength(20);
        builder.Property(e => e.Complemento).HasMaxLength(80);
        builder.Property(e => e.Bairro).HasMaxLength(80);
        builder.Property(e => e.Cidade).HasMaxLength(80);
        builder.Property(e => e.Uf).HasMaxLength(2);
        builder.Property(e => e.Cep).HasMaxLength(10);
    }
}

public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("Clientes");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Codigo).IsRequired().HasMaxLength(30);
        builder.HasIndex(c => c.Codigo).IsUnique();

        builder.Property(c => c.TipoPessoa).IsRequired().HasMaxLength(2);
        builder.Property(c => c.NomeRazaoSocial).IsRequired().HasMaxLength(150);
        builder.Property(c => c.NomeFantasia).HasMaxLength(150);
        builder.Property(c => c.CpfCnpj).IsRequired().HasMaxLength(20);
        builder.HasIndex(c => c.CpfCnpj);

        builder.Property(c => c.RgIe).HasMaxLength(30);
        builder.Property(c => c.Telefone).HasMaxLength(25);
        builder.Property(c => c.Celular).IsRequired().HasMaxLength(25);
        builder.Property(c => c.Email).IsRequired().HasMaxLength(120);
        builder.Property(c => c.EmailFinanceiro).HasMaxLength(120);

        builder.Property(c => c.Cep).HasMaxLength(10);
        builder.Property(c => c.Logradouro).HasMaxLength(150);
        builder.Property(c => c.Numero).HasMaxLength(20);
        builder.Property(c => c.Complemento).HasMaxLength(80);
        builder.Property(c => c.Bairro).HasMaxLength(80);
        builder.Property(c => c.Cidade).HasMaxLength(80);
        builder.Property(c => c.Uf).HasMaxLength(2);
        builder.Property(c => c.Observacoes).HasMaxLength(1000);
    }
}

public class TecnicoConfiguration : IEntityTypeConfiguration<Tecnico>
{
    public void Configure(EntityTypeBuilder<Tecnico> builder)
    {
        builder.ToTable("Tecnicos");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Codigo).IsRequired().HasMaxLength(30);
        builder.HasIndex(t => t.Codigo).IsUnique();

        builder.Property(t => t.Nome).IsRequired().HasMaxLength(120);
        builder.Property(t => t.Cpf).HasMaxLength(20);
        builder.Property(t => t.Telefone).HasMaxLength(25);
        builder.Property(t => t.Celular).HasMaxLength(25);
        builder.Property(t => t.Email).HasMaxLength(120);
        builder.Property(t => t.Especialidade).HasMaxLength(100);
        builder.Property(t => t.RegistroProfissional).HasMaxLength(50);
        builder.Property(t => t.Observacoes).HasMaxLength(1000);
    }
}

public class PecaConfiguration : IEntityTypeConfiguration<Peca>
{
    public void Configure(EntityTypeBuilder<Peca> builder)
    {
        builder.ToTable("Pecas");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Codigo).IsRequired().HasMaxLength(50);
        builder.HasIndex(p => p.Codigo).IsUnique();

        builder.Property(p => p.Descricao).IsRequired().HasMaxLength(200);
        builder.Property(p => p.UnidadeMedida).IsRequired().HasMaxLength(10);

        builder.Property(p => p.PrecoCusto).HasPrecision(18, 2);
        builder.Property(p => p.PrecoVenda).HasPrecision(18, 2);
        builder.Property(p => p.EstoqueAtual).HasPrecision(18, 3);
        builder.Property(p => p.EstoqueMinimo).HasPrecision(18, 3);

        builder.Property(p => p.Observacoes).HasMaxLength(1000);
    }
}

public class ConfiguracaoConfiguration : IEntityTypeConfiguration<Configuracao>
{
    public void Configure(EntityTypeBuilder<Configuracao> builder)
    {
        builder.ToTable("Configuracoes");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Chave).IsRequired().HasMaxLength(100);
        builder.HasIndex(c => c.Chave).IsUnique();

        builder.Property(c => c.Valor).IsRequired().HasMaxLength(2000);
        builder.Property(c => c.Descricao).HasMaxLength(250);
        builder.Property(c => c.Categoria).IsRequired().HasMaxLength(60);
    }
}

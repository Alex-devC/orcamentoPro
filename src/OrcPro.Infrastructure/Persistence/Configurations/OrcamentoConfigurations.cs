using OrcPro.Domain.Entities.Orcamento;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OrcPro.Infrastructure.Persistence.Configurations;

public class OrcamentoConfiguration : IEntityTypeConfiguration<Orcamento>
{
    public void Configure(EntityTypeBuilder<Orcamento> builder)
    {
        builder.ToTable("Orcamentos");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Numero).IsRequired().HasMaxLength(30);
        builder.HasIndex(o => o.Numero).IsUnique();

        builder.Property(o => o.ValorTotalItens).HasPrecision(18, 2);
        builder.Property(o => o.ValorTotalMaoDeObra).HasPrecision(18, 2);
        builder.Property(o => o.ValorDesconto).HasPrecision(18, 2);
        builder.Property(o => o.ValorAcrescimo).HasPrecision(18, 2);
        builder.Property(o => o.ValorTotal).HasPrecision(18, 2);

        builder.Property(o => o.CondicoesPagamento).HasMaxLength(250);
        builder.Property(o => o.PrazoEntrega).HasMaxLength(100);
        builder.Property(o => o.Garantia).HasMaxLength(150);
        builder.Property(o => o.Observacoes).HasMaxLength(2000);
        builder.Property(o => o.ObservacoesInternas).HasMaxLength(2000);

        builder.HasOne(o => o.Cliente)
            .WithMany(c => c.Orcamentos)
            .HasForeignKey(o => o.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Empresa)
            .WithMany(e => e.Orcamentos)
            .HasForeignKey(o => o.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Usuario)
            .WithMany(u => u.Orcamentos)
            .HasForeignKey(o => o.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Status)
            .WithMany(s => s.Orcamentos)
            .HasForeignKey(o => o.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Itens)
            .WithOne(i => i.Orcamento)
            .HasForeignKey(i => i.OrcamentoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.MaosDeObra)
            .WithOne(m => m.Orcamento)
            .HasForeignKey(m => m.OrcamentoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.Tecnicos)
            .WithOne(t => t.Orcamento)
            .HasForeignKey(t => t.OrcamentoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.Historicos)
            .WithOne(h => h.Orcamento)
            .HasForeignKey(h => h.OrcamentoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrcamentoItemConfiguration : IEntityTypeConfiguration<OrcamentoItem>
{
    public void Configure(EntityTypeBuilder<OrcamentoItem> builder)
    {
        builder.ToTable("OrcamentoItens");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.CodigoPeca).IsRequired().HasMaxLength(50);
        builder.Property(i => i.Descricao).IsRequired().HasMaxLength(200);
        builder.Property(i => i.UnidadeMedida).IsRequired().HasMaxLength(10);

        builder.Property(i => i.Quantidade).HasPrecision(18, 3);
        builder.Property(i => i.PrecoUnitario).HasPrecision(18, 2);
        builder.Property(i => i.ValorDesconto).HasPrecision(18, 2);
        builder.Property(i => i.ValorTotal).HasPrecision(18, 2);

        builder.HasOne(i => i.Peca)
            .WithMany(p => p.OrcamentoItens)
            .HasForeignKey(i => i.PecaId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class OrcamentoMaoDeObraConfiguration : IEntityTypeConfiguration<OrcamentoMaoDeObra>
{
    public void Configure(EntityTypeBuilder<OrcamentoMaoDeObra> builder)
    {
        builder.ToTable("OrcamentoMaosDeObra");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Descricao).IsRequired().HasMaxLength(250);
        builder.Property(m => m.QuantidadeHoras).HasPrecision(18, 2);
        builder.Property(m => m.ValorUnitario).HasPrecision(18, 2);
        builder.Property(m => m.ValorDesconto).HasPrecision(18, 2);
        builder.Property(m => m.ValorTotal).HasPrecision(18, 2);
        builder.Property(m => m.Observacoes).HasMaxLength(1000);

        builder.HasMany(m => m.Tecnicos)
            .WithOne(t => t.OrcamentoMaoDeObra)
            .HasForeignKey(t => t.OrcamentoMaoDeObraId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrcamentoMaoDeObraTecnicoConfiguration : IEntityTypeConfiguration<OrcamentoMaoDeObraTecnico>
{
    public void Configure(EntityTypeBuilder<OrcamentoMaoDeObraTecnico> builder)
    {
        builder.ToTable("OrcamentoMaoDeObraTecnicos");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Funcao).HasMaxLength(80);
        builder.Property(t => t.Observacoes).HasMaxLength(250);

        builder.HasOne(t => t.Tecnico)
            .WithMany(tec => tec.MaoDeObraTecnicos)
            .HasForeignKey(t => t.TecnicoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrcamentoTecnicoConfiguration : IEntityTypeConfiguration<OrcamentoTecnico>
{
    public void Configure(EntityTypeBuilder<OrcamentoTecnico> builder)
    {
        builder.ToTable("OrcamentoTecnicos");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Funcao).HasMaxLength(80);
        builder.Property(t => t.Observacoes).HasMaxLength(250);

        builder.HasOne(t => t.Tecnico)
            .WithMany(tec => tec.OrcamentoTecnicos)
            .HasForeignKey(t => t.TecnicoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrcamentoStatusConfiguration : IEntityTypeConfiguration<OrcamentoStatus>
{
    public void Configure(EntityTypeBuilder<OrcamentoStatus> builder)
    {
        builder.ToTable("OrcamentoStatus");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Codigo).IsRequired().HasMaxLength(50);
        builder.HasIndex(s => s.Codigo).IsUnique();

        builder.Property(s => s.Nome).IsRequired().HasMaxLength(60);
        builder.Property(s => s.Descricao).HasMaxLength(200);
        builder.Property(s => s.CorHex).HasMaxLength(20);

        // Seed dos status iniciais obrigatórios
        builder.HasData(OrcamentoStatus.CriarStatusIniciais());
    }
}

public class OrcamentoHistoricoConfiguration : IEntityTypeConfiguration<OrcamentoHistorico>
{
    public void Configure(EntityTypeBuilder<OrcamentoHistorico> builder)
    {
        builder.ToTable("OrcamentoHistoricos");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Acao).IsRequired().HasMaxLength(80);
        builder.Property(h => h.StatusAnterior).HasMaxLength(60);
        builder.Property(h => h.StatusNovo).HasMaxLength(60);
        builder.Property(h => h.NomeUsuario).HasMaxLength(150);
        builder.Property(h => h.Descricao).IsRequired().HasMaxLength(500);
        builder.Property(h => h.Detalhes).HasMaxLength(2000);

        builder.HasOne(h => h.Usuario)
            .WithMany()
            .HasForeignKey(h => h.UsuarioId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

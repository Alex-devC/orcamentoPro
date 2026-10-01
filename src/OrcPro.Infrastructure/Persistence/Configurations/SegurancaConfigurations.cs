using OrcPro.Domain.Entities.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OrcPro.Infrastructure.Persistence.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuarios");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(60);

        builder.HasIndex(u => u.Username)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(u => u.NomeCompleto)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(u => u.Email)
            .HasMaxLength(150);

        builder.HasOne(u => u.Perfil)
            .WithMany(p => p.Usuarios)
            .HasForeignKey(u => u.PerfilId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PerfilConfiguration : IEntityTypeConfiguration<Perfil>
{
    public void Configure(EntityTypeBuilder<Perfil> builder)
    {
        builder.ToTable("Perfis");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Nome)
            .IsRequired()
            .HasMaxLength(80);

        builder.HasIndex(p => p.Nome)
            .IsUnique();

        builder.Property(p => p.Descricao)
            .HasMaxLength(250);
    }
}

public class PermissaoConfiguration : IEntityTypeConfiguration<Permissao>
{
    public void Configure(EntityTypeBuilder<Permissao> builder)
    {
        builder.ToTable("Permissoes");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Codigo)
            .IsRequired()
            .HasMaxLength(80);

        builder.HasIndex(p => p.Codigo)
            .IsUnique();

        builder.Property(p => p.Nome)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Modulo)
            .IsRequired()
            .HasMaxLength(60);

        builder.Property(p => p.Descricao)
            .HasMaxLength(250);
    }
}

public class PerfilPermissaoConfiguration : IEntityTypeConfiguration<PerfilPermissao>
{
    public void Configure(EntityTypeBuilder<PerfilPermissao> builder)
    {
        builder.ToTable("PerfilPermissoes");
        builder.HasKey(pp => pp.Id);

        builder.HasIndex(pp => new { pp.PerfilId, pp.PermissaoId })
            .IsUnique();

        builder.HasOne(pp => pp.Perfil)
            .WithMany(p => p.PerfilPermissoes)
            .HasForeignKey(pp => pp.PerfilId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pp => pp.Permissao)
            .WithMany(p => p.PerfilPermissoes)
            .HasForeignKey(pp => pp.PermissaoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

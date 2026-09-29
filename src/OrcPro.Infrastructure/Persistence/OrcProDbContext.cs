using OrcPro.Domain.Entities.Cliente;
using OrcPro.Domain.Entities.Configuracao;
using OrcPro.Domain.Entities.Empresa;
using OrcPro.Domain.Entities.Orcamento;
using OrcPro.Domain.Entities.Peca;
using OrcPro.Domain.Entities.Seguranca;
using OrcPro.Domain.Entities.Tecnico;
using Microsoft.EntityFrameworkCore;

namespace OrcPro.Infrastructure.Persistence;

public class OrcProDbContext : DbContext
{
    public OrcProDbContext(DbContextOptions<OrcProDbContext> options) : base(options)
    {
    }

    // Segurança
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Perfil> Perfis => Set<Perfil>();
    public DbSet<Permissao> Permissoes => Set<Permissao>();
    public DbSet<PerfilPermissao> PerfilPermissoes => Set<PerfilPermissao>();

    // Cadastros e Empresa
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Tecnico> Tecnicos => Set<Tecnico>();
    public DbSet<Peca> Pecas => Set<Peca>();

    // Orçamentos
    public DbSet<Orcamento> Orcamentos => Set<Orcamento>();
    public DbSet<OrcamentoItem> OrcamentoItens => Set<OrcamentoItem>();
    public DbSet<OrcamentoMaoDeObra> OrcamentoMaosDeObra => Set<OrcamentoMaoDeObra>();
    public DbSet<OrcamentoMaoDeObraTecnico> OrcamentoMaoDeObraTecnicos => Set<OrcamentoMaoDeObraTecnico>();
    public DbSet<OrcamentoTecnico> OrcamentoTecnicos => Set<OrcamentoTecnico>();
    public DbSet<OrcamentoStatus> OrcamentoStatus => Set<OrcamentoStatus>();
    public DbSet<OrcamentoHistorico> OrcamentoHistoricos => Set<OrcamentoHistorico>();

    // Configurações
    public DbSet<Configuracao> Configuracoes => Set<Configuracao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Aplica todas as configurações de entidades implementadas em IEntityTypeConfiguration deste assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrcProDbContext).Assembly);
    }
}

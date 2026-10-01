using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Entities.Seguranca;
using OrcPro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace OrcPro.Infrastructure.DependencyInjection;

/// <summary>
/// Prepara a base de dados para o login real: cria o schema quando a base não existe e,
/// apenas em instalações novas (sem nenhum usuário), cadastra o usuário inicial usando o
/// <see cref="IPasswordHasher"/> já registrado no DI. Não é um sistema de autenticação
/// alternativo nem um cadastro de usuários — é o seed mínimo para o login funcionar.
/// </summary>
public static class DatabaseInitializer
{
    public const string UsuarioInicial = "admin";
    public const string SenhaInicial = "admin";
    public const string PerfilInicial = "Administrador";

    public static async Task InitializeAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrcProDbContext>();

        await context.Database.EnsureCreatedAsync(cancellationToken);

        // Catálogo de permissões: cria as permissões ausentes e garante que o perfil
        // Administrador possua todas. Idempotente — pode rodar em toda inicialização.
        var sincronizador = scope.ServiceProvider.GetRequiredService<IPermissaoSincronizador>();
        await sincronizador.AplicarAsync(cancellationToken);

        if (await context.Usuarios.AnyAsync(cancellationToken))
        {
            return;
        }

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var perfil = await context.Perfis
            .FirstOrDefaultAsync(p => p.Nome == PerfilInicial, cancellationToken);

        perfil ??= (await context.Perfis.AddAsync(new Perfil
        {
            Nome = PerfilInicial,
            Descricao = "Acesso total ao sistema",
            Ativo = true
        }, cancellationToken)).Entity;

        context.Usuarios.Add(new Usuario
        {
            Username = UsuarioInicial,
            PasswordHash = hasher.HashPassword(SenhaInicial),
            NomeCompleto = "Administrador do Sistema",
            Email = "admin@orcpro.local",
            Ativo = true,
            Perfil = perfil
        });

        await context.SaveChangesAsync(cancellationToken);
    }
}

using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Application.Services;
using OrcPro.Infrastructure.Persistence;
using OrcPro.Infrastructure.Persistence.Providers;
using OrcPro.Infrastructure.Persistence.Repositories;
using OrcPro.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace OrcPro.Infrastructure.DependencyInjection;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        DatabaseConnectionOptions options)
    {
        // Configure DbContext with the appropriate database provider
        services.AddDbContext<OrcProDbContext>(builder =>
        {
            var configurator = DatabaseProviderConfiguratorFactory.ObterConfigurador(options.Provider);
            configurator.Configure(builder, options.ConnectionString);
        });

        // Repositories
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IPerfilRepository, PerfilRepository>();
        services.AddScoped<IEmpresaRepository, EmpresaRepository>();
        services.AddScoped<IClienteRepository, ClienteRepository>();
        services.AddScoped<ITecnicoRepository, TecnicoRepository>();
        services.AddScoped<IPecaRepository, PecaRepository>();
        services.AddScoped<IOrcamentoRepository, OrcamentoRepository>();
        services.AddScoped<IOrcamentoStatusRepository, OrcamentoStatusRepository>();
        services.AddScoped<IOrcamentoHistoricoRepository, OrcamentoHistoricoRepository>();

        // Security
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();

        // Application services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUsuarioService, UsuarioService>();
        services.AddScoped<IClienteService, ClienteService>();
        services.AddScoped<ITecnicoService, TecnicoService>();
        services.AddScoped<IPecaService, PecaService>();
        services.AddScoped<IEmpresaService, EmpresaService>();
        services.AddScoped<IOrcamentoService, OrcamentoService>();
        services.AddScoped<IRelatorioService, RelatorioService>();

        return services;
    }
}

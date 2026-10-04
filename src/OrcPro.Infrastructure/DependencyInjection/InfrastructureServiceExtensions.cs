using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Application.Services;
using OrcPro.Infrastructure.Persistence;
using OrcPro.Infrastructure.Persistence.Providers;
using OrcPro.Infrastructure.Persistence.Repositories;
using OrcPro.Infrastructure.Security;
using OrcPro.Infrastructure.Services;
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
            var provider = DatabaseProviderFactory.Create(options.Provider);
            provider.Configure(builder, options.ConnectionString);
        });

        // Repositories
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IPerfilRepository, PerfilRepository>();
        services.AddScoped<IPermissaoRepository, PermissaoRepository>();
        services.AddScoped<IEmpresaRepository, EmpresaRepository>();
        services.AddScoped<IClienteRepository, ClienteRepository>();
        services.AddScoped<ITecnicoRepository, TecnicoRepository>();
        services.AddScoped<IPecaRepository, PecaRepository>();
        services.AddScoped<IServicoRepository, ServicoRepository>();
        services.AddScoped<IOrcamentoRepository, OrcamentoRepository>();
        services.AddScoped<IOrcamentoStatusRepository, OrcamentoStatusRepository>();
        services.AddScoped<IOrcamentoHistoricoRepository, OrcamentoHistoricoRepository>();

        // Security
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();

        // Application services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPermissaoService, PermissaoService>();
        services.AddScoped<IPermissaoSincronizador, PermissaoSincronizador>();
        services.AddScoped<IUsuarioService, UsuarioService>();
        services.AddScoped<IPerfilService, PerfilService>();
        services.AddScoped<IClienteService, ClienteService>();
        services.AddScoped<ITecnicoService, TecnicoService>();
        services.AddScoped<IPecaService, PecaService>();
        services.AddScoped<IServicoService, ServicoService>();
        services.AddScoped<IEmpresaService, EmpresaService>();
        services.AddScoped<IOrcamentoService, OrcamentoService>();
        services.AddScoped<IRelatorioService, RelatorioService>();

        // External services
        services.AddSingleton<ICepService, ViaCepService>();

        return services;
    }
}

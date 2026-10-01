using OrcPro.Domain.Common;

namespace OrcPro.Application.Interfaces.Services;

/// <summary>
/// Resultado da aplicação do catálogo de permissões (usado no seed da base).
/// </summary>
public sealed class PermissaoSincronizacaoResultado
{
    public PermissaoSincronizacaoResultado(int permissoesCriadas, int vinculosCriados, bool perfilCriado)
    {
        PermissoesCriadas = permissoesCriadas;
        VinculosCriados = vinculosCriados;
        PerfilCriado = perfilCriado;
    }

    public int PermissoesCriadas { get; }
    public int VinculosCriados { get; }
    public bool PerfilCriado { get; }

    public bool HouveAlteracoes => PermissoesCriadas > 0 || VinculosCriados > 0 || PerfilCriado;
}

/// <summary>
/// Mantém a base sincronizada com o catálogo de permissões do Domain: cria as permissões que
/// ainda não existem e garante que o perfil Administrador possua todas elas. A operação é
/// idempotente — pode rodar a cada inicialização sem duplicar registros.
/// </summary>
public interface IPermissaoSincronizador
{
    Task<PermissaoSincronizacaoResultado> AplicarAsync(CancellationToken cancellationToken = default);
}
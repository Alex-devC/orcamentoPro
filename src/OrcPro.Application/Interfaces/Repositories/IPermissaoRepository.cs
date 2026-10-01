using OrcPro.Domain.Entities.Seguranca;

namespace OrcPro.Application.Interfaces.Repositories;

/// <summary>
/// Repositório de permissões do sistema. Exposto para o <c>PerfilService</c>
/// validar/sincronizar as permissões vinculadas a um perfil (estrutura preparada
/// para a gestão de permissões futura).
/// </summary>
public interface IPermissaoRepository : IRepository<Permissao>
{
}
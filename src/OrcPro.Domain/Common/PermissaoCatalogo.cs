namespace OrcPro.Domain.Common;

/// <summary>
/// Definição de uma permissão do catálogo do OrcPro. O código segue o padrão
/// <c>MODULO.ACAO</c> (ex.: <c>CLIENTES.CRIAR</c>) e é a chave usada em toda a aplicação.
/// </summary>
public sealed class PermissaoDefinicao
{
    public PermissaoDefinicao(string codigo, string modulo, string nome)
    {
        Codigo = codigo;
        Modulo = modulo;
        Nome = nome;
    }

    public string Codigo { get; }
    public string Modulo { get; }
    public string Nome { get; }
}

/// <summary>
/// Catálogo central de permissões. Para disponibilizar um módulo novo basta acrescentar
/// uma entrada em <see cref="Definicoes"/>: o sincronizador cria o registro na base
/// (<c>Permissoes</c>) e o perfil Administrador passa a recebê-lo automaticamente.
/// Nenhuma outra camada precisa mudar.
/// </summary>
public static class PermissaoCatalogo
{
    /// <summary>Códigos das permissões — use estas constantes em vez de literais.</summary>
    public static class Codigos
    {
        public static class Dashboard
        {
            public const string Visualizar = "DASHBOARD.VISUALIZAR";
        }

        public static class Clientes
        {
            public const string Visualizar = "CLIENTES.VISUALIZAR";
            public const string Criar = "CLIENTES.CRIAR";
            public const string Editar = "CLIENTES.EDITAR";
            public const string Excluir = "CLIENTES.EXCLUIR";
            public const string AtivarInativar = "CLIENTES.ATIVAR_INATIVAR";
        }

        public static class UsuariosPerfis
        {
            public const string Visualizar = "USUARIOS_PERFIS.VISUALIZAR";
            public const string Criar = "USUARIOS_PERFIS.CRIAR";
            public const string Editar = "USUARIOS_PERFIS.EDITAR";
            public const string Excluir = "USUARIOS_PERFIS.EXCLUIR";
            public const string GerenciarPermissoes = "USUARIOS_PERFIS.GERENCIAR_PERMISSOES";
        }

        public static class Tecnicos
        {
            public const string Visualizar = "TECNICOS.VISUALIZAR";
            public const string Criar = "TECNICOS.CRIAR";
            public const string Editar = "TECNICOS.EDITAR";
            public const string Excluir = "TECNICOS.EXCLUIR";
            public const string AtivarInativar = "TECNICOS.ATIVAR_INATIVAR";
        }

        public static class Pecas
        {
            public const string Visualizar = "PECAS.VISUALIZAR";
            public const string Criar = "PECAS.CRIAR";
            public const string Editar = "PECAS.EDITAR";
            public const string Excluir = "PECAS.EXCLUIR";
            public const string AtivarInativar = "PECAS.ATIVAR_INATIVAR";
        }
    }

    public const string ModuloDashboard = "DASHBOARD";
    public const string ModuloClientes = "CLIENTES";
    public const string ModuloUsuariosPerfis = "USUARIOS_PERFIS";
    public const string ModuloTecnicos = "TECNICOS";
    public const string ModuloPecas = "PECAS";

    /// <summary>Nome do perfil que recebe todas as permissões do catálogo.</summary>
    public const string PerfilAdministrador = "Administrador";

    /// <summary>Catálogo do sistema, agrupado por módulo na ordem de exibição.</summary>
    public static IReadOnlyList<PermissaoDefinicao> Definicoes { get; } = new[]
    {
        new PermissaoDefinicao(Codigos.Dashboard.Visualizar, ModuloDashboard, "Visualizar o painel"),

        new PermissaoDefinicao(Codigos.Clientes.Visualizar, ModuloClientes, "Visualizar clientes"),
        new PermissaoDefinicao(Codigos.Clientes.Criar, ModuloClientes, "Cadastrar clientes"),
        new PermissaoDefinicao(Codigos.Clientes.Editar, ModuloClientes, "Editar clientes"),
        new PermissaoDefinicao(Codigos.Clientes.Excluir, ModuloClientes, "Excluir clientes"),
        new PermissaoDefinicao(Codigos.Clientes.AtivarInativar, ModuloClientes, "Ativar / inativar clientes"),

        new PermissaoDefinicao(Codigos.UsuariosPerfis.Visualizar, ModuloUsuariosPerfis, "Visualizar usuários e perfis"),
        new PermissaoDefinicao(Codigos.UsuariosPerfis.Criar, ModuloUsuariosPerfis, "Cadastrar usuários e perfis"),
        new PermissaoDefinicao(Codigos.UsuariosPerfis.Editar, ModuloUsuariosPerfis, "Editar usuários e perfis"),
        new PermissaoDefinicao(Codigos.UsuariosPerfis.Excluir, ModuloUsuariosPerfis, "Excluir usuários e perfis"),
        new PermissaoDefinicao(Codigos.UsuariosPerfis.GerenciarPermissoes, ModuloUsuariosPerfis, "Gerenciar permissões de perfis"),

        new PermissaoDefinicao(Codigos.Tecnicos.Visualizar, ModuloTecnicos, "Visualizar técnicos"),
        new PermissaoDefinicao(Codigos.Tecnicos.Criar, ModuloTecnicos, "Cadastrar técnicos"),
        new PermissaoDefinicao(Codigos.Tecnicos.Editar, ModuloTecnicos, "Editar técnicos"),
        new PermissaoDefinicao(Codigos.Tecnicos.Excluir, ModuloTecnicos, "Excluir técnicos"),
        new PermissaoDefinicao(Codigos.Tecnicos.AtivarInativar, ModuloTecnicos, "Ativar / inativar técnicos"),

        new PermissaoDefinicao(Codigos.Pecas.Visualizar, ModuloPecas, "Visualizar peças e itens"),
        new PermissaoDefinicao(Codigos.Pecas.Criar, ModuloPecas, "Cadastrar peças e itens"),
        new PermissaoDefinicao(Codigos.Pecas.Editar, ModuloPecas, "Editar peças e itens"),
        new PermissaoDefinicao(Codigos.Pecas.Excluir, ModuloPecas, "Excluir peças e itens"),
        new PermissaoDefinicao(Codigos.Pecas.AtivarInativar, ModuloPecas, "Ativar / inativar peças e itens")
    };

    private static readonly IReadOnlyDictionary<string, string> RotulosModulo =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ModuloDashboard] = "Dashboard",
            [ModuloClientes] = "Clientes",
            [ModuloUsuariosPerfis] = "Usuários e Perfis",
            [ModuloTecnicos] = "Técnicos",
            [ModuloPecas] = "Peças e Itens"
        };

    /// <summary>Rótulo amigável do módulo (fallback: o próprio código do módulo).</summary>
    public static string RotuloModulo(string? modulo)
        => !string.IsNullOrWhiteSpace(modulo) && RotulosModulo.TryGetValue(modulo, out var rotulo)
            ? rotulo
            : modulo ?? string.Empty;

    public static bool Existe(string codigo)
        => Definicoes.Any(d => string.Equals(d.Codigo, codigo, StringComparison.OrdinalIgnoreCase));

    /// <summary>Códigos de um módulo, na ordem do catálogo.</summary>
    public static IReadOnlyList<string> CodigosDoModulo(string modulo)
        => Definicoes
            .Where(d => string.Equals(d.Modulo, modulo, StringComparison.OrdinalIgnoreCase))
            .Select(d => d.Codigo)
            .ToList();
}
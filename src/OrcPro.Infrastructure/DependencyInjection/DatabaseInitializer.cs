using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Entities.Orcamento;
using OrcPro.Domain.Entities.Seguranca;
using OrcPro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace OrcPro.Infrastructure.DependencyInjection;

/// <summary>
/// Ajustes pontuais de schema para bases criadas por versões anteriores.
/// O <c>EnsureCreated</c> não altera tabelas existentes, então colunas novas precisam ser
/// adicionadas explicitamente. A operação é idempotente e só atua em providers SQLite.
/// </summary>
internal static class SchemaUpgrade
{
    private static readonly Dictionary<string, Dictionary<string, string>> Colunas =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Tecnicos"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Rg"] = "TEXT NULL",
                ["Cep"] = "TEXT NULL",
                ["Logradouro"] = "TEXT NULL",
                ["Numero"] = "TEXT NULL",
                ["Complemento"] = "TEXT NULL",
                ["Bairro"] = "TEXT NULL",
                ["Cidade"] = "TEXT NULL",
                ["Uf"] = "TEXT NULL"
            },
            ["Pecas"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Categoria"] = "TEXT NULL",
                ["Marca"] = "TEXT NULL",
                ["Modelo"] = "TEXT NULL",
                ["CodigoBarras"] = "TEXT NULL"
            },
            // Emitente: campos adicionados ao cadastro de Minha Empresa.
            ["Empresas"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["EmailFinanceiro"] = "TEXT NULL",
                ["Observacoes"] = "TEXT NULL"
            },
            // Orçamentos: vínculo da linha de serviço com o cadastro de Serviços.
            ["OrcamentoMaosDeObra"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["ServicoId"] = "INTEGER NULL"
            }
        };

        /// <summary>
        /// Status de orçamento, garantidos em instalações que já existiam antes do seed do EF.
        /// O <c>HasData</c> do EF só roda em bases novas: sem esta etapa, um banco antigo
        /// ficaria sem nenhum status e o módulo de Orçamentos não conseguiria criar nada.
        /// A operação é idempotente (INSERT ... SELECT ... WHERE NOT EXISTS).
        /// </summary>
        private static readonly IReadOnlyList<(string Codigo, string Nome, string Cor, int Ordem)> StatusIniciais =
            new List<(string, string, string, int)>
            {
                (OrcamentoStatus.CodigoRascunho, "Rascunho", "#ECEFF3", 1),
                (OrcamentoStatus.CodigoAguardandoAprovacao, "Aguardando aprovação", "#FEF3C7", 2),
                (OrcamentoStatus.CodigoAprovado, "Aprovado", "#E0F2FE", 3),
                (OrcamentoStatus.CodigoEmExecucao, "Em execução", "#E0F7FF", 4),
                (OrcamentoStatus.CodigoFinalizado, "Finalizado", "#DCFCE7", 5),
                (OrcamentoStatus.CodigoCancelado, "Cancelado", "#FEE2E2", 6),
                (OrcamentoStatus.CodigoRecusado, "Recusado", "#FFDAD6", 7)
            };

    /// <summary>
    /// Tabelas introduzidas depois da criação inicial da base.
    ///
    /// <para>O <c>EnsureCreated</c> só cria o schema quando o banco ainda não existe; em uma
    /// instalação anterior ao módulo, a tabela nova jamais seria criada. Aqui ela é criada
    /// sob demanda com <c>IF NOT EXISTS</c> (operação idempotente, segura para rodar em toda
    /// inicialização). O DDL é idêntico ao gerado pelo EF para esta entidade — inclusive o
    /// tipo TEXT usado pelo provider SQLite para <c>decimal</c>.</para>
    /// </summary>
    private static readonly Dictionary<string, (string CriarTabela, string CriarIndice)> Tabelas =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Servicos"] = (
                """
                CREATE TABLE IF NOT EXISTS "Servicos" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Servicos" PRIMARY KEY AUTOINCREMENT,
                    "Codigo" TEXT NOT NULL,
                    "Descricao" TEXT NOT NULL,
                    "Categoria" TEXT NULL,
                    "Valor" TEXT NOT NULL,
                    "Unidade" TEXT NOT NULL,
                    "TempoEstimado" TEXT NOT NULL,
                    "Observacoes" TEXT NULL,
                    "Ativo" INTEGER NOT NULL,
                    "DataCriacao" TEXT NOT NULL,
                    "DataAtualizacao" TEXT NULL
                )
                """,
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Servicos_Codigo" ON "Servicos" ("Codigo")
                """)
        };

    public static async Task AplicarAsync(OrcProDbContext context, CancellationToken cancellationToken = default)
    {
        if (!context.Database.IsSqlite())
            return;

        foreach (var (tabela, colunas) in Colunas)
        {
            var existentes = await LerColunasAsync(context, tabela, cancellationToken);
            if (existentes.Count == 0)
            {
                // Tabela ainda não existe (base nova): EnsureCreated já a criou com todas as colunas.
                continue;
            }

            foreach (var (coluna, definicao) in colunas)
            {
                if (existentes.Contains(coluna))
                    continue;

                // Os identificadores vêm do dicionário fixo acima (nunca entrada do usuário);
                // DDL não aceita parâmetros, então a interpolação é segura aqui.
#pragma warning disable EF1002
                await context.Database.ExecuteSqlRawAsync(
                    $"ALTER TABLE \"{tabela}\" ADD COLUMN \"{coluna}\" {definicao}",
                    cancellationToken);
#pragma warning restore EF1002
            }
        }

        foreach (var (tabela, (criarTabela, criarIndice)) in Tabelas)
        {
            var existentes = await LerColunasAsync(context, tabela, cancellationToken);
            if (existentes.Count > 0)
                continue;

            // Tabela ausente em uma base já existente: EnsureCreated não a criaria.
            await context.Database.ExecuteSqlRawAsync(criarTabela, cancellationToken);
            await context.Database.ExecuteSqlRawAsync(criarIndice, cancellationToken);
        }

        await GarantirStatusDeOrcamentoAsync(context, cancellationToken);
    }

    /// <summary>
    /// Garante os status de orçamento em bases criadas antes do seed do EF. Sem status
    /// nenhum o módulo de Orçamentos não consegue criar orçamento (não há para onde ir).
    /// Idempotente: só insere os códigos que ainda não existirem.
    /// </summary>
    private static async Task GarantirStatusDeOrcamentoAsync(
        OrcProDbContext context,
        CancellationToken cancellationToken)
    {
        var statusDb = await context.Database
            .SqlQueryRaw<int>("SELECT 1 AS Value FROM sqlite_master WHERE type = 'table' AND name = 'OrcamentoStatus'")
            .ToListAsync(cancellationToken);

        if (statusDb.Count == 0)
            return;

        foreach (var status in StatusIniciais)
        {
            var agoraSql = $"'{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}'";

            // Os valores vêm da lista fixa acima (nunca entrada do usuário).
            var sql = $"""
                INSERT INTO "OrcamentoStatus" ("Codigo", "Nome", "CorHex", "Ordem", "Ativo", "DataCriacao")
                SELECT '{status.Codigo}', '{status.Nome}', '{status.Cor}', {status.Ordem}, 1, {agoraSql}
                WHERE NOT EXISTS (SELECT 1 FROM "OrcamentoStatus" WHERE "Codigo" = '{status.Codigo}')
                """;

#pragma warning disable EF1002
            await context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
#pragma warning restore EF1002
        }
    }

    private static async Task<HashSet<string>> LerColunasAsync(
        OrcProDbContext context,
        string tabela,
        CancellationToken cancellationToken)
    {
        var colunas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var conexao = context.Database.GetDbConnection();
        if (conexao.State != System.Data.ConnectionState.Open)
        {
            await conexao.OpenAsync(cancellationToken);
        }

        using var comando = conexao.CreateCommand();
        comando.CommandText = $"PRAGMA table_info(\"{tabela}\")";

        using var leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            colunas.Add(leitor.GetString(1));
        }

        return colunas;
    }
}

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

        // Colunas novas em tabelas já existentes (o EnsureCreated não altera tabelas criadas antes).
        await SchemaUpgrade.AplicarAsync(context, cancellationToken);

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

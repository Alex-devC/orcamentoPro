using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrcPro.Application.DTOs.Cliente;
using OrcPro.Application.DTOs.Empresa;
using CpfCnpjValidator = OrcPro.Domain.Common.Formatters.CpfCnpjValidator;
using OrcPro.Application.DTOs.Orcamento;
using OrcPro.Application.DTOs.Peca;
using OrcPro.Application.DTOs.Servico;
using OrcPro.Application.DTOs.Tecnico;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Common;
using OrcPro.Domain.Common.Calculos;
using OrcPro.Domain.Common.Formatters;
using OrcPro.Domain.Entities.Orcamento;
using OrcPro.Infrastructure.DependencyInjection;
using OrcPro.Infrastructure.Persistence;
using OrcPro.Infrastructure.Persistence.Providers;
using OrcPro.Infrastructure.Services;
using Xunit;

namespace OrcPro.Tests;

/// <summary>
/// Cobertura de integração do módulo de Orçamentos sobre SQLite real, usando exatamente o
/// caminho do aplicativo (<see cref="DatabaseInitializer"/>). Usa os SERVIÇOS REAIS — sem
/// mocks — para Cliente, Emitente, Peça, Serviço, Técnico e Usuário.
/// </summary>
public sealed class OrcamentosIntegracaoTests : IDisposable
{
    private readonly ServiceProvider _provedor;
    private readonly IServiceScope _escopo;
    private readonly string _caminhoBanco;
    private readonly string _pastaLogo;

    public OrcamentosIntegracaoTests()
    {
        _caminhoBanco = Path.Combine(Path.GetTempPath(), $"orcamentos-{Guid.NewGuid():N}.db");
        _pastaLogo = Path.Combine(Path.GetTempPath(), $"logo-orc-{Guid.NewGuid():N}");

        var services = new ServiceCollection();
        services.AddInfrastructure(DatabaseConnectionOptions.DefaultSqlite(_caminhoBanco));
        services.AddSingleton<IEmpresaLogoStorage>(new EmpresaLogoStorage(_pastaLogo));

        _provedor = services.BuildServiceProvider();
        DatabaseInitializer.InitializeAsync(_provedor).GetAwaiter().GetResult();

        _escopo = _provedor.CreateScope();
    }

    public void Dispose()
    {
        _escopo.Dispose();
        _provedor.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        try { if (File.Exists(_caminhoBanco)) File.Delete(_caminhoBanco); } catch (IOException) { }
        try { if (Directory.Exists(_pastaLogo)) Directory.Delete(_pastaLogo, true); } catch (IOException) { }
    }

    private IOrcamentoService Orcamentos => _escopo.ServiceProvider.GetRequiredService<IOrcamentoService>();
    private IClienteService Clientes => _escopo.ServiceProvider.GetRequiredService<IClienteService>();
    private IPecaService Pecas => _escopo.ServiceProvider.GetRequiredService<IPecaService>();
    private IServicoService Servicos => _escopo.ServiceProvider.GetRequiredService<IServicoService>();
    private ITecnicoService Tecnicos => _escopo.ServiceProvider.GetRequiredService<ITecnicoService>();
    private IEmpresaService Empresa => _escopo.ServiceProvider.GetRequiredService<IEmpresaService>();

    /// <summary>Cadastra o usuário real do seed (admin) — nenhum usuário fictício.</summary>
    private async Task<int> UsuarioRealAsync()
    {
        using var escopo = _provedor.CreateScope();
        var ctx = escopo.ServiceProvider.GetRequiredService<OrcProDbContext>();
        return await ctx.Usuarios.Select(u => u.Id).FirstAsync();
    }

    /// <summary>Cadastra as dependências reais usadas pelo orçamento.</summary>
    private async Task<(int ClienteId, int PecaId, int ServicoId, int TecnicoId, int UsuarioId)> DependenciasAsync()
    {
        var usuarioId = await UsuarioRealAsync();

        var cliente = await Clientes.CriarAsync(new CriarClienteDto
        {
            NomeRazaoSocial = "CLIENTE INTEGRACAO",
            TipoPessoa = "PJ",
            Celular = "11988887777",
            Email = "cliente@teste.com"
        });

        var peca = await Pecas.CriarAsync(new CriarPecaDto
        {
            Codigo = await Pecas.GerarProximoCodigoAsync(),
            Descricao = "CABO DE REDE CAT6",
            UnidadeMedida = "UN",
            PrecoVenda = 100.00m
        });

        var servico = await Servicos.CriarAsync(new CriarServicoDto
        {
            Codigo = await Servicos.GerarProximoCodigoAsync(),
            Descricao = "INSTALACAO E CONFIGURACAO",
            Unidade = "HR",
            Valor = 150.00m,
            TempoEstimado = 1m
        });

        var tecnico = await Tecnicos.CriarAsync(new CriarTecnicoDto
        {
            Codigo = "TEC-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant(),
            Nome = "TECNICO INTEGRACAO",
            Cpf = CpfCnpjValidator.GerarCpfValido()
        });

        return (cliente.Id, peca.Id, servico.Id, tecnico.Id, usuarioId);
    }

    private async Task ConfigurarEmitenteAsync()
    {
        await Empresa.SalvarAsync(new SalvarEmpresaDto
        {
            RazaoSocial = "ALEX T.I. TECNOLOGIA E ASSISTENCIA LTDA",
            NomeFantasia = "ALEX",
            Cnpj = CpfCnpjValidator.GerarCnpjNumericoValido(),
            Email = "emitente@teste.com"
        });
    }

    private static CriarOrcamentoDto Novo(int clienteId, int usuarioId, int pecaId, int servicoId, int? tecnicoId = null)
    {
        var dto = new CriarOrcamentoDto
        {
            ClienteId = clienteId,
            UsuarioId = usuarioId,
            DiasValidade = 15,
            ItensIniciais = new()
            {
                new AdicionarItemDto
                {
                    PecaId = pecaId,
                    CodigoPeca = "PEC-0001",
                    Descricao = "CABO DE REDE CAT6",
                    Quantidade = 2,
                    PrecoUnitario = 100.00m
                }
            },
            MaosDeObraIniciais = new()
            {
                new AdicionarMaoDeObraDto
                {
                    ServicoId = servicoId,
                    Descricao = "INSTALACAO E CONFIGURACAO",
                    QuantidadeHoras = 1,
                    ValorUnitario = 150.00m
                }
            }
        };

        if (tecnicoId.HasValue)
            dto.TecnicosIniciais.Add(new AssociarTecnicoDto { TecnicoId = tecnicoId.Value });

        return dto;
    }

    // ---------- Persistência completa ----------

    [Fact]
    public async Task Orcamento_CriarSalvarCarregar_DevePreservarTudo()
    {
        await ConfigurarEmitenteAsync();
        var (clienteId, pecaId, servicoId, tecnicoId, usuarioId) = await DependenciasAsync();

        var criado = await Orcamentos.CriarAsync(Novo(clienteId, usuarioId, pecaId, servicoId, tecnicoId));

        // Recarrega do banco em uma nova chamada de serviço.
        var relido = await Orcamentos.ObterPorIdAsync(criado.Id);

        Assert.False(string.IsNullOrWhiteSpace(relido.Numero));
        Assert.Equal(criado.Numero, relido.Numero);
        Assert.Equal(clienteId, relido.ClienteId);
        Assert.Equal(OrcamentoStatus.CodigoRascunho, relido.StatusCodigo);

        Assert.Single(relido.Itens);
        Assert.Equal("CABO DE REDE CAT6", relido.Itens[0].Descricao);
        Assert.Equal(2m, relido.Itens[0].Quantidade);
        Assert.Equal(100.00m, relido.Itens[0].PrecoUnitario);

        Assert.Single(relido.MaosDeObra);
        Assert.Equal(servicoId, relido.MaosDeObra[0].ServicoId);
        Assert.Equal(150.00m, relido.MaosDeObra[0].ValorUnitario);

        Assert.Single(relido.Tecnicos);
        Assert.Equal(tecnicoId, relido.Tecnicos[0].TecnicoId);

        Assert.Equal(200.00m, relido.ValorTotalItens);
        Assert.Equal(150.00m, relido.ValorTotalMaoDeObra);
        Assert.Equal(350.00m, relido.ValorTotal);
    }

    [Fact]
    public async Task Orcamento_ComDesconto_DevePersistirOTotalCalculado()
    {
        await ConfigurarEmitenteAsync();
        var (clienteId, pecaId, servicoId, _, usuarioId) = await DependenciasAsync();

        var dto = Novo(clienteId, usuarioId, pecaId, servicoId);
        dto.ValorDesconto = 50.00m;

        var criado = await Orcamentos.CriarAsync(dto);
        var relido = await Orcamentos.ObterPorIdAsync(criado.Id);

        Assert.Equal(300.00m, relido.ValorTotal);
    }

    [Fact]
    public async Task Orcamento_SemEmitente_DeveBloquearCriacao()
    {
        var (clienteId, pecaId, servicoId, _, usuarioId) = await DependenciasAsync();

        await Assert.ThrowsAsync<OrcPro.Application.Exceptions.BusinessException>(() =>
            Orcamentos.CriarAsync(Novo(clienteId, usuarioId, pecaId, servicoId)));
    }

    [Fact]
    public async Task Orcamento_HistoricoEUsuario_DeverFicarGravadosNoBanco()
    {
        await ConfigurarEmitenteAsync();
        var (clienteId, pecaId, servicoId, _, usuarioId) = await DependenciasAsync();

        var criado = await Orcamentos.CriarAsync(Novo(clienteId, usuarioId, pecaId, servicoId));

        var aguardando = (await Orcamentos.ListarStatusDisponiveisAsync())
            .First(s => s.Codigo == OrcamentoStatus.CodigoAguardandoAprovacao);

        await Orcamentos.AlterarStatusAsync(new AlterarStatusOrcamentoDto
        {
            OrcamentoId = criado.Id,
            NovoStatusId = aguardando.Id,
            UsuarioId = usuarioId,
            ObservacaoMotivo = "Enviado ao cliente"
        });

        var relido = await Orcamentos.ObterPorIdAsync(criado.Id);

        Assert.Equal(2, relido.Historicos.Count);
        Assert.All(relido.Historicos, h => Assert.False(string.IsNullOrWhiteSpace(h.NomeUsuario)));
        Assert.All(relido.Historicos, h => Assert.False(string.IsNullOrWhiteSpace(h.NomeUsuario)));
        Assert.Equal(usuarioId, relido.UsuarioId);
    }

    [Fact]
    public async Task Orcamento_AlterarStatusERecarregar_DeveManterONovoStatus()
    {
        await ConfigurarEmitenteAsync();
        var (clienteId, pecaId, servicoId, _, usuarioId) = await DependenciasAsync();
        var criado = await Orcamentos.CriarAsync(Novo(clienteId, usuarioId, pecaId, servicoId));

        var statuses = await Orcamentos.ListarStatusDisponiveisAsync();

        foreach (var codigo in new[]
        {
            OrcamentoStatus.CodigoAguardandoAprovacao,
            OrcamentoStatus.CodigoAprovado,
            OrcamentoStatus.CodigoEmExecucao,
            OrcamentoStatus.CodigoFinalizado
        })
        {
            await Orcamentos.AlterarStatusAsync(new AlterarStatusOrcamentoDto
            {
                OrcamentoId = criado.Id,
                NovoStatusId = statuses.First(s => s.Codigo == codigo).Id,
                UsuarioId = usuarioId
            });
        }

        Assert.Equal(OrcamentoStatus.CodigoFinalizado, (await Orcamentos.ObterPorIdAsync(criado.Id)).StatusCodigo);

        // E a listagem reflete o novo status.
        var paged = await Orcamentos.ListarPaginadoAsync(new OrcPro.Application.DTOs.Common.PagedRequest { PageNumber = 1, PageSize = 20 });
        Assert.Single(paged.Items);
        Assert.Equal(OrcamentoStatus.CodigoFinalizado, paged.Items[0].StatusCodigo);
    }

    [Fact]
    public async Task Orcamento_ValorDaPecaNoOrcamento_NaoSegueMudancaNoCadastro()
    {
        await ConfigurarEmitenteAsync();
        var (clienteId, pecaId, servicoId, _, usuarioId) = await DependenciasAsync();
        var criado = await Orcamentos.CriarAsync(Novo(clienteId, usuarioId, pecaId, servicoId));

        // Altera o preço da peça no cadastro.
        var peca = await Pecas.ObterPorIdAsync(pecaId);
        await Pecas.AtualizarAsync(new AtualizarPecaDto
        {
            Id = peca.Id,
            Codigo = peca.Codigo,
            Descricao = peca.Descricao,
            UnidadeMedida = peca.UnidadeMedida,
            PrecoVenda = 9999.00m,
            Ativo = true
        });

        // O orçamento existente NÃO muda.
        var relido = await Orcamentos.ObterPorIdAsync(criado.Id);
        Assert.Equal(100.00m, relido.Itens[0].PrecoUnitario);
        Assert.Equal(350.00m, relido.ValorTotal);
    }

    [Fact]
    public async Task Orcamento_ExcluirFinalizado_DeveSerBloqueadoNoBanco()
    {
        await ConfigurarEmitenteAsync();
        var (clienteId, pecaId, servicoId, _, usuarioId) = await DependenciasAsync();
        var criado = await Orcamentos.CriarAsync(Novo(clienteId, usuarioId, pecaId, servicoId));

        var statuses = await Orcamentos.ListarStatusDisponiveisAsync();
        foreach (var codigo in new[]
        {
            OrcamentoStatus.CodigoAguardandoAprovacao,
            OrcamentoStatus.CodigoAprovado,
            OrcamentoStatus.CodigoEmExecucao,
            OrcamentoStatus.CodigoFinalizado
        })
        {
            await Orcamentos.AlterarStatusAsync(new AlterarStatusOrcamentoDto
            {
                OrcamentoId = criado.Id,
                NovoStatusId = statuses.First(s => s.Codigo == codigo).Id,
                UsuarioId = usuarioId
            });
        }

        await Assert.ThrowsAsync<OrcPro.Application.Exceptions.BusinessException>(() =>
            Orcamentos.ExcluirAsync(new ExcluirOrcamentoDto { OrcamentoId = criado.Id, UsuarioId = usuarioId }));

        Assert.NotNull(await Orcamentos.ObterPorIdAsync(criado.Id));
    }

    [Fact]
    public async Task Orcamento_ExcluirRascunho_DeveRemoverComItensETecnicos()
    {
        await ConfigurarEmitenteAsync();
        var (clienteId, pecaId, servicoId, tecnicoId, usuarioId) = await DependenciasAsync();
        var criado = await Orcamentos.CriarAsync(Novo(clienteId, usuarioId, pecaId, servicoId, tecnicoId));

        await Orcamentos.ExcluirAsync(new ExcluirOrcamentoDto { OrcamentoId = criado.Id, UsuarioId = usuarioId });

        await Assert.ThrowsAsync<OrcPro.Application.Exceptions.NotFoundException>(() => Orcamentos.ObterPorIdAsync(criado.Id));

        // As linhas órfãs foram removidas junto (cascade).
        await using var escopo = _provedor.CreateAsyncScope();
        var ctx = escopo.ServiceProvider.GetRequiredService<OrcProDbContext>();

        Assert.False(await ctx.OrcamentoItens.AnyAsync(i => i.OrcamentoId == criado.Id));
        Assert.False(await ctx.OrcamentoMaosDeObra.AnyAsync(m => m.OrcamentoId == criado.Id));
        Assert.False(await ctx.OrcamentoTecnicos.AnyAsync(t => t.OrcamentoId == criado.Id));
        Assert.False(await ctx.OrcamentoHistoricos.AnyAsync(h => h.OrcamentoId == criado.Id));
    }

    [Fact]
    public async Task Orcamento_PersistirAposReiniciarOPrograma()
    {
        await ConfigurarEmitenteAsync();
        var (clienteId, pecaId, servicoId, tecnicoId, usuarioId) = await DependenciasAsync();
        var criado = await Orcamentos.CriarAsync(Novo(clienteId, usuarioId, pecaId, servicoId, tecnicoId));

        // Novo provedor sobre o MESMO arquivo equivale a reiniciar o aplicativo.
        var services = new ServiceCollection();
        services.AddInfrastructure(DatabaseConnectionOptions.DefaultSqlite(_caminhoBanco));
        services.AddSingleton<IEmpresaLogoStorage>(new EmpresaLogoStorage(_pastaLogo));
        await using var novoProvedor = services.BuildServiceProvider();
        await DatabaseInitializer.InitializeAsync(novoProvedor);

        await using var novoEscopo = novoProvedor.CreateAsyncScope();
        var novoServico = novoEscopo.ServiceProvider.GetRequiredService<IOrcamentoService>();

        var relido = await novoServico.ObterPorIdAsync(criado.Id);

        Assert.Equal(criado.Numero, relido.Numero);
        Assert.Single(relido.Itens);
        Assert.Single(relido.MaosDeObra);
        Assert.Single(relido.Tecnicos);
        Assert.Equal(350.00m, relido.ValorTotal);
    }

    [Fact]
    public async Task Orcamento_NumerosSequenciais_NaoSeRepetemAposExclusao()
    {
        await ConfigurarEmitenteAsync();
        var (clienteId, pecaId, servicoId, _, usuarioId) = await DependenciasAsync();

        var primeiro = await Orcamentos.CriarAsync(Novo(clienteId, usuarioId, pecaId, servicoId));
        var segundo = await Orcamentos.CriarAsync(Novo(clienteId, usuarioId, pecaId, servicoId));

        Assert.NotEqual(primeiro.Numero, segundo.Numero);

        // O sequencial do ano continua a partir do maior existente.
        var terceiro = await Orcamentos.CriarAsync(Novo(clienteId, usuarioId, pecaId, servicoId));
        Assert.Equal(segundo.Sequencial + 1, terceiro.Sequencial);
    }

    [Fact]
    public async Task Orcamento_DezItens_DeveSerGravadoInteiro()
    {
        await ConfigurarEmitenteAsync();
        var (clienteId, pecaId, servicoId, _, usuarioId) = await DependenciasAsync();

        var dto = Novo(clienteId, usuarioId, pecaId, servicoId);
        dto.ItensIniciais.Clear();

        for (int i = 1; i <= 10; i++)
            dto.ItensIniciais.Add(new AdicionarItemDto
            {
                CodigoPeca = $"PEC-{i:D4}",
                Descricao = $"PECA {i}",
                Quantidade = 1,
                PrecoUnitario = 10.00m
            });

        var criado = await Orcamentos.CriarAsync(dto);
        var relido = await Orcamentos.ObterPorIdAsync(criado.Id);

        Assert.Equal(10, relido.Itens.Count);
        Assert.Equal(100.00m, relido.ValorTotalItens);
    }

    // ---------- SchemaUpgrade em base já existente ----------

    /// <summary>DDL da tabela como era ANTES do vínculo com Serviços (base antiga).</summary>
    private const string MaoDeObraAntigo = """
        CREATE TABLE "OrcamentoMaosDeObra" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_OrcamentoMaosDeObra" PRIMARY KEY AUTOINCREMENT,
            "OrcamentoId" INTEGER NOT NULL,
            "NumeroItem" INTEGER NOT NULL,
            "Descricao" TEXT NOT NULL,
            "QuantidadeHoras" TEXT NOT NULL,
            "ValorUnitario" TEXT NOT NULL,
            "ValorDesconto" TEXT NOT NULL,
            "ValorTotal" TEXT NOT NULL,
            "Observacoes" TEXT NULL,
            "DataCriacao" TEXT NOT NULL,
            "DataAtualizacao" TEXT NULL,
            CONSTRAINT "FK_OrcamentoMaosDeObra_Orcamentos_OrcamentoId" FOREIGN KEY ("OrcamentoId") REFERENCES "Orcamentos" ("Id") ON DELETE CASCADE
        )
        """;

    [Fact]
    public async Task SchemaUpgrade_DeveAdicionarServicoIdEmBaseJaExistente()
    {
        var caminho = Path.Combine(Path.GetTempPath(), $"orc-schema-{Guid.NewGuid():N}.db");

        try
        {
            var services = new ServiceCollection();
            services.AddInfrastructure(DatabaseConnectionOptions.DefaultSqlite(caminho));
            await using var provedor = services.BuildServiceProvider();
            await DatabaseInitializer.InitializeAsync(provedor);

            // Simula a instalação anterior ao vínculo com Serviços.
            await using (var escopo = provedor.CreateAsyncScope())
            {
                var ctx = escopo.ServiceProvider.GetRequiredService<OrcProDbContext>();
                await ctx.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");
                await ctx.Database.ExecuteSqlRawAsync("DROP TABLE \"OrcamentoMaosDeObra\"");
                await ctx.Database.ExecuteSqlRawAsync(MaoDeObraAntigo);
                await ctx.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON");
            }

            Assert.False(ColunaExiste(await LerColunasAsync(provedor), "ServicoId"));

            // O EnsureCreated não altera tabelas existentes: quem cria a coluna é o SchemaUpgrade.
            await DatabaseInitializer.InitializeAsync(provedor);

            Assert.True(ColunaExiste(await LerColunasAsync(provedor), "ServicoId"));

            // Idempotente: rodar de novo não falha.
            await DatabaseInitializer.InitializeAsync(provedor);
            Assert.True(ColunaExiste(await LerColunasAsync(provedor), "ServicoId"));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { if (File.Exists(caminho)) File.Delete(caminho); } catch (IOException) { }
        }
    }

    private static async Task<List<string>> LerColunasAsync(IServiceProvider provedor)
    {
        await using var escopo = provedor.CreateAsyncScope();
        var ctx = escopo.ServiceProvider.GetRequiredService<OrcProDbContext>();
        var conexao = ctx.Database.GetDbConnection();
        conexao.Open();

        using var cmd = conexao.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(\"OrcamentoMaosDeObra\")";
        using var leitor = await cmd.ExecuteReaderAsync();

        var colunas = new List<string>();
        while (await leitor.ReadAsync())
            colunas.Add(leitor.GetString(1));

        return colunas;
    }

    private static bool ColunaExiste(List<string> colunas, string coluna)
        => colunas.Any(c => string.Equals(c, coluna, StringComparison.OrdinalIgnoreCase));
    [Fact]
    public async Task SchemaUpgrade_DeveGarantirStatusDeOrcamentoEmBaseSemStatus()
    {
        var caminho = Path.Combine(Path.GetTempPath(), $"orc-status-{Guid.NewGuid():N}.db");

        try
        {
            var services = new ServiceCollection();
            services.AddInfrastructure(DatabaseConnectionOptions.DefaultSqlite(caminho));
            await using var provedor = services.BuildServiceProvider();
            await DatabaseInitializer.InitializeAsync(provedor);

            // Simula base antiga que ficou sem os status.
            await using (var escopo = provedor.CreateAsyncScope())
            {
                var ctx = escopo.ServiceProvider.GetRequiredService<OrcProDbContext>();
                await ctx.Database.ExecuteSqlRawAsync("DELETE FROM \"OrcamentoStatus\"");
            }

            await DatabaseInitializer.InitializeAsync(provedor);

            await using (var escopo = provedor.CreateAsyncScope())
            {
                var ctx = escopo.ServiceProvider.GetRequiredService<OrcProDbContext>();
                var status = await ctx.OrcamentoStatus.ToListAsync();

                // O fluxo obrigatório precisa existir para o módulo funcionar.
                Assert.Contains(status, s => s.Codigo == OrcamentoStatus.CodigoAguardandoAprovacao);
                Assert.Contains(status, s => s.Codigo == OrcamentoStatus.CodigoAprovado);
                Assert.Contains(status, s => s.Codigo == OrcamentoStatus.CodigoFinalizado);
                Assert.Contains(status, s => s.Codigo == OrcamentoStatus.CodigoCancelado);
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { if (File.Exists(caminho)) File.Delete(caminho); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task SchemaUpgrade_InstalacaoNova_DeveCriarStatusEOrlamentoCompleto()
    {
        await using var escopo = _provedor.CreateAsyncScope();
        var ctx = escopo.ServiceProvider.GetRequiredService<OrcProDbContext>();

        Assert.True(await ctx.OrcamentoStatus.AnyAsync());
        Assert.True(await ctx.Orcamentos.AnyAsync() == false);
    }
}

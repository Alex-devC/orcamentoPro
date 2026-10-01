using System.Linq.Expressions;
using OrcPro.Application.DTOs.Auth;
using OrcPro.Application.DTOs.Common;
using OrcPro.Application.DTOs.Orcamento;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Application.Services;
using OrcPro.Domain.Common;
using OrcPro.Domain.Entities.Cliente;
using OrcPro.Domain.Entities.Orcamento;
using OrcPro.Domain.Entities.Seguranca;
using OrcPro.Domain.Entities.Tecnico;

namespace OrcPro.Tests;

// Mock simples em memória para IPasswordHasher
public class FakePasswordHasher : IPasswordHasher
{
    public string HashPassword(string password) => $"HASH_{password}";
    public bool VerifyPassword(string password, string passwordHash) => passwordHash == $"HASH_{password}";
}

// Repositório genérico em memória para testes unitários
public class InMemoryRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly List<T> Items = new();
    private int _nextId = 1;

    public Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(i => i.Id == id));

    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<T>>(Items.ToList());

    public Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<T>>(Items.AsQueryable().Where(predicate).ToList());

    public Task<PagedResult<T>> GetPagedAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        var count = Items.Count;
        var paged = Items.Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize).ToList();
        return Task.FromResult(new PagedResult<T>(paged, count, request.PageNumber, request.PageSize));
    }

    public Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (entity.Id == 0)
            entity.Id = _nextId++;
        Items.Add(entity);
        return Task.FromResult(entity);
    }

    public Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        var idx = Items.FindIndex(i => i.Id == entity.Id);
        if (idx >= 0)
            Items[idx] = entity;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        Items.RemoveAll(i => i.Id == id);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(i => i.Id == id));
}

public class InMemoryUsuarioRepository : InMemoryRepository<Usuario>, IUsuarioRepository
{
    public Task<Usuario?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)));

    public Task<Usuario?> GetWithPerfilAndPermissoesByUsernameAsync(string username, CancellationToken cancellationToken = default)
        => GetByUsernameAsync(username, cancellationToken);

    public Task<Usuario?> GetWithPerfilAndPermissoesAsync(int id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public Task<bool> ExistsUsernameAsync(string username, int? ignorarId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase) && (!ignorarId.HasValue || u.Id != ignorarId.Value)));

    public Task<int> CountByPerfilIdAsync(int perfilId, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Count(u => u.PerfilId == perfilId));
}

public class InMemoryOrcamentoRepository : InMemoryRepository<Orcamento>, IOrcamentoRepository
{
    private int _seq = 1;

    public Task<Orcamento?> GetWithDetailsByIdAsync(int id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public Task<Orcamento?> GetByNumeroAsync(string numero, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(o => o.Numero == numero));

    public Task<int> ObterProximoSequencialAsync(int ano, CancellationToken cancellationToken = default)
        => Task.FromResult(_seq++);

    public Task<bool> ExistsNumeroAsync(string numero, int? ignorarId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(o => o.Numero == numero && (!ignorarId.HasValue || o.Id != ignorarId.Value)));

    public Task<IReadOnlyList<Orcamento>> GetByClienteIdAsync(int clienteId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Orcamento>>(Items.Where(o => o.ClienteId == clienteId).ToList());

    public Task<IReadOnlyList<Orcamento>> GetByStatusIdAsync(int statusId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Orcamento>>(Items.Where(o => o.StatusId == statusId).ToList());

    public Task<int> CountByUsuarioIdAsync(int usuarioId, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Count(o => o.UsuarioId == usuarioId));

    public Task<int> CountByClienteIdAsync(int clienteId, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Count(o => o.ClienteId == clienteId));
}

public class InMemoryOrcamentoStatusRepository : InMemoryRepository<OrcamentoStatus>, IOrcamentoStatusRepository
{
    public Task<OrcamentoStatus?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(s => string.Equals(s.Codigo, codigo, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<OrcamentoStatus>> GetAllAtivosAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<OrcamentoStatus>>(Items.Where(s => s.Ativo).ToList());
}

public class InMemoryOrcamentoHistoricoRepository : InMemoryRepository<OrcamentoHistorico>, IOrcamentoHistoricoRepository
{
    public Task<IReadOnlyList<OrcamentoHistorico>> GetByOrcamentoIdAsync(int orcamentoId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<OrcamentoHistorico>>(Items.Where(h => h.OrcamentoId == orcamentoId).ToList());
}

public class InMemoryClienteRepository : InMemoryRepository<Cliente>, IClienteRepository
{
    public Task<Cliente?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(c => c.Codigo == codigo));

    public Task<Cliente?> GetByCpfCnpjAsync(string cpfCnpj, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(c => c.CpfCnpj == cpfCnpj));

    public Task<bool> ExistsCpfCnpjAsync(string cpfCnpj, int? ignorarId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(c => c.CpfCnpj == cpfCnpj && (!ignorarId.HasValue || c.Id != ignorarId.Value)));

    public Task<string> GerarProximoCodigoAsync(CancellationToken cancellationToken = default)
        => Task.FromResult($"CLI-{Items.Count + 1:D5}");
}

public class InMemoryTecnicoRepository : InMemoryRepository<Tecnico>, ITecnicoRepository
{
    public Task<Tecnico?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(t => t.Codigo == codigo));

    public Task<Tecnico?> GetByCpfAsync(string cpf, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(t => t.Cpf == cpf));

    public Task<bool> ExistsCodigoAsync(string codigo, int? ignorarId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(t => t.Codigo == codigo && (!ignorarId.HasValue || t.Id != ignorarId.Value)));

    public Task<bool> ExistsCpfAsync(string cpf, int? ignorarId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(t => t.Cpf == cpf && (!ignorarId.HasValue || t.Id != ignorarId.Value)));

    /// <summary>Conta vínculos de orçamento a partir das coleções carregadas na entidade.</summary>
    public Task<int> CountOrcamentosAsync(int tecnicoId, CancellationToken cancellationToken = default)
    {
        var tecnico = Items.FirstOrDefault(t => t.Id == tecnicoId);

        return Task.FromResult(
            (tecnico?.OrcamentoTecnicos.Count ?? 0) +
            (tecnico?.MaoDeObraTecnicos.Count ?? 0));
    }

    public Task<string> GerarProximoCodigoAsync(CancellationToken cancellationToken = default)
        => Task.FromResult($"TEC-{Items.Count + 1:D3}");

    public Task<IReadOnlyList<Tecnico>> GetAllAtivosAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Tecnico>>(Items.Where(t => t.Ativo).ToList());
}

public class ApplicationServiceTests
{
    [Fact]
    public async Task AuthService_LoginComCredenciaisCorretas_DeveRetornarSucessoESessao()
    {
        // Arrange
        var hasher = new FakePasswordHasher();
        var userRepo = new InMemoryUsuarioRepository();

        var perfil = new Perfil { Id = 1, Nome = "Administrador" };
        var permissao = new Permissao { Id = 1, Codigo = "ORCAMENTOS_CRIAR", Nome = "Criar Orçamentos", Ativo = true };
        perfil.PerfilPermissoes.Add(new PerfilPermissao { Perfil = perfil, Permissao = permissao });

        await userRepo.AddAsync(new Usuario
        {
            Id = 1,
            Username = "alex.santos",
            PasswordHash = hasher.HashPassword("123456"),
            NomeCompleto = "Alex Santos",
            Ativo = true,
            PerfilId = 1,
            Perfil = perfil
        });

        var authService = new AuthService(userRepo, hasher);

        // Act
        var response = await authService.LoginAsync(new LoginRequestDto
        {
            Username = "alex.santos",
            Senha = "123456"
        });

        // Assert
        Assert.True(response.Sucesso);
        Assert.NotNull(response.Usuario);
        Assert.Equal("alex.santos", response.Usuario.Username);
        Assert.True(response.Usuario.PossuiPermissao("ORCAMENTOS_CRIAR"));
    }

    [Fact]
    public async Task AuthService_LoginComSenhaIncorreta_DeveRetornarFalha()
    {
        // Arrange
        var hasher = new FakePasswordHasher();
        var userRepo = new InMemoryUsuarioRepository();
        await userRepo.AddAsync(new Usuario
        {
            Id = 1,
            Username = "operador",
            PasswordHash = hasher.HashPassword("senhaCorreta"),
            Ativo = true
        });

        var authService = new AuthService(userRepo, hasher);

        // Act
        var response = await authService.LoginAsync(new LoginRequestDto
        {
            Username = "operador",
            Senha = "senhaErrada"
        });

        // Assert
        Assert.False(response.Sucesso);
        Assert.Null(response.Usuario);
    }

    [Fact]
    public async Task OrcamentoService_CriarEClonarOrcamento_DevePreservarItensEGeraNovoNumero()
    {
        // Arrange
        var orcamentoRepo = new InMemoryOrcamentoRepository();
        var statusRepo = new InMemoryOrcamentoStatusRepository();
        var historicoRepo = new InMemoryOrcamentoHistoricoRepository();
        var clienteRepo = new InMemoryClienteRepository();
        var tecnicoRepo = new InMemoryTecnicoRepository();
        var userRepo = new InMemoryUsuarioRepository();

        foreach (var st in OrcamentoStatus.CriarStatusIniciais())
            await statusRepo.AddAsync(st);

        var cliente = await clienteRepo.AddAsync(new Cliente
        {
            Id = 1,
            NomeRazaoSocial = "Cliente Teste Ltda",
            CpfCnpj = "11.222.333/0001-44",
            Celular = "(11) 99999-8888",
            Email = "teste@cliente.com"
        });

        var usuario = await userRepo.AddAsync(new Usuario
        {
            Id = 1,
            Username = "vendedor",
            NomeCompleto = "Vendedor Teste",
            Ativo = true
        });

        var orcamentoService = new OrcamentoService(
            orcamentoRepo,
            statusRepo,
            historicoRepo,
            clienteRepo,
            tecnicoRepo,
            userRepo);

        // Act 1: Criar orçamento
        var criado = await orcamentoService.CriarAsync(new CriarOrcamentoDto
        {
            ClienteId = cliente.Id,
            EmpresaId = 1,
            UsuarioId = usuario.Id,
            DiasValidade = 10,
            ItensIniciais = new()
            {
                new AdicionarItemDto
                {
                    CodigoPeca = "PC-01",
                    Descricao = "Placa de Rede",
                    Quantidade = 2,
                    PrecoUnitario = 50m
                }
            },
            MaosDeObraIniciais = new()
            {
                new AdicionarMaoDeObraDto
                {
                    Descricao = "Configuração e Instalação",
                    QuantidadeHoras = 1,
                    ValorUnitario = 120m
                }
            }
        });

        // Assert 1
        Assert.NotNull(criado);
        Assert.Equal(220m, criado.ValorTotal); // (2*50) + (1*120) = 220
        Assert.Single(criado.Itens);
        Assert.Single(criado.MaosDeObra);

        // Act 2: Clonar orçamento
        var clonado = await orcamentoService.ClonarAsync(criado.Id, usuario.Id);

        // Assert 2
        Assert.NotNull(clonado);
        Assert.NotEqual(criado.Numero, clonado.Numero);
        Assert.Equal(criado.ValorTotal, clonado.ValorTotal);
        Assert.Single(clonado.Itens);
        Assert.Single(clonado.MaosDeObra);
        Assert.Equal(OrcamentoStatus.CodigoRascunho, clonado.StatusCodigo);
    }
}

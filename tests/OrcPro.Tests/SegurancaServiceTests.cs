using OrcPro.Application.DTOs.Perfil;
using OrcPro.Application.DTOs.Usuario;
using OrcPro.Application.Exceptions;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Application.Services;
using OrcPro.Domain.Entities.Orcamento;
using OrcPro.Domain.Entities.Seguranca;
using Xunit;

namespace OrcPro.Tests;

public class InMemoryPerfilRepository : InMemoryRepository<Perfil>, IPerfilRepository
{
    public Task<Perfil?> GetWithPermissoesAsync(int id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Perfil>> GetAllAtivosAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Perfil>>(Items.Where(p => p.Ativo).OrderBy(p => p.Nome).ToList());
}

public class InMemoryPermissaoRepository : InMemoryRepository<Permissao>, IPermissaoRepository
{
}

public class SegurancaServiceTests
{
    private static UsuarioService CriarUsuarioService(
        InMemoryUsuarioRepository usuarioRepo,
        InMemoryPerfilRepository perfilRepo,
        InMemoryOrcamentoRepository orcamentoRepo,
        FakePasswordHasher hasher)
        => new(usuarioRepo, perfilRepo, hasher, orcamentoRepo);

    private static PerfilService CriarPerfilService(
        InMemoryPerfilRepository perfilRepo,
        InMemoryUsuarioRepository usuarioRepo,
        InMemoryPermissaoRepository permissaoRepo)
        => new(perfilRepo, usuarioRepo, permissaoRepo);

    [Fact]
    public async Task UsuarioService_Criar_DeveGravarSenhaComHashEVinculadaAoPerfil()
    {
        var hasher = new FakePasswordHasher();
        var usuarioRepo = new InMemoryUsuarioRepository();
        var perfilRepo = new InMemoryPerfilRepository();
        await perfilRepo.AddAsync(new Perfil { Id = 1, Nome = "Administrador", Ativo = true });

        var service = CriarUsuarioService(usuarioRepo, perfilRepo, new InMemoryOrcamentoRepository(), hasher);

        var criado = await service.CriarAsync(new CriarUsuarioDto
        {
            Username = "Tecnico01",
            NomeCompleto = "Tecnico Um",
            Senha = "segredo123",
            PerfilId = 1,
            Ativo = true
        });

        Assert.Equal("tecnico01", criado.Username); // usuário é normalizado (login nunca usa e-mail)
        Assert.Equal(1, criado.PerfilId);

        var armazenado = await usuarioRepo.GetByIdAsync(criado.Id);
        Assert.NotNull(armazenado);
        Assert.Equal(hasher.HashPassword("segredo123"), armazenado!.PasswordHash);
        Assert.StartsWith("HASH_", armazenado.PasswordHash); // senha nunca é gravada em texto puro
    }

    [Fact]
    public async Task UsuarioService_Criar_ComUsuarioDuplicado_DeveFalhar()
    {
        var hasher = new FakePasswordHasher();
        var usuarioRepo = new InMemoryUsuarioRepository();
        var perfilRepo = new InMemoryPerfilRepository();
        await perfilRepo.AddAsync(new Perfil { Id = 1, Nome = "Administrador", Ativo = true });
        await usuarioRepo.AddAsync(new Usuario { Id = 1, Username = "admin", NomeCompleto = "Administrador", PerfilId = 1 });

        var service = CriarUsuarioService(usuarioRepo, perfilRepo, new InMemoryOrcamentoRepository(), hasher);

        var erro = await Assert.ThrowsAsync<BusinessException>(() => service.CriarAsync(new CriarUsuarioDto
        {
            Username = "ADMIN",
            NomeCompleto = "Outro Admin",
            Senha = "123456",
            PerfilId = 1
        }));

        Assert.Contains("admin", erro.Message, StringComparison.OrdinalIgnoreCase);
    }



    [Fact]
    public async Task UsuarioService_Atualizar_ComNovaSenha_DeveRehashear()
    {
        var hasher = new FakePasswordHasher();
        var usuarioRepo = new InMemoryUsuarioRepository();
        var perfilRepo = new InMemoryPerfilRepository();
        await perfilRepo.AddAsync(new Perfil { Id = 1, Nome = "Administrador", Ativo = true });
        await usuarioRepo.AddAsync(new Usuario
        {
            Id = 1,
            Username = "admin",
            NomeCompleto = "Administrador do Sistema",
            PasswordHash = hasher.HashPassword("antiga"),
            PerfilId = 1,
            Ativo = true
        });

        var service = CriarUsuarioService(usuarioRepo, perfilRepo, new InMemoryOrcamentoRepository(), hasher);

        await service.AtualizarAsync(new AtualizarUsuarioDto
        {
            Id = 1,
            NomeCompleto = "Administrador do Sistema",
            PerfilId = 1,
            Ativo = false,
            NovaSenha = "novaSenha"
        });

        var armazenado = await usuarioRepo.GetByIdAsync(1);
        Assert.Equal(hasher.HashPassword("novaSenha"), armazenado!.PasswordHash);
        Assert.False(armazenado.Ativo);
        Assert.Equal("admin", armazenado.Username); // usuário não muda na edição
    }

    [Fact]
    public async Task UsuarioService_Atualizar_SemNovaSenha_DeveManterHash()
    {
        var hasher = new FakePasswordHasher();
        var usuarioRepo = new InMemoryUsuarioRepository();
        var perfilRepo = new InMemoryPerfilRepository();
        await perfilRepo.AddAsync(new Perfil { Id = 1, Nome = "Administrador", Ativo = true });
        await usuarioRepo.AddAsync(new Usuario
        {
            Id = 1,
            Username = "admin",
            NomeCompleto = "Administrador do Sistema",
            PasswordHash = hasher.HashPassword("antiga"),
            PerfilId = 1,
            Ativo = true
        });

        var service = CriarUsuarioService(usuarioRepo, perfilRepo, new InMemoryOrcamentoRepository(), hasher);

        await service.AtualizarAsync(new AtualizarUsuarioDto
        {
            Id = 1,
            NomeCompleto = "Administrador Renomeado",
            PerfilId = 1,
            Ativo = true
        });

        var armazenado = await usuarioRepo.GetByIdAsync(1);
        Assert.Equal(hasher.HashPassword("antiga"), armazenado!.PasswordHash);
        Assert.Equal("Administrador Renomeado", armazenado.NomeCompleto);
    }

    [Fact]
    public async Task UsuarioService_Excluir_ComOrcamentos_DeveBloquear()
    {
        var hasher = new FakePasswordHasher();
        var usuarioRepo = new InMemoryUsuarioRepository();
        var perfilRepo = new InMemoryPerfilRepository();
        await perfilRepo.AddAsync(new Perfil { Id = 1, Nome = "Administrador", Ativo = true });
        await usuarioRepo.AddAsync(new Usuario { Id = 1, Username = "admin", NomeCompleto = "Admin", PerfilId = 1 });

        var orcamentoRepo = new InMemoryOrcamentoRepository();
        await orcamentoRepo.AddAsync(new Orcamento { Numero = "0001/2026", UsuarioId = 1, StatusId = 1, ClienteId = 1, EmpresaId = 1 });

        var service = CriarUsuarioService(usuarioRepo, perfilRepo, orcamentoRepo, hasher);

        await Assert.ThrowsAsync<BusinessException>(() => service.ExcluirAsync(1));
        Assert.NotNull(await usuarioRepo.GetByIdAsync(1));
    }

    [Fact]
    public async Task UsuarioService_Excluir_SemOrcamentos_DeveRemover()
    {
        var hasher = new FakePasswordHasher();
        var usuarioRepo = new InMemoryUsuarioRepository();
        var perfilRepo = new InMemoryPerfilRepository();
        await perfilRepo.AddAsync(new Perfil { Id = 1, Nome = "Administrador", Ativo = true });
        await usuarioRepo.AddAsync(new Usuario { Id = 1, Username = "semorcamento", NomeCompleto = "Sem Orçamento", PerfilId = 1 });

        var service = CriarUsuarioService(usuarioRepo, perfilRepo, new InMemoryOrcamentoRepository(), hasher);

        await service.ExcluirAsync(1);

        Assert.Null(await usuarioRepo.GetByIdAsync(1));
    }

    [Fact]
    public async Task PerfilService_Criar_ComNomeDuplicado_DeveFalhar()
    {
        var perfilRepo = new InMemoryPerfilRepository();
        await perfilRepo.AddAsync(new Perfil { Id = 1, Nome = "Administrador", Ativo = true });

        var service = CriarPerfilService(perfilRepo, new InMemoryUsuarioRepository(), new InMemoryPermissaoRepository());

        await Assert.ThrowsAsync<BusinessException>(() => service.CriarAsync(new SalvarPerfilDto
        {
            Nome = "administrador",
            Descricao = "Acesso total"
        }));
    }

    [Fact]
    public async Task PerfilService_Criar_SemNome_DeveFalhar()
    {
        var service = CriarPerfilService(new InMemoryPerfilRepository(), new InMemoryUsuarioRepository(), new InMemoryPermissaoRepository());

        var erro = await Assert.ThrowsAsync<ValidationException>(() => service.CriarAsync(new SalvarPerfilDto { Nome = "   " }));
        Assert.Contains("obrigatório", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PerfilService_Criar_DeveSalvarNomeDescricaoEAtivo()
    {
        var perfilRepo = new InMemoryPerfilRepository();
        var service = CriarPerfilService(perfilRepo, new InMemoryUsuarioRepository(), new InMemoryPermissaoRepository());

        var criado = await service.CriarAsync(new SalvarPerfilDto
        {
            Nome = "Atendente",
            Descricao = "  Atendimento comercial  ",
            Ativo = true
        });

        Assert.Equal("Atendente", criado.Nome);
        Assert.Equal("Atendimento comercial", criado.Descricao);
        Assert.True(criado.Ativo);
        Assert.Empty(criado.Permissoes);
    }

    [Fact]
    public async Task PerfilService_Atualizar_DeveManterPermissoesEGerarVinculos()
    {
        var permissaoRepo = new InMemoryPermissaoRepository();
        var permissao = await permissaoRepo.AddAsync(new Permissao { Id = 1, Codigo = "USUARIOS_VER", Nome = "Ver usuários", Modulo = "Usuários" });

        var perfilRepo = new InMemoryPerfilRepository();
        var perfil = await perfilRepo.AddAsync(new Perfil
        {
            Id = 1,
            Nome = "Atendente",
            Descricao = "Atendimento",
            Ativo = true,
            PerfilPermissoes = new List<PerfilPermissao> { new() { PerfilId = 1, PermissaoId = 1, Permissao = permissao } }
        });

        var service = CriarPerfilService(perfilRepo, new InMemoryUsuarioRepository(), permissaoRepo);

        var atualizado = await service.AtualizarAsync(new SalvarPerfilDto
        {
            Id = perfil.Id,
            Nome = "Atendente",
            Descricao = "Atendimento e caixa",
            Ativo = false,
            PermissaoIds = new List<int> { 1 }
        });

        Assert.False(atualizado.Ativo);
        Assert.Equal("Atendimento e caixa", atualizado.Descricao);
        Assert.Single(atualizado.Permissoes);
        Assert.Equal("USUARIOS_VER", atualizado.Permissoes[0].Codigo);
    }

    [Fact]
    public async Task PerfilService_Excluir_ComUsuarios_DeveBloquear()
    {
        var perfilRepo = new InMemoryPerfilRepository();
        await perfilRepo.AddAsync(new Perfil { Id = 1, Nome = "Administrador", Ativo = true });

        var usuarioRepo = new InMemoryUsuarioRepository();
        await usuarioRepo.AddAsync(new Usuario { Id = 1, Username = "admin", NomeCompleto = "Admin", PerfilId = 1 });

        var service = CriarPerfilService(perfilRepo, usuarioRepo, new InMemoryPermissaoRepository());

        await Assert.ThrowsAsync<BusinessException>(() => service.ExcluirAsync(1));
        Assert.NotNull(await perfilRepo.GetByIdAsync(1));
    }

    [Fact]
    public async Task PerfilService_Excluir_SemUsuarios_DeveRemover()
    {
        var perfilRepo = new InMemoryPerfilRepository();
        await perfilRepo.AddAsync(new Perfil { Id = 1, Nome = "Temporario", Ativo = true });

        var service = CriarPerfilService(perfilRepo, new InMemoryUsuarioRepository(), new InMemoryPermissaoRepository());

        await service.ExcluirAsync(1);

        Assert.Null(await perfilRepo.GetByIdAsync(1));
    }

    [Fact]
    public async Task PerfilService_ListarAtivos_DeveRetornarSomentePerfisAtivos()
    {
        var perfilRepo = new InMemoryPerfilRepository();
        await perfilRepo.AddAsync(new Perfil { Id = 1, Nome = "Administrador", Ativo = true });
        await perfilRepo.AddAsync(new Perfil { Id = 2, Nome = "Inativo", Ativo = false });

        var usuarioRepo = new InMemoryUsuarioRepository();
        await usuarioRepo.AddAsync(new Usuario { Id = 1, Username = "admin", NomeCompleto = "Admin", PerfilId = 1 });

        var service = CriarPerfilService(perfilRepo, usuarioRepo, new InMemoryPermissaoRepository());

        var ativos = await service.ListarAtivosAsync();

        Assert.Single(ativos);
        Assert.Equal("Administrador", ativos[0].Nome);
    }
}

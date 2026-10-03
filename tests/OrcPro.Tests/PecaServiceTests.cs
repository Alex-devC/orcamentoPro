using OrcPro.Application.DTOs.Common;
using OrcPro.Application.DTOs.Peca;
using OrcPro.Application.Exceptions;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Application.Services;
using OrcPro.Domain.Entities.Peca;
using Xunit;

namespace OrcPro.Tests;

public class InMemoryPecaRepository : InMemoryRepository<Peca>, IPecaRepository
{
    public IReadOnlyList<Peca> ItemsList => Items.ToList();

    public Task<Peca?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(p => p.Codigo == codigo));

    public Task<bool> ExistsCodigoAsync(string codigo, int? ignorarId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(p => p.Codigo == codigo && (!ignorarId.HasValue || p.Id != ignorarId.Value)));

    public Task<IReadOnlyList<Peca>> GetAllAtivosAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Peca>>(Items.Where(p => p.Ativo).ToList());
}

public class PecaServiceTests
{
    private static PecaService CriarServico(InMemoryPecaRepository repo)
        => new(repo);

    private static CriarPecaDto NovaPeca(string codigo = "PEC-001", string descricao = "Parafuso") => new()
    {
        Codigo = codigo,
        Descricao = descricao,
        Categoria = "Ferragens",
        Marca = "3M",
        Modelo = "P1",
        CodigoBarras = "7891234567890",
        UnidadeMedida = "UN",
        PrecoCusto = 1.50m,
        PrecoVenda = 3.00m,
        EstoqueAtual = 100m,
        EstoqueMinimo = 10m,
        Ativo = true
    };

    [Fact]
    public async Task PecaService_Criar_DeveNormalizarCamposECapitalizarCodigo()
    {
        var repo = new InMemoryPecaRepository();
        var service = CriarServico(repo);

        var dto = NovaPeca(codigo: "pec-001", descricao: "parafuso");
        var criado = await service.CriarAsync(dto);

        Assert.Equal("PEC-001", criado.Codigo);
        Assert.Equal("PARAFUSO", criado.Descricao);
        Assert.Equal("FERRAGENS", criado.Categoria);
        Assert.Equal("3M", criado.Marca);
        Assert.Equal("P1", criado.Modelo);
        Assert.Equal("7891234567890", criado.CodigoBarras);
        Assert.Equal("UN", criado.UnidadeMedida);
        Assert.Equal(1.50m, criado.PrecoCusto);
        Assert.Equal(3.00m, criado.PrecoVenda);
        Assert.Equal(100m, criado.EstoqueAtual);
        Assert.Equal(10m, criado.EstoqueMinimo);
        Assert.True(criado.Ativo);
    }

    [Fact]
    public async Task PecaService_Criar_ComCodigoDuplicado_DeveFalhar()
    {
        var repo = new InMemoryPecaRepository();
        await repo.AddAsync(new Peca { Id = 1, Codigo = "PEC-001", Descricao = "Existente" });
        var service = CriarServico(repo);

        await Assert.ThrowsAsync<BusinessException>(() => service.CriarAsync(NovaPeca(codigo: "PEC-001")));
    }

    [Fact]
    public async Task PecaService_Criar_ComCodigoVazio_DeveFalhar()
    {
        var service = CriarServico(new InMemoryPecaRepository());

        await Assert.ThrowsAsync<ValidationException>(() => service.CriarAsync(NovaPeca(codigo: "")));
    }

    [Fact]
    public async Task PecaService_Criar_ComDescricaoVazia_DeveFalhar()
    {
        var service = CriarServico(new InMemoryPecaRepository());

        var dto = NovaPeca();
        dto.Descricao = "";

        await Assert.ThrowsAsync<ValidationException>(() => service.CriarAsync(dto));
    }

    [Fact]
    public async Task PecaService_Atualizar_DeveAtualizarTodosOsCampos()
    {
        var repo = new InMemoryPecaRepository();
        await repo.AddAsync(new Peca
        {
            Id = 1,
            Codigo = "PEC-001",
            Descricao = "Parafuso",
            Categoria = "Ferragens",
            Marca = "3M",
            Modelo = "P1",
            CodigoBarras = "7891234567890",
            UnidadeMedida = "UN",
            PrecoCusto = 1.50m,
            PrecoVenda = 3.00m,
            EstoqueAtual = 100m,
            EstoqueMinimo = 10m,
            Ativo = true
        });
        var service = CriarServico(repo);

        var atualizado = await service.AtualizarAsync(new AtualizarPecaDto
        {
            Id = 1,
            Codigo = "PEC-001",
            Descricao = "Parafuso Atualizado",
            Categoria = "Ferragens Novas",
            Marca = "Inox",
            Modelo = "P2",
            CodigoBarras = "789000111222",
            UnidadeMedida = "PC",
            PrecoCusto = 2.00m,
            PrecoVenda = 5.00m,
            EstoqueAtual = 50m,
            EstoqueMinimo = 5m,
            Ativo = false
        });

        Assert.Equal("PARAFUSO ATUALIZADO", atualizado.Descricao);
        Assert.Equal("FERRAGENS NOVAS", atualizado.Categoria);
        Assert.Equal("INOX", atualizado.Marca);
        Assert.Equal("P2", atualizado.Modelo);
        Assert.Equal("789000111222", atualizado.CodigoBarras);
        Assert.Equal("PC", atualizado.UnidadeMedida);
        Assert.Equal(2.00m, atualizado.PrecoCusto);
        Assert.Equal(5.00m, atualizado.PrecoVenda);
        Assert.Equal(50m, atualizado.EstoqueAtual);
        Assert.Equal(5m, atualizado.EstoqueMinimo);
        Assert.False(atualizado.Ativo);
    }

    [Fact]
    public async Task PecaService_Atualizar_ComCodigoDeOutraPeca_DeveFalhar()
    {
        var repo = new InMemoryPecaRepository();
        await repo.AddAsync(new Peca { Id = 1, Codigo = "PEC-001", Descricao = "Peca 1" });
        await repo.AddAsync(new Peca { Id = 2, Codigo = "PEC-002", Descricao = "Peca 2" });
        var service = CriarServico(repo);

        await Assert.ThrowsAsync<BusinessException>(() => service.AtualizarAsync(new AtualizarPecaDto
        {
            Id = 2,
            Codigo = "PEC-001",
            Descricao = "Peca 2 Editada"
        }));
    }

    [Fact]
    public async Task PecaService_Inativar_DeveMarcarAtivoFalse()
    {
        var repo = new InMemoryPecaRepository();
        await repo.AddAsync(new Peca { Id = 1, Codigo = "PEC-001", Descricao = "Parafuso", Ativo = true });
        var service = CriarServico(repo);

        await service.InativarAsync(1);

        var peca = repo.ItemsList.First(p => p.Id == 1);
        Assert.False(peca.Ativo);
    }

    [Fact]
    public async Task PecaService_Excluir_DeveRemoverPeca()
    {
        var repo = new InMemoryPecaRepository();
        await repo.AddAsync(new Peca { Id = 1, Codigo = "PEC-001", Descricao = "Parafuso" });
        var service = CriarServico(repo);

        await service.ExcluirAsync(1);

        Assert.Empty(repo.ItemsList);
    }

    [Fact]
    public async Task PecaService_ObterPorId_Inexistente_DeveLancarNotFoundException()
    {
        var service = CriarServico(new InMemoryPecaRepository());

        await Assert.ThrowsAsync<NotFoundException>(() => service.ObterPorIdAsync(999));
    }

    [Fact]
    public async Task PecaService_Atualizar_Inexistente_DeveLancarNotFoundException()
    {
        var service = CriarServico(new InMemoryPecaRepository());

        await Assert.ThrowsAsync<NotFoundException>(() => service.AtualizarAsync(new AtualizarPecaDto
        {
            Id = 999,
            Codigo = "PEC-001",
            Descricao = "Parafuso"
        }));
    }

    [Fact]
    public async Task PecaService_Criar_ComPrecoVendaNegativo_DeveFalhar()
    {
        var service = CriarServico(new InMemoryPecaRepository());

        var dto = NovaPeca();
        dto.PrecoVenda = -1m;

        await Assert.ThrowsAsync<ValidationException>(() => service.CriarAsync(dto));
    }

    [Fact]
    public async Task PecaService_ListarPaginado_DeveRetornarPaginadoCorretamente()
    {
        var repo = new InMemoryPecaRepository();

        for (int i = 1; i <= 5; i++)
        {
            await repo.AddAsync(new Peca
            {
                Id = i,
                Codigo = $"PEC-{i:D3}",
                Descricao = $"Peça {i}",
                Ativo = true
            });
        }

        var service = CriarServico(repo);

        var request = new PagedRequest
        {
            PageNumber = 1,
            PageSize = 2,
            SearchTerm = null
        };

        request.Filters.Add(new FilterRequest { PropertyName = "Status", Value = "todos" });

        var result = await service.ListarPaginadoAsync(request);

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("PEC-001", result.Items[0].Codigo);
    }
}

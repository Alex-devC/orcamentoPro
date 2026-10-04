using OrcPro.Application.DTOs.Common;
using OrcPro.Application.DTOs.Servico;
using OrcPro.Application.Exceptions;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Application.Services;
using OrcPro.Domain.Entities.Servico;
using Xunit;

namespace OrcPro.Tests;

public class InMemoryServicoRepository : InMemoryRepository<Servico>, IServicoRepository
{
    /// <summary>Quantidade de vínculos de orçamento simulados (para o bloqueio de exclusão).</summary>
    public int VinculosOrcamento { get; set; }

    public IReadOnlyList<Servico> ItemsList => Items.ToList();

    public Task<Servico?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(s => s.Codigo == codigo));

    public Task<bool> ExistsCodigoAsync(string codigo, int? ignorarId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(s => s.Codigo == codigo && (!ignorarId.HasValue || s.Id != ignorarId.Value)));

    public Task<IReadOnlyList<Servico>> GetAllAtivosAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Servico>>(Items.Where(s => s.Ativo).ToList());

    /// <summary>
    /// Réplica da regra do <c>ServicoRepository</c>: maior código numérico + 1, considerando
    /// todos os registros (inclusive inativos), nunca a quantidade de itens.
    /// </summary>
    public Task<string> GerarProximoCodigoAsync(CancellationToken cancellationToken = default)
    {
        var maior = 0;

        foreach (var servico in Items)
        {
            var texto = (servico.Codigo ?? string.Empty).Trim();

            if (texto.StartsWith("SRV-", StringComparison.OrdinalIgnoreCase))
                texto = texto[4..];

            if (int.TryParse(texto, out var numero) && numero > maior)
                maior = numero;
        }

        return Task.FromResult($"SRV-{(maior + 1).ToString().PadLeft(4, '0')}");
    }

    public Task<int> CountVinculosOrcamentoAsync(int servicoId, CancellationToken cancellationToken = default)
        => Task.FromResult(VinculosOrcamento);
}

public class ServicoServiceTests
{
    private static ServicoService CriarServico(InMemoryServicoRepository repo)
        => new(repo);

    private static CriarServicoDto NovoServico(
        string codigo = "SRV-0001",
        string descricao = "Instalação",
        string? categoria = "MONTAGEM",
        decimal valor = 150m,
        decimal tempo = 1m)
        => new()
        {
            Codigo = codigo,
            Descricao = descricao,
            Categoria = categoria,
            Valor = valor,
            Unidade = "HR",
            TempoEstimado = tempo,
            Ativo = true
        };

    // ---------- Criação / normalização ----------

    [Fact]
    public async Task ServicoService_Criar_DeveNormalizarTextoEmMaiusculas()
    {
        var repo = new InMemoryServicoRepository();
        var service = CriarServico(repo);

        var dto = NovoServico(codigo: "srv-0001", descricao: "instalação de sistema", categoria: "montagem");
        dto.Unidade = "hr";
        dto.Observacoes = "garantia de 90 dias";

        var criado = await service.CriarAsync(dto);

        Assert.Equal("SRV-0001", criado.Codigo);
        Assert.Equal("INSTALAÇÃO DE SISTEMA", criado.Descricao);
        Assert.Equal("MONTAGEM", criado.Categoria);
        Assert.Equal("HR", criado.Unidade);
        Assert.Equal("GARANTIA DE 90 DIAS", criado.Observacoes);
        Assert.Equal(150m, criado.Valor);
        Assert.Equal(1m, criado.TempoEstimado);
        Assert.True(criado.Ativo);
    }

    [Fact]
    public async Task ServicoService_Criar_ComDescricaoVazia_DeveFalhar()
    {
        var service = CriarServico(new InMemoryServicoRepository());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CriarAsync(NovoServico(descricao: "   ")));
    }

    [Fact]
    public async Task ServicoService_Criar_ComCodigoVazio_DeveFalhar()
    {
        var service = CriarServico(new InMemoryServicoRepository());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CriarAsync(NovoServico(codigo: "")));
    }

    [Fact]
    public async Task ServicoService_Criar_ComValorNegativo_DeveFalhar()
    {
        var service = CriarServico(new InMemoryServicoRepository());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CriarAsync(NovoServico(valor: -1m)));
    }

    [Fact]
    public async Task ServicoService_Criar_ComTempoEstimadoNegativo_DeveFalhar()
    {
        var service = CriarServico(new InMemoryServicoRepository());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CriarAsync(NovoServico(tempo: -0.5m)));
    }

    [Fact]
    public async Task ServicoService_Criar_ComCodigoDuplicado_DeveFalhar()
    {
        var repo = new InMemoryServicoRepository();
        await repo.AddAsync(new Servico { Id = 1, Codigo = "SRV-0001", Descricao = "Padrão" });
        var service = CriarServico(repo);

        await Assert.ThrowsAsync<BusinessException>(() =>
            service.CriarAsync(NovoServico(codigo: "SRV-0001")));
    }

    // ---------- Código automático ----------

    [Fact]
    public async Task ServicoService_GerarProximoCodigo_EmBaseVazia_DeveComecarEmSrv0001()
    {
        var service = CriarServico(new InMemoryServicoRepository());

        Assert.Equal("SRV-0001", await service.GerarProximoCodigoAsync());
    }

    [Fact]
    public async Task ServicoService_GerarProximoCodigo_DeveAvancarSequencialmente()
    {
        var repo = new InMemoryServicoRepository();
        var service = CriarServico(repo);

        for (int i = 1; i <= 3; i++)
        {
            var codigo = await service.GerarProximoCodigoAsync();
            await service.CriarAsync(NovoServico(codigo: codigo, descricao: $"Serviço {i}"));
        }

        Assert.Equal("SRV-0004", await service.GerarProximoCodigoAsync());
    }

    [Fact]
    public async Task ServicoService_GerarProximoCodigo_AposExclusao_NaoPodeReaproveitarCodigoExcluido()
    {
        var repo = new InMemoryServicoRepository();
        var service = CriarServico(repo);

        var primeiro = await service.CriarAsync(NovoServico(codigo: "SRV-0001"));
        await service.CriarAsync(NovoServico(codigo: "SRV-0002"));
        await service.CriarAsync(NovoServico(codigo: "SRV-0003"));

        // Exclui o serviço do meio: o código SRV-0002 não pode voltar a ser oferecido.
        await service.ExcluirAsync(primeiro.Id + 1);

        Assert.Equal("SRV-0004", await service.GerarProximoCodigoAsync());
    }

    [Fact]
    public async Task ServicoService_GerarProximoCodigo_DeConsiderarRegistrosInativos()
    {
        var repo = new InMemoryServicoRepository();
        var service = CriarServico(repo);

        await service.CriarAsync(NovoServico(codigo: "SRV-0001"));
        await service.CriarAsync(NovoServico(codigo: "SRV-0002"));
        await service.InativarAsync(2);

        Assert.Equal("SRV-0003", await service.GerarProximoCodigoAsync());
    }

    // ---------- Edição ----------

    [Fact]
    public async Task ServicoService_Atualizar_NaoPodeAlterarOCodigo()
    {
        var repo = new InMemoryServicoRepository();
        var service = CriarServico(repo);

        var criado = await service.CriarAsync(NovoServico(codigo: "SRV-0001", descricao: "Original"));

        var atualizado = await service.AtualizarAsync(new AtualizarServicoDto
        {
            Id = criado.Id,
            Codigo = "SRV-9999",
            Descricao = "Alterada",
            Valor = 200m,
            Unidade = "HR",
            TempoEstimado = 2m
        });

        Assert.Equal("SRV-0001", atualizado.Codigo);

        var naBase = await service.ObterPorIdAsync(criado.Id);
        Assert.Equal("SRV-0001", naBase.Codigo);
    }

    [Fact]
    public async Task ServicoService_Atualizar_DeveAplicarMaiusculasEDadosNovos()
    {
        var repo = new InMemoryServicoRepository();
        var service = CriarServico(repo);

        var criado = await service.CriarAsync(NovoServico());

        var atualizado = await service.AtualizarAsync(new AtualizarServicoDto
        {
            Id = criado.Id,
            Codigo = criado.Codigo,
            Descricao = "revisão preventiva",
            Categoria = "manutenção",
            Valor = 1250.75m,
            Unidade = "dia",
            TempoEstimado = 3.5m,
            Observacoes = "inclui deslocamento",
            Ativo = false
        });

        Assert.Equal("REVISÃO PREVENTIVA", atualizado.Descricao);
        Assert.Equal("MANUTENÇÃO", atualizado.Categoria);
        Assert.Equal("DIA", atualizado.Unidade);
        Assert.Equal("INCLUI DESLOCAMENTO", atualizado.Observacoes);
        Assert.Equal(1250.75m, atualizado.Valor);
        Assert.Equal(3.5m, atualizado.TempoEstimado);
        Assert.False(atualizado.Ativo);
    }

    [Fact]
    public async Task ServicoService_Atualizar_Inexistente_DeveLancarNotFound()
    {
        var service = CriarServico(new InMemoryServicoRepository());

        await Assert.ThrowsAsync<NotFoundException>(() => service.AtualizarAsync(new AtualizarServicoDto
        {
            Id = 999,
            Codigo = "SRV-0001",
            Descricao = "Serviço"
        }));
    }

    [Fact]
    public async Task ServicoService_ObterPorId_DeveDevolverAFicha()
    {
        var repo = new InMemoryServicoRepository();
        var service = CriarServico(repo);

        var criado = await service.CriarAsync(NovoServico());

        var ficha = await service.ObterPorIdAsync(criado.Id);

        Assert.Equal(criado.Id, ficha.Id);
        Assert.Equal("SRV-0001", ficha.Codigo);
        Assert.Equal("INSTALAÇÃO", ficha.Descricao);
    }

    [Fact]
    public async Task ServicoService_ObterPorId_Inexistente_DeveLancarNotFound()
    {
        var service = CriarServico(new InMemoryServicoRepository());

        await Assert.ThrowsAsync<NotFoundException>(() => service.ObterPorIdAsync(999));
    }
    // ---------- Situação ----------

    [Fact]
    public async Task ServicoService_Inativar_DeveMarcarAtivoFalse()
    {
        var repo = new InMemoryServicoRepository();
        var service = CriarServico(repo);
        var criado = await service.CriarAsync(NovoServico());

        await service.InativarAsync(criado.Id);

        Assert.False((await service.ObterPorIdAsync(criado.Id)).Ativo);
    }

    [Fact]
    public async Task ServicoService_Ativar_DeveMarcarAtivoTrue()
    {
        var repo = new InMemoryServicoRepository();
        var service = CriarServico(repo);
        var criado = await service.CriarAsync(NovoServico());

        await service.InativarAsync(criado.Id);
        await service.AtivarAsync(criado.Id);

        Assert.True((await service.ObterPorIdAsync(criado.Id)).Ativo);
    }

    // ---------- Exclusão ----------

    [Fact]
    public async Task ServicoService_Excluir_DeveRemoverServico()
    {
        var repo = new InMemoryServicoRepository();
        var service = CriarServico(repo);
        var criado = await service.CriarAsync(NovoServico());

        await service.ExcluirAsync(criado.Id);

        Assert.Empty(repo.ItemsList);
    }

    [Fact]
    public async Task ServicoService_Excluir_Inexistente_DeveLancarNotFound()
    {
        var service = CriarServico(new InMemoryServicoRepository());

        await Assert.ThrowsAsync<NotFoundException>(() => service.ExcluirAsync(999));
    }

    [Fact]
    public async Task ServicoService_Excluir_ComVinculoEmOrcamento_DeveBloquear()
    {
        var repo = new InMemoryServicoRepository();
        var service = CriarServico(repo);
        var criado = await service.CriarAsync(NovoServico());

        repo.VinculosOrcamento = 2;

        var erro = await Assert.ThrowsAsync<BusinessException>(() => service.ExcluirAsync(criado.Id));

        Assert.Contains("não pode ser excluído", erro.Message);
        Assert.Single(repo.ItemsList);
    }

    // ---------- Valor: entrada natural preservada ----------

    [Theory]
    [InlineData("1", 1)]
    [InlineData("10", 10)]
    [InlineData("10,50", 10.50)]
    [InlineData("1000", 1000)]
    [InlineData("1250,75", 1250.75)]
    public async Task ServicoService_ValorDigitadoNoPadraoBrasileiro_DeveSerPreservado(string digitado, double esperado)
    {
        var repo = new InMemoryServicoRepository();
        var service = CriarServico(repo);

        Assert.True(OrcPro.Domain.Common.Formatters.DecimalInputHelper.TentarConverter(digitado, out var valor));
        Assert.Equal((decimal)esperado, valor);

        var criado = await service.CriarAsync(NovoServico(valor: valor));

        Assert.Equal((decimal)esperado, criado.Valor);
    }

    [Fact]
    public async Task ServicoService_Valor1_NaoPodeVirar1000_RegressaoDaMascara()
    {
        var repo = new InMemoryServicoRepository();
        var service = CriarServico(repo);

        // Regressão: digitar "1" com máscara StringFormat=N2 em TwoWay produzia "1.000".
        Assert.True(OrcPro.Domain.Common.Formatters.DecimalInputHelper.TentarConverter("1", out var valor));
        Assert.Equal(1m, valor);
        Assert.NotEqual(1000m, valor);

        var criado = await service.CriarAsync(NovoServico(valor: valor));
        var recarregado = await service.ObterPorIdAsync(criado.Id);

        Assert.Equal(1m, recarregado.Valor);
    }

    [Fact]
    public async Task ServicoService_ValorComCentos_NaoPodeSerCorrompido()
    {
        var repo = new InMemoryServicoRepository();
        var service = CriarServico(repo);

        Assert.True(OrcPro.Domain.Common.Formatters.DecimalInputHelper.TentarConverter("10,50", out var valor));

        var criado = await service.CriarAsync(NovoServico(valor: valor));

        Assert.Equal(10.50m, criado.Valor);
    }

    // ---------- Listagem / seleção futura ----------

    [Fact]
    public async Task ServicoService_ListarTodosAtivos_DeveDevolverSomenteAtivos()
    {
        var repo = new InMemoryServicoRepository();
        var service = CriarServico(repo);

        await service.CriarAsync(NovoServico(codigo: "SRV-0001", descricao: "Ativo 1"));
        await service.CriarAsync(NovoServico(codigo: "SRV-0002", descricao: "Ativo 2"));
        await service.CriarAsync(NovoServico(codigo: "SRV-0003", descricao: "Inativo"));
        await service.InativarAsync(3);

        var ativos = await service.ListarTodosAtivosAsync();

        Assert.Equal(2, ativos.Count);
        Assert.All(ativos, s => Assert.True(s.Ativo));
    }

    [Fact]
    public async Task ServicoService_ListarPaginado_DeveRespeitarPaginaETotal()
    {
        var repo = new InMemoryServicoRepository();
        var service = CriarServico(repo);

        for (int i = 1; i <= 5; i++)
            await service.CriarAsync(NovoServico(codigo: $"SRV-{i:D4}", descricao: $"Serviço {i}"));

        var result = await service.ListarPaginadoAsync(new PagedRequest
        {
            PageNumber = 2,
            PageSize = 2,
            SearchTerm = null
        });

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task ServicoService_ObterPorId_DepoisDeRecarregar_DeveManterOsDados()
    {
        var repo = new InMemoryServicoRepository();
        var service = CriarServico(repo);

        var criado = await service.CriarAsync(NovoServico(valor: 1250.75m, tempo: 2.5m));
        var ficha = await service.ObterPorIdAsync(criado.Id);

        Assert.Equal("SRV-0001", ficha.Codigo);
        Assert.Equal(1250.75m, ficha.Valor);
        Assert.Equal(2.5m, ficha.TempoEstimado);
        Assert.Equal("INSTALAÇÃO", ficha.Descricao);
    }
}
using OrcPro.Application.DTOs.Cliente;
using OrcPro.Application.DTOs.Common;
using OrcPro.Application.Exceptions;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Application.Services;
using OrcPro.Domain.Common;
using OrcPro.Domain.Entities.Cliente;
using OrcPro.Domain.Entities.Orcamento;
using Xunit;

namespace OrcPro.Tests;

public class ClienteServiceTests
{
    // CNPJ e CPF sinteticamente válidos (apenas dígitos verificadores conferem).
    private const string CnpjValido = "11222333000181";
    private const string CpfValido = "52998224725";

    private static ClienteService CriarServico(
        InMemoryClienteRepository clienteRepo,
        InMemoryOrcamentoRepository orcamentoRepo)
        => new(clienteRepo, orcamentoRepo);

    private static CriarClienteDto NovoCliente(string nome = "Cliente Teste Ltda", string? cpfCnpj = CnpjValido) => new()
    {
        TipoPessoa = "PJ",
        NomeRazaoSocial = nome,
        CpfCnpj = cpfCnpj,
        Celular = "(11) 99999-8888",
        Email = "teste@cliente.com"
    };

    [Fact]
    public void CpfCnpjValidator_DeveAceitarCpfECnpjValidosEReceberInvalidos()
    {
        Assert.True(CpfCnpjValidator.EhValido(CpfValido));
        Assert.True(CpfCnpjValidator.EhValido(CnpjValido));
        Assert.True(CpfCnpjValidator.EhValido("11.222.333/0001-81")); // com máscara
        Assert.True(CpfCnpjValidator.EhValido("529.982.247-25"));

        Assert.False(CpfCnpjValidator.EhValido("11222333000199")); // dígito verificador errado
        Assert.False(CpfCnpjValidator.EhValido("52998224726"));
        Assert.False(CpfCnpjValidator.EhValido("11111111111")); // dígitos repetidos
        Assert.False(CpfCnpjValidator.EhValido("123"));
        Assert.False(CpfCnpjValidator.EhValido(null));
        Assert.False(CpfCnpjValidator.EhValido(""));
    }

    [Fact]
    public void CpfCnpjValidator_NormalizarEFormatar_DevemGerarMascaraPadrao()
    {
        Assert.Equal(CnpjValido, CpfCnpjValidator.Normalizar("11.222.333/0001-81"));
        Assert.Equal("11.222.333/0001-81", CpfCnpjValidator.Formatar(CnpjValido));
        Assert.Equal("529.982.247-25", CpfCnpjValidator.Formatar(CpfValido));
    }

    [Fact]
    public async Task ClienteService_Criar_DeveGravarCpfCnpjNormalizadoEGerarCodigo()
    {
        var clienteRepo = new InMemoryClienteRepository();
        var service = CriarServico(clienteRepo, new InMemoryOrcamentoRepository());

        var criado = await service.CriarAsync(NovoCliente());

        Assert.StartsWith("CLI-", criado.Codigo);
        Assert.Equal(CnpjValido, criado.CpfCnpj);
        Assert.Equal("PJ", criado.TipoPessoa);
        Assert.True(criado.Ativo);
        Assert.Equal(0, criado.QuantidadeOrcamentos);
    }

    [Fact]
    public async Task ClienteService_Criar_SemCpfCnpj_DevePermitirPessoaFisica()
    {
        var service = CriarServico(new InMemoryClienteRepository(), new InMemoryOrcamentoRepository());

        var criado = await service.CriarAsync(new CriarClienteDto
        {
            TipoPessoa = "PF",
            NomeRazaoSocial = "Maria Souza",
            Celular = "(11) 98888-7777",
            Email = "maria@email.com"
        });

        Assert.True(string.IsNullOrEmpty(criado.CpfCnpj));
        Assert.Equal("PF", criado.TipoPessoa);
    }
[Fact]
    public async Task ClienteService_Criar_ComCpfCnpjInvalido_DeveFalhar()
    {
        var service = CriarServico(new InMemoryClienteRepository(), new InMemoryOrcamentoRepository());

        await Assert.ThrowsAsync<ValidationException>(() => service.CriarAsync(NovoCliente(cpfCnpj: "11222333000199")));
    }

    [Fact]
    public async Task ClienteService_Criar_ComCpfCnpjDuplicado_DeveFalhar()
    {
        var clienteRepo = new InMemoryClienteRepository();
        await clienteRepo.AddAsync(new Cliente { Id = 1, Codigo = "CLI-00001", NomeRazaoSocial = "Outro", CpfCnpj = CnpjValido });

        var service = CriarServico(clienteRepo, new InMemoryOrcamentoRepository());

        // Inclusive com máscara diferente: a comparação usa apenas os dígitos.
        await Assert.ThrowsAsync<BusinessException>(() => service.CriarAsync(NovoCliente(cpfCnpj: "11.222.333/0001-81")));
    }

    [Fact]
    public async Task ClienteService_Criar_ComCodigoDuplicado_DeveFalhar()
    {
        var clienteRepo = new InMemoryClienteRepository();
        await clienteRepo.AddAsync(new Cliente { Id = 1, Codigo = "CLI-00001", NomeRazaoSocial = "Existe", Celular = "11", Email = "a@a.com" });

        var service = CriarServico(clienteRepo, new InMemoryOrcamentoRepository());

        var dto = NovoCliente();
        dto.Codigo = "CLI-00001";

        await Assert.ThrowsAsync<BusinessException>(() => service.CriarAsync(dto));
    }

    [Fact]
    public async Task ClienteService_Atualizar_ComCpfCnpjDeOutroCliente_DeveFalhar()
    {
        var clienteRepo = new InMemoryClienteRepository();
        await clienteRepo.AddAsync(new Cliente { Id = 1, Codigo = "CLI-00001", NomeRazaoSocial = "Cliente 1", CpfCnpj = CnpjValido, Celular = "11", Email = "a@a.com" });
        await clienteRepo.AddAsync(new Cliente { Id = 2, Codigo = "CLI-00002", NomeRazaoSocial = "Cliente 2", CpfCnpj = CpfValido, Celular = "22", Email = "b@b.com" });

        var service = CriarServico(clienteRepo, new InMemoryOrcamentoRepository());

        var erro = await Assert.ThrowsAsync<BusinessException>(() => service.AtualizarAsync(new AtualizarClienteDto
        {
            Id = 2,
            NomeRazaoSocial = "Cliente 2",
            CpfCnpj = CnpjValido,
            Celular = "22",
            Email = "b@b.com"
        }));

        Assert.Contains("11.222.333/0001-81", erro.Message);
    }

    [Fact]
    public async Task ClienteService_Atualizar_DeveNormalizarCamposEAtualizarSituacao()
    {
        var clienteRepo = new InMemoryClienteRepository();
        await clienteRepo.AddAsync(new Cliente
        {
            Id = 1,
            Codigo = "CLI-00001",
            NomeRazaoSocial = "Cliente 1",
            CpfCnpj = CnpjValido,
            Celular = "11",
            Email = "a@a.com",
            Ativo = true
        });

        var service = CriarServico(clienteRepo, new InMemoryOrcamentoRepository());

        var atualizado = await service.AtualizarAsync(new AtualizarClienteDto
        {
            Id = 1,
            NomeRazaoSocial = "Cliente 1 Atualizado",
            CpfCnpj = "529.982.247-25",
            Uf = "sp",
            Cidade = "São Paulo",
            Celular = "11",
            Email = "a@a.com",
            Ativo = false
        });

        Assert.Equal("Cliente 1 Atualizado", atualizado.NomeRazaoSocial);
        Assert.Equal(CpfValido, atualizado.CpfCnpj);
        Assert.Equal("SP", atualizado.Uf);
        Assert.False(atualizado.Ativo);
    }
[Fact]
    public async Task ClienteService_Excluir_ComOrcamentos_DeveBloquearEOrientarInativar()
    {
        var clienteRepo = new InMemoryClienteRepository();
        await clienteRepo.AddAsync(new Cliente { Id = 1, Codigo = "CLI-00001", NomeRazaoSocial = "Cliente Vinculado", Celular = "11", Email = "a@a.com" });

        var orcamentoRepo = new InMemoryOrcamentoRepository();
        await orcamentoRepo.AddAsync(new Orcamento { Numero = "0001/2026", ClienteId = 1, UsuarioId = 1, StatusId = 1, EmpresaId = 1 });

        var service = CriarServico(clienteRepo, orcamentoRepo);

        var erro = await Assert.ThrowsAsync<BusinessException>(() => service.ExcluirAsync(1));
        Assert.Contains("Inative-o", erro.Message);
        Assert.NotNull(await clienteRepo.GetByIdAsync(1));

        // Alternativa permitida: inativar.
        await service.InativarAsync(1);
        var inativado = await clienteRepo.GetByIdAsync(1);
        Assert.False(inativado!.Ativo);
    }

    [Fact]
    public async Task ClienteService_Excluir_SemOrcamentos_DeveRemover()
    {
        var clienteRepo = new InMemoryClienteRepository();
        await clienteRepo.AddAsync(new Cliente { Id = 1, Codigo = "CLI-00001", NomeRazaoSocial = "Cliente Livre", Celular = "11", Email = "a@a.com" });

        var service = CriarServico(clienteRepo, new InMemoryOrcamentoRepository());

        await service.ExcluirAsync(1);

        Assert.Null(await clienteRepo.GetByIdAsync(1));
    }

    [Fact]
    public async Task ClienteService_ListarPaginado_DeveTrazerQuantidadeDeOrcamentos()
    {
        var clienteRepo = new InMemoryClienteRepository();
        await clienteRepo.AddAsync(new Cliente { Id = 1, Codigo = "CLI-00001", NomeRazaoSocial = "Com Orçamento", Celular = "11", Email = "a@a.com", Ativo = true });
        await clienteRepo.AddAsync(new Cliente { Id = 2, Codigo = "CLI-00002", NomeRazaoSocial = "Sem Orçamento", Celular = "22", Email = "b@b.com", Ativo = false });

        var orcamentoRepo = new InMemoryOrcamentoRepository();
        await orcamentoRepo.AddAsync(new Orcamento { Numero = "0001/2026", ClienteId = 1, UsuarioId = 1, StatusId = 1, EmpresaId = 1 });

        var service = CriarServico(clienteRepo, orcamentoRepo);

        var result = await service.ListarPaginadoAsync(new PagedRequest { PageNumber = 1, PageSize = 10 });

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.Items.Single(c => c.Codigo == "CLI-00001").QuantidadeOrcamentos);
        Assert.Equal(0, result.Items.Single(c => c.Codigo == "CLI-00002").QuantidadeOrcamentos);
    }
}

using OrcPro.Application.DTOs.Cliente;
using OrcPro.Application.DTOs.Common;
using OrcPro.Application.DTOs.Tecnico;
using OrcPro.Application.Exceptions;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Application.Services;
using OrcPro.Domain.Common;
using OrcPro.Domain.Entities.Cliente;
using OrcPro.Domain.Entities.Orcamento;
using OrcPro.Domain.Entities.Tecnico;
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

        Assert.Equal("CLIENTE 1 ATUALIZADO", atualizado.NomeRazaoSocial);
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

/// <summary>
/// Regras do cadastro de Técnicos: CPF validado/normalizado e único, código único, e a
/// impossibilidade de excluir quem está vinculado a orçamentos (restando a inativação).
/// </summary>
public class TecnicoServiceTests
{
    // CPFs sinteticamente válidos (apenas os dígitos verificadores conferem).
    private const string CpfValido = "52998224725";
    private const string OutroCpfValido = "11144477735";

    private static TecnicoService CriarServico(InMemoryTecnicoRepository repo) => new(repo);

    private static CriarTecnicoDto NovoTecnico(string nome = "Técnico Teste", string? cpf = CpfValido) => new()
    {
        Nome = nome,
        Cpf = cpf,
        Celular = "(11) 98888-7777",
        Email = "tecnico@empresa.com"
    };

    [Fact]
    public async Task TecnicoService_Criar_DeveGerarCodigoNormalizarCpfEGravarEndereco()
    {
        var repo = new InMemoryTecnicoRepository();
        var service = CriarServico(repo);

        var dto = NovoTecnico(cpf: "529.982.247-25");
        dto.Especialidade = "Elétrica";
        dto.Cep = "01001-000";
        dto.Logradouro = "Av. Paulista";
        dto.Numero = "1000";
        dto.Complemento = "Sala 5";
        dto.Bairro = "Bela Vista";
        dto.Cidade = "São Paulo";
        dto.Uf = "sp";

        var criado = await service.CriarAsync(dto);

        Assert.StartsWith("TEC-", criado.Codigo);
        Assert.Equal(CpfValido, criado.Cpf); // gravado sem máscara
        Assert.Equal("ELÉTRICA", criado.Especialidade);
        Assert.Equal("01001000", criado.Cep);
        Assert.Equal("AV. PAULISTA", criado.Logradouro);
        Assert.Equal("SALA 5", criado.Complemento);
        Assert.Equal("SP", criado.Uf); // UF normalizada em maiúsculas
        Assert.True(criado.Ativo);
        Assert.Equal(0, criado.QuantidadeOrcamentos);
    }

    [Fact]
    public async Task TecnicoService_Criar_ComCpfInvalido_DeveFalhar()
    {
        var service = CriarServico(new InMemoryTecnicoRepository());

        await Assert.ThrowsAsync<ValidationException>(() => service.CriarAsync(NovoTecnico(cpf: "52998224726")));
    }

    [Fact]
    public async Task TecnicoService_Criar_SemCpf_DeveGravarNulo()
    {
        var service = CriarServico(new InMemoryTecnicoRepository());

        var criado = await service.CriarAsync(NovoTecnico(nome: "Sem documento", cpf: null));

        Assert.True(string.IsNullOrEmpty(criado.Cpf));
    }

    [Fact]
    public async Task TecnicoService_Criar_ComCpfDuplicadoComMascaraDiferente_DeveFalhar()
    {
        var repo = new InMemoryTecnicoRepository();
        await repo.AddAsync(new Tecnico { Id = 1, Codigo = "TEC-001", Nome = "Outro", Cpf = CpfValido, Ativo = true });

        var service = CriarServico(repo);

        // Com máscara: a comparação usa apenas os dígitos, então a duplicidade é detectada.
        await Assert.ThrowsAsync<BusinessException>(() => service.CriarAsync(NovoTecnico(cpf: "529.982.247-25")));
    }

    [Fact]
    public async Task TecnicoService_Criar_ComCodigoDuplicado_DeveFalhar()
    {
        var repo = new InMemoryTecnicoRepository();
        await repo.AddAsync(new Tecnico { Id = 1, Codigo = "TEC-001", Nome = "Existe", Cpf = null, Ativo = true });

        var service = CriarServico(repo);

        var dto = NovoTecnico();
        dto.Codigo = "TEC-001";

        await Assert.ThrowsAsync<BusinessException>(() => service.CriarAsync(dto));
    }

    [Fact]
    public async Task TecnicoService_Criar_SemNome_DeveFalhar()
    {
        var service = CriarServico(new InMemoryTecnicoRepository());

        await Assert.ThrowsAsync<ValidationException>(() => service.CriarAsync(new CriarTecnicoDto { Nome = "  " }));
    }
    [Fact]
    public async Task TecnicoService_Atualizar_ComCpfDeOutroTecnico_DeveFalhar()
    {
        var repo = new InMemoryTecnicoRepository();
        await repo.AddAsync(new Tecnico { Id = 1, Codigo = "TEC-001", Nome = "Técnico 1", Cpf = CpfValido, Ativo = true });
        await repo.AddAsync(new Tecnico { Id = 2, Codigo = "TEC-002", Nome = "Técnico 2", Cpf = OutroCpfValido, Ativo = true });

        var service = CriarServico(repo);

        await Assert.ThrowsAsync<BusinessException>(() => service.AtualizarAsync(new AtualizarTecnicoDto
        {
            Id = 2,
            Nome = "Técnico 2",
            Cpf = CpfValido
        }));
    }

    [Fact]
    public async Task TecnicoService_Atualizar_MantendoOProprioCpf_DevePermitir()
    {
        var repo = new InMemoryTecnicoRepository();
        await repo.AddAsync(new Tecnico { Id = 1, Codigo = "TEC-001", Nome = "Técnico 1", Cpf = CpfValido, Ativo = true });

        var service = CriarServico(repo);

        var atualizado = await service.AtualizarAsync(new AtualizarTecnicoDto
        {
            Id = 1,
            Codigo = "TEC-001",
            Nome = "Técnico 1 Atualizado",
            Cpf = "529.982.247-25"
        });

        Assert.Equal("TÉCNICO 1 ATUALIZADO", atualizado.Nome);
        Assert.Equal(CpfValido, atualizado.Cpf);
    }

    [Fact]
    public async Task TecnicoService_Excluir_ComOrcamentosVinculados_DeveBloquearEOrientarInativar()
    {
        var repo = new InMemoryTecnicoRepository();
        var tecnico = new Tecnico { Id = 1, Codigo = "TEC-001", Nome = "Vinculado", Cpf = null, Ativo = true };
        tecnico.OrcamentoTecnicos.Add(new OrcamentoTecnico { Id = 1, TecnicoId = 1 });
        await repo.AddAsync(tecnico);

        var service = CriarServico(repo);

        var erro = await Assert.ThrowsAsync<BusinessException>(() => service.ExcluirAsync(1));
        Assert.Contains("1 orçamento", erro.Message);
        Assert.NotNull(await repo.GetByIdAsync(1));

        // Alternativa permitida: inativar.
        await service.InativarAsync(1);
        var inativado = await repo.GetByIdAsync(1);
        Assert.False(inativado!.Ativo);
    }

    [Fact]
    public async Task TecnicoService_Excluir_SemOrcamentos_DeveRemover()
    {
        var repo = new InMemoryTecnicoRepository();
        await repo.AddAsync(new Tecnico { Id = 1, Codigo = "TEC-001", Nome = "Livre", Cpf = null, Ativo = true });

        var service = CriarServico(repo);

        await service.ExcluirAsync(1);

        Assert.Null(await repo.GetByIdAsync(1));
    }

    [Fact]
    public async Task TecnicoService_ListarPaginado_DeveTrazerQuantidadeDeOrcamentos()
    {
        var repo = new InMemoryTecnicoRepository();
        await repo.AddAsync(new Tecnico { Id = 1, Codigo = "TEC-001", Nome = "Com Orçamento", Cpf = null, Ativo = true });
        var comVinculo = new Tecnico { Id = 2, Codigo = "TEC-002", Nome = "Com Mão de Obra", Cpf = null, Ativo = true };
        comVinculo.MaoDeObraTecnicos.Add(new OrcamentoMaoDeObraTecnico { Id = 1, TecnicoId = 2 });
        await repo.AddAsync(comVinculo);
        await repo.AddAsync(new Tecnico { Id = 3, Codigo = "TEC-003", Nome = "Sem Orçamento", Cpf = null, Ativo = false });

        var service = CriarServico(repo);

        var result = await service.ListarPaginadoAsync(new PagedRequest { PageNumber = 1, PageSize = 10 });

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(1, result.Items.Single(t => t.Codigo == "TEC-002").QuantidadeOrcamentos);
        Assert.Equal(0, result.Items.Single(t => t.Codigo == "TEC-003").QuantidadeOrcamentos);
    }

    [Fact]
    public void Tecnico_NaoDeveCriarVinculoComUsuario_PoisOsCadastrosSaoIndependentes()
    {
        // O cadastro de técnicos é independente: não existe propriedade de usuário na entidade
        // nem campo de usuário no DTO de criação.
        var tecnico = new Tecnico { Codigo = "TEC-001", Nome = "Técnico" };

        Assert.Null(typeof(Tecnico).GetProperty("UsuarioId"));
        Assert.Null(typeof(Tecnico).GetProperty("Usuario"));
        Assert.DoesNotContain(
            typeof(CriarTecnicoDto).GetProperties(),
            p => p.Name.Contains("Usuario", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Técnico", tecnico.Nome);
    }
}

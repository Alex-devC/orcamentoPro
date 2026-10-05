using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OrcPro.Application.DTOs.Empresa;
using OrcPro.Application.Exceptions;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Application.Services;
using OrcPro.Domain.Common.Formatters;
using OrcPro.Domain.Entities.Empresa;
using Xunit;

namespace OrcPro.Tests;

public class InMemoryEmpresaRepository : InMemoryRepository<Empresa>, IEmpresaRepository
{
    public IReadOnlyList<Empresa> ItemsList => Items.ToList();

    /// <summary>Emitente mais antigo ativo, como no <c>EmpresaRepository</c> real.</summary>
    public Task<Empresa?> GetEmitentePrincipalAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Where(e => e.Ativo).OrderBy(e => e.Id).FirstOrDefault());
}

/// <summary>Armazenamento de logo em memória, com o mesmo contrato do serviço real.</summary>
public class FakeEmpresaLogoStorage : IEmpresaLogoStorage
{
    /// <summary>Pasta temporária usada pelo fake (o serviço real usa %LocalAppData%\OrcPro\logo).</summary>
    public string Pasta { get; } = Path.Combine(Path.GetTempPath(), "orcpro-logo-fake-" + Guid.NewGuid().ToString("N"));

    public IReadOnlyList<string> ExtensoesAceitas { get; } =
        new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

    public int ChamadasImportar { get; private set; }
    public int ChamadasRemover { get; private set; }

    public string Importar(string caminhoOrigem, CancellationToken cancellationToken = default)
    {
        ChamadasImportar++;

        var extensao = System.IO.Path.GetExtension(caminhoOrigem).ToLowerInvariant();
        if (ExtensoesAceitas.Contains(extensao) == false)
            throw new ValidationException("Formato de imagem não suportado.");

        // Grava de verdade em disco: IEmpresaService.ObterLogoBytesAsync lê o arquivo
        // pelo caminho absoluto, então o fake precisa fornecer um arquivo real.
        Directory.CreateDirectory(Pasta);
        var caminho = Path.Combine(Pasta, "logo-empresa" + extensao);
        File.WriteAllBytes(caminho, Encoding.UTF8.GetBytes(caminho));

        return Path.GetFileName(caminho);
    }

    public void Remover(string? nomeArquivo)
    {
        ChamadasRemover++;

        foreach (var extensao in ExtensoesAceitas)
        {
            var caminho = Path.Combine(Pasta, "logo-empresa" + extensao);
            if (File.Exists(caminho))
                File.Delete(caminho);
        }
    }

    public string? ObterCaminhoAbsoluto(string? nomeArquivo)
    {
        if (string.IsNullOrWhiteSpace(nomeArquivo))
            return null;

        var caminho = Path.Combine(Pasta, nomeArquivo);
        return File.Exists(caminho) ? caminho : null;
    }

    public bool Existe(string? nomeArquivo) => ObterCaminhoAbsoluto(nomeArquivo) is not null;
}

public class EmpresaServiceTests
{
    private static readonly string CnpjValido = CpfCnpjValidator.GerarCnpjNumericoValido();
    private static readonly string CnpjAlfanumericoValido = CpfCnpjValidator.GerarCnpjAlfanumericoValido();

    private static (EmpresaService, InMemoryEmpresaRepository, FakeEmpresaLogoStorage) CriarServico()
    {
        var repo = new InMemoryEmpresaRepository();
        var logos = new FakeEmpresaLogoStorage();
        return (new EmpresaService(repo, logos), repo, logos);
    }

    private static SalvarEmpresaDto Nova(
        string? razao = "ALEX T.I. TECNOLOGIA LTDA",
        string? cnpj = null) => new()
    {
        RazaoSocial = razao ?? "ALEX T.I. TECNOLOGIA LTDA",
        Cnpj = cnpj ?? CnpjValido
    };

    // ---------- Estado inicial ----------

    [Fact]
    public async Task EmpresaService_SemCadastro_DeveDevolverNulo()
    {
        var (service, _, _) = CriarServico();

        Assert.Null(await service.ObterAsync());
        Assert.False(await service.EstaConfiguradoAsync());
        Assert.Null(await service.ObterLogoCaminhoAbsolutoAsync());
        Assert.Null(await service.ObterLogoBytesAsync());
    }

    [Fact]
    public async Task EmpresaService_SemCadastro_NaoPodeCriarDadosFicticios()
    {
        var (service, repo, _) = CriarServico();

        await service.ObterAsync();

        // Nenhuma empresa é criada automaticamente: o cadastro abre vazio para o usuário.
        Assert.Empty(repo.ItemsList);
    }

    // ---------- Criar / salvar / carregar / editar ----------

    [Fact]
    public async Task EmpresaService_Salvar_PrimeiroRegistro_DeveCriarEmitente()
    {
        var (service, repo, _) = CriarServico();

        var salvo = await service.SalvarAsync(Nova());

        Assert.True(salvo.Id > 0);
        Assert.Single(repo.ItemsList);
        Assert.True(await service.EstaConfiguradoAsync());
    }

    [Fact]
    public async Task EmpresaService_Salvar_NuncaCriaSegundoEmitente()
    {
        var (service, repo, _) = CriarServico();

        await service.SalvarAsync(Nova());
        await service.SalvarAsync(Nova("SEGUNDA EMPRESA"));
        await service.SalvarAsync(Nova());

        Assert.Single(repo.ItemsList);

        var atual = await service.ObterAsync();
        Assert.Equal("ALEX T.I. TECNOLOGIA LTDA", atual!.RazaoSocial);
    }

    [Fact]
    public async Task EmpresaService_Salvar_ComIdZero_ReaproveitaEmitenteExistente()
    {
        var (service, repo, _) = CriarServico();

        var primeiro = await service.SalvarAsync(Nova());

        // Formulário recarregado sem Id (simula reabrir a tela): deve editar, não inserir.
        var dto = Nova("EMPRESA ATUALIZADA");
        dto.Id = 0;
        await service.SalvarAsync(dto);

        Assert.Single(repo.ItemsList);
        Assert.Equal("EMPRESA ATUALIZADA", (await service.ObterAsync())!.RazaoSocial);
        Assert.Equal(primeiro.Id, repo.ItemsList[0].Id);
    }

    [Fact]
    public async Task EmpresaService_Obter_DeveCarregarOsDadosSalvos()
    {
        var (service, _, _) = CriarServico();

        var dto = Nova();
        dto.NomeFantasia = "ALEX Servicos";
        dto.Email = "Contato@Alexti.com.BR";
        dto.EmailFinanceiro = "Financeiro@Alexti.com.BR";
        dto.Telefone = "(11) 3333-4444";
        dto.Celular = "11988887777";
        dto.Uf = "sp";
        dto.Cidade = "Sao Paulo";
        dto.Observacoes = "Emitente principal";

        await service.SalvarAsync(dto);

        var lido = await service.ObterAsync();

        Assert.Equal("ALEX T.I. TECNOLOGIA LTDA", lido!.RazaoSocial);
        Assert.Equal("ALEX SERVICOS", lido.NomeFantasia);
        Assert.Equal("contato@alexti.com.br", lido.Email);
        Assert.Equal("financeiro@alexti.com.br", lido.EmailFinanceiro);
        Assert.Equal("1133334444", lido.Telefone);
        Assert.Equal("11988887777", lido.Celular);
        Assert.Equal("SAO PAULO", lido.Cidade);
        Assert.Equal("EMITENTE PRINCIPAL", lido.Observacoes);
    }

    // ---------- Formatação ----------

    [Fact]
    public async Task EmpresaService_TextosDevemGravarEmMaiusculas()
    {
        var (service, _, _) = CriarServico();

        var dto = Nova("alex t.i. tecnologia ltda");
        dto.NomeFantasia = "alex orcamentos";
        dto.Logradouro = "rua das flores";
        dto.Cidade = "sao paulo";
        dto.InscricaoEstadual = "isento";
        dto.Website = "www.alex.com.br";

        var salvo = await service.SalvarAsync(dto);

        Assert.Equal("ALEX T.I. TECNOLOGIA LTDA", salvo.RazaoSocial);
        Assert.Equal("ALEX ORCAMENTOS", salvo.NomeFantasia);
        Assert.Equal("RUA DAS FLORES", salvo.Logradouro);
        Assert.Equal("SAO PAULO", salvo.Cidade);
        Assert.Equal("ISENTO", salvo.InscricaoEstadual);
        Assert.Equal("WWW.ALEX.COM.BR", salvo.Website);
    }

    [Fact]
    public async Task EmpresaService_EmailsDevemGravarEmMinusculas()
    {
        var (service, _, _) = CriarServico();

        var dto = Nova();
        dto.Email = "CONTATO@ALEX.COM.BR";
        dto.EmailFinanceiro = "FINANCEIRO@ALEX.COM.BR";

        var salvo = await service.SalvarAsync(dto);

        Assert.Equal("contato@alex.com.br", salvo.Email);
        Assert.Equal("financeiro@alex.com.br", salvo.EmailFinanceiro);
    }

    [Fact]
    public async Task EmpresaService_CnpjECepETelefone_DevemSerNormalizados()
    {
        var (service, _, _) = CriarServico();

        var dto = Nova();
        dto.Cnpj = CpfCnpjValidator.Formatar(CnpjValido);
        dto.Cep = "01310-100";
        dto.Telefone = "(11) 3333-4444";

        var salvo = await service.SalvarAsync(dto);

        Assert.Equal(CnpjValido, salvo.Cnpj);
        Assert.Equal("01310100", salvo.Cep);
        Assert.Equal("1133334444", salvo.Telefone);
    }
    // ---------- Validação ----------

    [Fact]
    public async Task EmpresaService_RazaoSocialVazia_DeveFalhar()
    {
        var (service, _, _) = CriarServico();

        await Assert.ThrowsAsync<ValidationException>(() => service.SalvarAsync(Nova(razao: "   ")));
    }

    [Fact]
    public async Task EmpresaService_NomeFantasiaOpcional_DeveAceitarVazio()
    {
        var (service, _, _) = CriarServico();

        var salvo = await service.SalvarAsync(Nova());

        Assert.Equal(string.Empty, salvo.NomeFantasia);
    }

    [Fact]
    public async Task EmpresaService_CnpjNumericoValido_DeveAceitar()
    {
        var (service, _, _) = CriarServico();

        var salvo = await service.SalvarAsync(Nova(cnpj: CnpjValido));

        Assert.Equal(CnpjValido, salvo.Cnpj);
    }

    [Fact]
    public async Task EmpresaService_CnpjAlfanumericoValido_DeveAceitar()
    {
        var (service, _, _) = CriarServico();

        Assert.Equal(14, CnpjAlfanumericoValido.Length);
        Assert.Equal(DocumentoTipo.CnpjAlfanumerico, CpfCnpjValidator.Validar(CnpjAlfanumericoValido).Tipo);

        var salvo = await service.SalvarAsync(Nova(cnpj: CnpjAlfanumericoValido.ToUpperInvariant()));

        Assert.Equal(CnpjAlfanumericoValido.ToUpperInvariant(), salvo.Cnpj);
    }

    [Fact]
    public async Task EmpresaService_CnpjInvalido_DeveFalhar()
    {
        var (service, _, _) = CriarServico();

        await Assert.ThrowsAsync<ValidationException>(() => service.SalvarAsync(Nova(cnpj: "11.111.111/1111-11")));
    }

    [Fact]
    public async Task EmpresaService_CnpjComLetrasInvalido_DeveFalhar()
    {
        var (service, _, _) = CriarServico();

        await Assert.ThrowsAsync<ValidationException>(() => service.SalvarAsync(Nova(cnpj: "AB.CDE.FGH/IJKL-99")));
    }

    [Fact]
    public async Task EmpresaService_EmailInvalido_DeveFalhar()
    {
        var (service, _, _) = CriarServico();

        var dto = Nova();
        dto.Email = "email-sem-arroba.com.br";

        await Assert.ThrowsAsync<ValidationException>(() => service.SalvarAsync(dto));
    }

    [Fact]
    public async Task EmpresaService_EmailFinanceiroInvalido_DeveFalhar()
    {
        var (service, _, _) = CriarServico();

        var dto = Nova();
        dto.EmailFinanceiro = "@sem-dominio.com";

        await Assert.ThrowsAsync<ValidationException>(() => service.SalvarAsync(dto));
    }

    [Fact]
    public async Task EmpresaService_CepInvalido_DeveFalhar()
    {
        var (service, _, _) = CriarServico();

        var dto = Nova();
        dto.Cep = "123";

        await Assert.ThrowsAsync<ValidationException>(() => service.SalvarAsync(dto));
    }

    [Fact]
    public async Task EmpresaService_UfInvalida_DeveFalhar()
    {
        var (service, _, _) = CriarServico();

        var dto = Nova();
        dto.Uf = "XX";

        await Assert.ThrowsAsync<ValidationException>(() => service.SalvarAsync(dto));
    }

    [Fact]
    public async Task EmpresaService_UfValida_DeveAceitar()
    {
        var (service, _, _) = CriarServico();

        var dto = Nova();
        dto.Uf = "rj";

        var salvo = await service.SalvarAsync(dto);

        Assert.Equal("RJ", salvo.Uf);
    }

    [Fact]
    public async Task EmpresaService_TelefoneInvalido_DeveFalhar()
    {
        var (service, _, _) = CriarServico();

        var dto = Nova();
        dto.Telefone = "123";

        await Assert.ThrowsAsync<ValidationException>(() => service.SalvarAsync(dto));
    }

    // ---------- Logo ----------

    [Fact]
    public async Task EmpresaService_Logo_DeveSalvarCarregarEpersistirONomeGerenciado()
    {
        var (service, _, logos) = CriarServico();

        var dto = Nova();
        dto.NovoLogoCaminhoOrigem = @"C:\Users\Alex\Desktop\minha-logo.png";
        await service.SalvarAsync(dto);

        var salvo = await service.ObterAsync();

        // Persiste o nome gerenciado, nunca o caminho absoluto da origem.
        Assert.Equal("logo-empresa.png", salvo!.LogoPath);
        Assert.DoesNotContain("Desktop", salvo.LogoPath!);

        Assert.True(logos.Existe(salvo.LogoPath));
        Assert.NotNull(await service.ObterLogoCaminhoAbsolutoAsync());
        Assert.NotNull(await service.ObterLogoBytesAsync());
    }

    [Fact]
    public async Task EmpresaService_Logo_PodeSerSubstituida()
    {
        var (service, _, _) = CriarServico();

        var dto = Nova();
        dto.NovoLogoCaminhoOrigem = @"C:\fotos\antiga.png";
        await service.SalvarAsync(dto);

        dto = Nova();
        dto.NovoLogoCaminhoOrigem = @"C:\fotos\nova.jpg";
        await service.SalvarAsync(dto);

        var salvo = await service.ObterAsync();

        Assert.Equal("logo-empresa.jpg", salvo!.LogoPath);
        Assert.NotNull(await service.ObterLogoCaminhoAbsolutoAsync());
    }

    [Fact]
    public async Task EmpresaService_Logo_PodeSerRemovida()
    {
        var (service, _, logos) = CriarServico();

        var dto = Nova();
        dto.NovoLogoCaminhoOrigem = @"C:\fotos\logo.png";
        await service.SalvarAsync(dto);

        dto = Nova();
        dto.RemoverLogo = true;
        var salvo = await service.SalvarAsync(dto);

        Assert.Null(salvo.LogoPath);
        Assert.Null(await service.ObterLogoCaminhoAbsolutoAsync());
        Assert.Equal(1, logos.ChamadasRemover);
    }

    [Fact]
    public async Task EmpresaService_LogoSemAlteracao_DeveManterAExistente()
    {
        var (service, _, logos) = CriarServico();

        var dto = Nova();
        dto.NovoLogoCaminhoOrigem = @"C:\fotos\logo.png";
        await service.SalvarAsync(dto);

        await service.SalvarAsync(Nova());

        Assert.Equal(1, logos.ChamadasImportar);
        Assert.Equal("logo-empresa.png", (await service.ObterAsync())!.LogoPath);
    }

    [Fact]
    public async Task EmpresaService_FormatoDeLogoNaoSuportado_DeveFalhar()
    {
        var (service, _, _) = CriarServico();

        var dto = Nova();
        dto.NovoLogoCaminhoOrigem = @"C:\docs\logo.pdf";

        await Assert.ThrowsAsync<ValidationException>(() => service.SalvarAsync(dto));
    }
}

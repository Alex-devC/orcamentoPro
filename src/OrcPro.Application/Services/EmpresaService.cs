using OrcPro.Application.DTOs.Empresa;
using OrcPro.Application.Exceptions;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Common.Formatters;
using OrcPro.Domain.Entities.Empresa;

namespace OrcPro.Application.Services;

/// <summary>
/// Emitente / Minha Empresa — registro único por instalação.
/// </summary>
/// <remarks>
/// Não cria um segundo emitente: <see cref="SalvarAsync"/> sempre resolve o registro
/// existente (o mais antigo) e só insere quando realmente não há nenhum. A logo é
/// importada pelo <see cref="IEmpresaLogoStorage"/>, e no banco fica apenas o nome do
/// arquivo gerenciado.
/// </remarks>
public class EmpresaService : IEmpresaService
{
    /// <summary>Siglas das 27 unidades federativas aceitas na UF.</summary>
    private static readonly HashSet<string> UfsValidas = new(StringComparer.OrdinalIgnoreCase)
    {
        "AC","AL","AP","AM","BA","CE","DF","ES","GO","MA","MT","MS","MG",
        "PA","PB","PR","PE","PI","RJ","RN","RS","RO","RR","SC","SP","SE","TO"
    };

    private readonly IEmpresaRepository _empresaRepository;
    private readonly IEmpresaLogoStorage _logoStorage;

    public EmpresaService(IEmpresaRepository empresaRepository, IEmpresaLogoStorage logoStorage)
    {
        _empresaRepository = empresaRepository;
        _logoStorage = logoStorage;
    }

    public async Task<EmpresaDto?> ObterAsync(CancellationToken cancellationToken = default)
    {
        var empresa = await ObterRegistroAsync(cancellationToken);
        return empresa is null ? null : MapearParaDto(empresa);
    }

    public async Task<bool> EstaConfiguradoAsync(CancellationToken cancellationToken = default)
        => await ObterRegistroAsync(cancellationToken) is not null;

    public async Task<EmpresaDto> SalvarAsync(SalvarEmpresaDto dto, CancellationToken cancellationToken = default)
    {
        Validar(dto);

        // Registro único: qualquer cadastro novo reaproveita o emitente existente.
        var existente = await ObterRegistroAsync(cancellationToken);
        Empresa empresa;

        if (existente is null)
        {
            empresa = new Empresa { DataCriacao = DateTime.UtcNow, Ativo = true };
            empresa = await _empresaRepository.AddAsync(empresa, cancellationToken);
        }
        else
        {
            // Precisa ser a entidade RASTREADA. <see cref="EmpresaRepository.GetEmitentePrincipalAsync"/>
            // usa AsNoTracking, e o UpdateAsync do BaseRepository faz
            // Context.Entry(x).State = Modified: se o contexto já mantém uma instância da
            // mesma linha (criada no primeiro cadastro), configurar uma entidade destacada
            // acabaria gravando os valores ANTIGOS e a edição seria perdida.
            empresa = await _empresaRepository.GetByIdAsync(existente.Id, cancellationToken) ?? existente;
        }

        empresa.RazaoSocial = InputFormattingHelper.ToUpperCase(dto.RazaoSocial.Trim());
        empresa.NomeFantasia = InputFormattingHelper.ToUpperCase(dto.NomeFantasia.Trim());
        empresa.Cnpj = CpfCnpjValidator.NormalizarCnpj(dto.Cnpj);
        empresa.InscricaoEstadual = InputFormattingHelper.NormalizeText(dto.InscricaoEstadual);
        empresa.InscricaoMunicipal = InputFormattingHelper.NormalizeText(dto.InscricaoMunicipal);
        empresa.Telefone = TelefoneNormalizado(dto.Telefone);
        empresa.Celular = TelefoneNormalizado(dto.Celular);
        empresa.Email = InputFormattingHelper.NormalizeEmail(dto.Email);
        empresa.EmailFinanceiro = InputFormattingHelper.NormalizeEmail(dto.EmailFinanceiro);
        empresa.Website = InputFormattingHelper.NormalizeText(dto.Website);
        empresa.Cep = CepNormalizado(dto.Cep);
        empresa.Logradouro = InputFormattingHelper.NormalizeText(dto.Logradouro);
        empresa.Numero = InputFormattingHelper.NormalizeText(dto.Numero);
        empresa.Complemento = InputFormattingHelper.NormalizeText(dto.Complemento);
        empresa.Bairro = InputFormattingHelper.NormalizeText(dto.Bairro);
        empresa.Cidade = InputFormattingHelper.NormalizeText(dto.Cidade);
        empresa.Uf = UfNormalizada(dto.Uf);
        empresa.Observacoes = InputFormattingHelper.NormalizeText(dto.Observacoes);

        // Logo: importar substitui; remover apaga. Sem ação, mantém a existente.
        var nomeLogoAtual = empresa.LogoPath;

        if (dto.RemoverLogo)
        {
            _logoStorage.Remover(nomeLogoAtual);
            empresa.LogoPath = null;
        }
        else if (!string.IsNullOrWhiteSpace(dto.NovoLogoCaminhoOrigem))
        {
            empresa.LogoPath = _logoStorage.Importar(dto.NovoLogoCaminhoOrigem, cancellationToken);

            // O arquivo anterior (se usava outra extensão) já foi limpo na importação.
        }

        empresa.Ativo = true;
        empresa.DataAtualizacao = DateTime.UtcNow;

        await _empresaRepository.UpdateAsync(empresa, cancellationToken);
        return MapearParaDto(empresa);
    }

    public async Task<string?> ObterLogoCaminhoAbsolutoAsync(CancellationToken cancellationToken = default)
    {
        var empresa = await ObterRegistroAsync(cancellationToken);
        return empresa is null ? null : _logoStorage.ObterCaminhoAbsoluto(empresa.LogoPath);
    }

    public async Task<byte[]?> ObterLogoBytesAsync(CancellationToken cancellationToken = default)
    {
        var caminho = await ObterLogoCaminhoAbsolutoAsync(cancellationToken);
        return caminho is null ? null : await File.ReadAllBytesAsync(caminho, cancellationToken);
    }

    /// <summary>Emitente mais antigo (o único esperado). Sem dados fictícios.</summary>
    private Task<Empresa?> ObterRegistroAsync(CancellationToken cancellationToken)
        => _empresaRepository.GetEmitentePrincipalAsync(cancellationToken);

    private static void Validar(SalvarEmpresaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.RazaoSocial))
            throw new ValidationException("A razão social é obrigatória.");

        if (!string.IsNullOrWhiteSpace(dto.Cnpj))
        {
            // Aceita CNPJ numérico tradicional e CNPJ alfanumérico oficial.
            var resultado = CpfCnpjValidator.Validar(dto.Cnpj);

            if (!resultado.IsValid || resultado.Tipo == DocumentoTipo.Cpf)
                throw new ValidationException("O CNPJ informado é inválido.");

            if (resultado.Tipo == DocumentoTipo.Desconhecido)
                throw new ValidationException("O CNPJ informado é inválido.");
        }

        if (!string.IsNullOrWhiteSpace(dto.Email) && !EhEmailValido(dto.Email))
            throw new ValidationException("O e-mail informado é inválido.");

        if (!string.IsNullOrWhiteSpace(dto.EmailFinanceiro) && !EhEmailValido(dto.EmailFinanceiro))
            throw new ValidationException("O e-mail financeiro informado é inválido.");

        if (!string.IsNullOrWhiteSpace(dto.Cep) && !CepMaskHelper.EhValido(dto.Cep))
            throw new ValidationException("O CEP deve ter 8 dígitos.");

        if (!string.IsNullOrWhiteSpace(dto.Uf) && !UfsValidas.Contains(dto.Uf.Trim()))
            throw new ValidationException("A UF informada é inválida.");

        if (!string.IsNullOrWhiteSpace(dto.Telefone) && !PhoneMaskHelper.EhValido(dto.Telefone))
            throw new ValidationException("O telefone deve ter 10 ou 11 dígitos.");

        if (!string.IsNullOrWhiteSpace(dto.Celular) && !PhoneMaskHelper.EhValido(dto.Celular))
            throw new ValidationException("O celular deve ter 10 ou 11 dígitos.");
    }

    private static bool EhEmailValido(string email)
        => System.Text.RegularExpressions.Regex.IsMatch(
            email.Trim(),
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static string? TelefoneNormalizado(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : PhoneMaskHelper.Normalizar(valor);

    private static string? CepNormalizado(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : CepMaskHelper.Normalizar(valor);

    private static string? UfNormalizada(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim().ToUpperInvariant();

    private static EmpresaDto MapearParaDto(Empresa e)
    {
        return new EmpresaDto
        {
            Id = e.Id,
            RazaoSocial = e.RazaoSocial,
            NomeFantasia = e.NomeFantasia,
            Cnpj = e.Cnpj,
            InscricaoEstadual = e.InscricaoEstadual,
            InscricaoMunicipal = e.InscricaoMunicipal,
            Telefone = e.Telefone,
            Celular = e.Celular,
            Email = e.Email,
            EmailFinanceiro = e.EmailFinanceiro,
            Website = e.Website,
            Logradouro = e.Logradouro,
            Numero = e.Numero,
            Complemento = e.Complemento,
            Bairro = e.Bairro,
            Cidade = e.Cidade,
            Uf = e.Uf,
            Cep = e.Cep,
            LogoPath = e.LogoPath,
            Observacoes = e.Observacoes,
            Ativo = e.Ativo
        };
    }
}
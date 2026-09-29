using OrcPro.Application.DTOs.Empresa;
using OrcPro.Application.Exceptions;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Entities.Empresa;

namespace OrcPro.Application.Services;

public class EmpresaService : IEmpresaService
{
    private readonly IEmpresaRepository _empresaRepository;

    public EmpresaService(IEmpresaRepository empresaRepository)
    {
        _empresaRepository = empresaRepository;
    }

    public async Task<EmpresaDto> ObterEmitentePrincipalAsync(CancellationToken cancellationToken = default)
    {
        var empresa = await _empresaRepository.GetEmitentePrincipalAsync(cancellationToken);
        if (empresa == null)
        {
            // Se ainda não existir empresa cadastrada, cria um emitente padrão inicial
            empresa = new Empresa
            {
                RazaoSocial = "ALEX T.I. TECNOLOGIA E ASSISTÊNCIA LTDA",
                NomeFantasia = "ALEX Orçamentos",
                Cnpj = "00.000.000/0001-00",
                Email = "contato@alexti.com.br",
                Telefone = "(11) 3000-0000",
                Cidade = "São Paulo",
                Uf = "SP",
                Ativo = true,
                DataCriacao = DateTime.UtcNow
            };
            empresa = await _empresaRepository.AddAsync(empresa, cancellationToken);
        }

        return MapearParaDto(empresa);
    }

    public async Task<EmpresaDto> AtualizarAsync(AtualizarEmpresaDto dto, CancellationToken cancellationToken = default)
    {
        var empresa = await _empresaRepository.GetByIdAsync(dto.Id, cancellationToken);
        if (empresa == null)
            throw new NotFoundException("Empresa", dto.Id);

        empresa.RazaoSocial = dto.RazaoSocial.Trim();
        empresa.NomeFantasia = dto.NomeFantasia.Trim();
        empresa.Cnpj = dto.Cnpj.Trim();
        empresa.InscricaoEstadual = dto.InscricaoEstadual?.Trim();
        empresa.InscricaoMunicipal = dto.InscricaoMunicipal?.Trim();
        empresa.Telefone = dto.Telefone?.Trim();
        empresa.Celular = dto.Celular?.Trim();
        empresa.Email = dto.Email?.Trim();
        empresa.Website = dto.Website?.Trim();
        empresa.Logradouro = dto.Logradouro?.Trim();
        empresa.Numero = dto.Numero?.Trim();
        empresa.Complemento = dto.Complemento?.Trim();
        empresa.Bairro = dto.Bairro?.Trim();
        empresa.Cidade = dto.Cidade?.Trim();
        empresa.Uf = dto.Uf?.Trim();
        empresa.Cep = dto.Cep?.Trim();
        empresa.LogoPath = dto.LogoPath?.Trim();
        empresa.DataAtualizacao = DateTime.UtcNow;

        await _empresaRepository.UpdateAsync(empresa, cancellationToken);
        return MapearParaDto(empresa);
    }

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
            Website = e.Website,
            Logradouro = e.Logradouro,
            Numero = e.Numero,
            Complemento = e.Complemento,
            Bairro = e.Bairro,
            Cidade = e.Cidade,
            Uf = e.Uf,
            Cep = e.Cep,
            LogoPath = e.LogoPath,
            Ativo = e.Ativo
        };
    }
}

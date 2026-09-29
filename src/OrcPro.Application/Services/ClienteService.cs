using OrcPro.Application.DTOs.Cliente;
using OrcPro.Application.DTOs.Common;
using OrcPro.Application.Exceptions;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Entities.Cliente;

namespace OrcPro.Application.Services;

public class ClienteService : IClienteService
{
    private readonly IClienteRepository _clienteRepository;

    public ClienteService(IClienteRepository clienteRepository)
    {
        _clienteRepository = clienteRepository;
    }

    public async Task<ClienteDto> ObterPorIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var cliente = await _clienteRepository.GetByIdAsync(id, cancellationToken);
        if (cliente == null)
            throw new NotFoundException("Cliente", id);

        return MapearParaDto(cliente);
    }

    public async Task<ClienteDto?> ObterPorCpfCnpjAsync(string cpfCnpj, CancellationToken cancellationToken = default)
    {
        var cliente = await _clienteRepository.GetByCpfCnpjAsync(cpfCnpj.Trim(), cancellationToken);
        return cliente == null ? null : MapearParaDto(cliente);
    }

    public async Task<PagedResult<ClienteDto>> ListarPaginadoAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _clienteRepository.GetPagedAsync(request, cancellationToken);
        var dtos = result.Items.Select(MapearParaDto).ToList();
        return new PagedResult<ClienteDto>(dtos, result.TotalCount, result.PageNumber, result.PageSize);
    }

    public async Task<IReadOnlyList<ClienteDto>> ListarTodosAtivosAsync(CancellationToken cancellationToken = default)
    {
        var clientes = await _clienteRepository.FindAsync(c => c.Ativo, cancellationToken);
        return clientes.Select(MapearParaDto).ToList();
    }

    public async Task<ClienteDto> CriarAsync(CriarClienteDto dto, CancellationToken cancellationToken = default)
    {
        ValidarCliente(dto);

        if (!string.IsNullOrWhiteSpace(dto.CpfCnpj) &&
            await _clienteRepository.ExistsCpfCnpjAsync(dto.CpfCnpj.Trim(), null, cancellationToken))
        {
            throw new BusinessException($"Já existe um cliente cadastrado com o CPF/CNPJ '{dto.CpfCnpj}'.");
        }

        var codigo = string.IsNullOrWhiteSpace(dto.Codigo)
            ? await _clienteRepository.GerarProximoCodigoAsync(cancellationToken)
            : dto.Codigo.Trim();

        var cliente = new Cliente
        {
            Codigo = codigo,
            TipoPessoa = dto.TipoPessoa,
            NomeRazaoSocial = dto.NomeRazaoSocial.Trim(),
            NomeFantasia = dto.NomeFantasia?.Trim(),
            CpfCnpj = dto.CpfCnpj.Trim(),
            RgIe = dto.RgIe?.Trim(),
            Telefone = dto.Telefone?.Trim(),
            Celular = dto.Celular.Trim(),
            Email = dto.Email.Trim(),
            EmailFinanceiro = dto.EmailFinanceiro?.Trim(),
            Cep = dto.Cep?.Trim(),
            Logradouro = dto.Logradouro?.Trim(),
            Numero = dto.Numero?.Trim(),
            Complemento = dto.Complemento?.Trim(),
            Bairro = dto.Bairro?.Trim(),
            Cidade = dto.Cidade?.Trim(),
            Uf = dto.Uf?.Trim(),
            Observacoes = dto.Observacoes?.Trim(),
            Ativo = dto.Ativo,
            DataCriacao = DateTime.UtcNow
        };

        var criado = await _clienteRepository.AddAsync(cliente, cancellationToken);
        return MapearParaDto(criado);
    }

    public async Task<ClienteDto> AtualizarAsync(AtualizarClienteDto dto, CancellationToken cancellationToken = default)
    {
        ValidarCliente(dto);

        var cliente = await _clienteRepository.GetByIdAsync(dto.Id, cancellationToken);
        if (cliente == null)
            throw new NotFoundException("Cliente", dto.Id);

        if (!string.IsNullOrWhiteSpace(dto.CpfCnpj) &&
            await _clienteRepository.ExistsCpfCnpjAsync(dto.CpfCnpj.Trim(), dto.Id, cancellationToken))
        {
            throw new BusinessException($"Já existe outro cliente cadastrado com o CPF/CNPJ '{dto.CpfCnpj}'.");
        }

        cliente.TipoPessoa = dto.TipoPessoa;
        cliente.NomeRazaoSocial = dto.NomeRazaoSocial.Trim();
        cliente.NomeFantasia = dto.NomeFantasia?.Trim();
        cliente.CpfCnpj = dto.CpfCnpj.Trim();
        cliente.RgIe = dto.RgIe?.Trim();
        cliente.Telefone = dto.Telefone?.Trim();
        cliente.Celular = dto.Celular.Trim();
        cliente.Email = dto.Email.Trim();
        cliente.EmailFinanceiro = dto.EmailFinanceiro?.Trim();
        cliente.Cep = dto.Cep?.Trim();
        cliente.Logradouro = dto.Logradouro?.Trim();
        cliente.Numero = dto.Numero?.Trim();
        cliente.Complemento = dto.Complemento?.Trim();
        cliente.Bairro = dto.Bairro?.Trim();
        cliente.Cidade = dto.Cidade?.Trim();
        cliente.Uf = dto.Uf?.Trim();
        cliente.Observacoes = dto.Observacoes?.Trim();
        cliente.Ativo = dto.Ativo;
        cliente.DataAtualizacao = DateTime.UtcNow;

        await _clienteRepository.UpdateAsync(cliente, cancellationToken);
        return MapearParaDto(cliente);
    }

    public async Task InativarAsync(int id, CancellationToken cancellationToken = default)
    {
        var cliente = await _clienteRepository.GetByIdAsync(id, cancellationToken);
        if (cliente == null)
            throw new NotFoundException("Cliente", id);

        cliente.Ativo = false;
        cliente.DataAtualizacao = DateTime.UtcNow;
        await _clienteRepository.UpdateAsync(cliente, cancellationToken);
    }

    public async Task ExcluirAsync(int id, CancellationToken cancellationToken = default)
    {
        var existe = await _clienteRepository.ExistsAsync(id, cancellationToken);
        if (!existe)
            throw new NotFoundException("Cliente", id);

        await _clienteRepository.DeleteAsync(id, cancellationToken);
    }

    private static void ValidarCliente(CriarClienteDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NomeRazaoSocial))
            throw new ValidationException("O Nome / Razão Social do cliente é obrigatório.");

        if (string.IsNullOrWhiteSpace(dto.CpfCnpj))
            throw new ValidationException("O CPF / CNPJ do cliente é obrigatório.");

        if (string.IsNullOrWhiteSpace(dto.Celular))
            throw new ValidationException("O Celular / WhatsApp é obrigatório.");

        if (string.IsNullOrWhiteSpace(dto.Email))
            throw new ValidationException("O E-mail principal é obrigatório.");
    }

    private static ClienteDto MapearParaDto(Cliente c)
    {
        return new ClienteDto
        {
            Id = c.Id,
            Codigo = c.Codigo,
            TipoPessoa = c.TipoPessoa,
            NomeRazaoSocial = c.NomeRazaoSocial,
            NomeFantasia = c.NomeFantasia,
            CpfCnpj = c.CpfCnpj,
            RgIe = c.RgIe,
            Telefone = c.Telefone,
            Celular = c.Celular,
            Email = c.Email,
            EmailFinanceiro = c.EmailFinanceiro,
            Cep = c.Cep,
            Logradouro = c.Logradouro,
            Numero = c.Numero,
            Complemento = c.Complemento,
            Bairro = c.Bairro,
            Cidade = c.Cidade,
            Uf = c.Uf,
            Observacoes = c.Observacoes,
            Ativo = c.Ativo,
            DataCriacao = c.DataCriacao
        };
    }
}

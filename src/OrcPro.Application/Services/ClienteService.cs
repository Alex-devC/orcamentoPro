using OrcPro.Application.DTOs.Cliente;
using OrcPro.Application.DTOs.Common;
using OrcPro.Application.Exceptions;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Common;
using OrcPro.Domain.Entities.Cliente;

namespace OrcPro.Application.Services;

public class ClienteService : IClienteService
{
    private readonly IClienteRepository _clienteRepository;
    private readonly IOrcamentoRepository _orcamentoRepository;

    public ClienteService(IClienteRepository clienteRepository, IOrcamentoRepository orcamentoRepository)
    {
        _clienteRepository = clienteRepository;
        _orcamentoRepository = orcamentoRepository;
    }

    public async Task<ClienteDto> ObterPorIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var cliente = await _clienteRepository.GetByIdAsync(id, cancellationToken);
        if (cliente == null)
            throw new NotFoundException("Cliente", id);

        var orcamentos = await _orcamentoRepository.CountByClienteIdAsync(id, cancellationToken);
        return MapearParaDto(cliente, orcamentos);
    }

    public async Task<ClienteDto?> ObterPorCpfCnpjAsync(string cpfCnpj, CancellationToken cancellationToken = default)
    {
        var cliente = await _clienteRepository.GetByCpfCnpjAsync(CpfCnpjValidator.Normalizar(cpfCnpj), cancellationToken);
        return cliente == null ? null : MapearParaDto(cliente, 0);
    }

    public async Task<PagedResult<ClienteDto>> ListarPaginadoAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _clienteRepository.GetPagedAsync(request, cancellationToken);

        var dtos = new List<ClienteDto>(result.Items.Count);
        foreach (var cliente in result.Items)
        {
            var orcamentos = await _orcamentoRepository.CountByClienteIdAsync(cliente.Id, cancellationToken);
            dtos.Add(MapearParaDto(cliente, orcamentos));
        }

        return new PagedResult<ClienteDto>(dtos, result.TotalCount, result.PageNumber, result.PageSize);
    }

    public async Task<IReadOnlyList<ClienteDto>> ListarTodosAtivosAsync(CancellationToken cancellationToken = default)
    {
        var clientes = await _clienteRepository.FindAsync(c => c.Ativo, cancellationToken);
        return clientes.Select(c => MapearParaDto(c, 0)).ToList();
    }

    public async Task<ClienteDto> CriarAsync(CriarClienteDto dto, CancellationToken cancellationToken = default)
    {
        ValidarCliente(dto);

        var cpfCnpj = CpfCnpjValidator.Normalizar(dto.CpfCnpj);

        if (!string.IsNullOrEmpty(cpfCnpj) &&
            await _clienteRepository.ExistsCpfCnpjAsync(cpfCnpj, null, cancellationToken))
        {
            throw new BusinessException(
                $"Já existe um cliente cadastrado com o CPF/CNPJ '{CpfCnpjValidator.Formatar(cpfCnpj)}'.");
        }

        var codigo = await ResolverCodigoAsync(dto.Codigo, null, cancellationToken);

        var cliente = new Cliente
        {
            Codigo = codigo,
            TipoPessoa = dto.TipoPessoa,
            NomeRazaoSocial = dto.NomeRazaoSocial.Trim(),
            NomeFantasia = NormalizarOpcional(dto.NomeFantasia),
            // Vazio (e não null) quando não informado: preserva compatibilidade com bases
            // existentes, cuja coluna CpfCnpj foi criada como NOT NULL.
            CpfCnpj = cpfCnpj,
            RgIe = NormalizarOpcional(dto.RgIe),
            Telefone = NormalizarOpcional(dto.Telefone),
            Celular = dto.Celular.Trim(),
            Email = dto.Email.Trim(),
            EmailFinanceiro = NormalizarOpcional(dto.EmailFinanceiro),
            Cep = NormalizarOpcional(dto.Cep),
            Logradouro = NormalizarOpcional(dto.Logradouro),
            Numero = NormalizarOpcional(dto.Numero),
            Complemento = NormalizarOpcional(dto.Complemento),
            Bairro = NormalizarOpcional(dto.Bairro),
            Cidade = NormalizarOpcional(dto.Cidade),
            Uf = NormalizarOpcional(dto.Uf)?.ToUpperInvariant(),
            Observacoes = NormalizarOpcional(dto.Observacoes),
            Ativo = dto.Ativo,
            DataCriacao = DateTime.UtcNow
        };

        var criado = await _clienteRepository.AddAsync(cliente, cancellationToken);
        return await ObterPorIdAsync(criado.Id, cancellationToken);
    }

    public async Task<ClienteDto> AtualizarAsync(AtualizarClienteDto dto, CancellationToken cancellationToken = default)
    {
        ValidarCliente(dto);

        var cliente = await _clienteRepository.GetByIdAsync(dto.Id, cancellationToken);
        if (cliente == null)
            throw new NotFoundException("Cliente", dto.Id);

        var cpfCnpj = CpfCnpjValidator.Normalizar(dto.CpfCnpj);

        if (!string.IsNullOrEmpty(cpfCnpj) &&
            await _clienteRepository.ExistsCpfCnpjAsync(cpfCnpj, dto.Id, cancellationToken))
        {
            throw new BusinessException(
                $"Já existe outro cliente cadastrado com o CPF/CNPJ '{CpfCnpjValidator.Formatar(cpfCnpj)}'.");
        }

        cliente.TipoPessoa = dto.TipoPessoa;
        cliente.NomeRazaoSocial = dto.NomeRazaoSocial.Trim();
        cliente.NomeFantasia = NormalizarOpcional(dto.NomeFantasia);
        cliente.CpfCnpj = cpfCnpj;
        cliente.RgIe = NormalizarOpcional(dto.RgIe);
        cliente.Telefone = NormalizarOpcional(dto.Telefone);
        cliente.Celular = dto.Celular.Trim();
        cliente.Email = dto.Email.Trim();
        cliente.EmailFinanceiro = NormalizarOpcional(dto.EmailFinanceiro);
        cliente.Cep = NormalizarOpcional(dto.Cep);
        cliente.Logradouro = NormalizarOpcional(dto.Logradouro);
        cliente.Numero = NormalizarOpcional(dto.Numero);
        cliente.Complemento = NormalizarOpcional(dto.Complemento);
        cliente.Bairro = NormalizarOpcional(dto.Bairro);
        cliente.Cidade = NormalizarOpcional(dto.Cidade);
        cliente.Uf = NormalizarOpcional(dto.Uf)?.ToUpperInvariant();
        cliente.Observacoes = NormalizarOpcional(dto.Observacoes);
        cliente.Ativo = dto.Ativo;
        cliente.DataAtualizacao = DateTime.UtcNow;

        await _clienteRepository.UpdateAsync(cliente, cancellationToken);
        return await ObterPorIdAsync(cliente.Id, cancellationToken);
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

    /// <summary>
    /// Exclui o cliente. Clientes com orçamentos vinculados não podem ser excluídos:
    /// nesse caso a orientação é inativar (mantém o histórico dos orçamentos).
    /// </summary>
    public async Task ExcluirAsync(int id, CancellationToken cancellationToken = default)
    {
        var cliente = await _clienteRepository.GetByIdAsync(id, cancellationToken);
        if (cliente == null)
            throw new NotFoundException("Cliente", id);

        var orcamentos = await _orcamentoRepository.CountByClienteIdAsync(id, cancellationToken);
        if (orcamentos > 0)
            throw new BusinessException(
                $"O cliente \"{cliente.NomeRazaoSocial}\" possui {orcamentos} orçamento(s) vinculado(s) e não pode ser excluído. Inative-o em vez de excluir.");

        await _clienteRepository.DeleteAsync(id, cancellationToken);
    }

    /// <summary>Usa o código informado (validando unicidade) ou gera o próximo sequencial.</summary>
    private async Task<string> ResolverCodigoAsync(string? codigoInformado, int? ignorarId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(codigoInformado))
            return await _clienteRepository.GerarProximoCodigoAsync(cancellationToken);

        var codigo = codigoInformado.Trim();
        var existente = await _clienteRepository.GetByCodigoAsync(codigo, cancellationToken);

        if (existente != null && (!ignorarId.HasValue || existente.Id != ignorarId.Value))
            throw new BusinessException($"Já existe um cliente cadastrado com o código '{codigo}'.");

        return codigo;
    }

    private static string? NormalizarOpcional(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static void ValidarCliente(CriarClienteDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NomeRazaoSocial))
            throw new ValidationException("O Nome / Razão Social do cliente é obrigatório.");

        if (string.IsNullOrWhiteSpace(dto.Celular))
            throw new ValidationException("O Celular / WhatsApp é obrigatório.");

        if (string.IsNullOrWhiteSpace(dto.Email))
            throw new ValidationException("O E-mail principal é obrigatório.");

        // CPF/CNPJ é opcional, mas quando informado precisa ser válido.
        var cpfCnpj = CpfCnpjValidator.Normalizar(dto.CpfCnpj);
        if (!string.IsNullOrEmpty(cpfCnpj) && !CpfCnpjValidator.EhValido(cpfCnpj))
            throw new ValidationException("O CPF/CNPJ informado é inválido. Confira os dígitos.");

        if (!string.IsNullOrWhiteSpace(dto.Uf) && dto.Uf.Trim().Length != 2)
            throw new ValidationException("A UF deve ter 2 letras.");

        if (dto.TipoPessoa is not ("PJ" or "PF"))
            throw new ValidationException("O tipo de pessoa deve ser PF ou PJ.");
    }

    private static ClienteDto MapearParaDto(Cliente c, int quantidadeOrcamentos)
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
            DataCriacao = c.DataCriacao,
            QuantidadeOrcamentos = quantidadeOrcamentos
        };
    }
}

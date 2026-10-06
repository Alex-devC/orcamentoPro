using OrcPro.Application.DTOs.Common;
using OrcPro.Application.DTOs.Orcamento;
using OrcPro.Application.Exceptions;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Entities.Orcamento;

namespace OrcPro.Application.Services;

public class OrcamentoService : IOrcamentoService
{
    private readonly IOrcamentoRepository _orcamentoRepository;
    private readonly IOrcamentoStatusRepository _statusRepository;
    private readonly IOrcamentoHistoricoRepository _historicoRepository;
    private readonly IClienteRepository _clienteRepository;
    private readonly ITecnicoRepository _tecnicoRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IEmpresaService _empresaService;
    private readonly IServicoService _servicoService;
    private readonly IPecaService _pecaService;

    public OrcamentoService(
        IOrcamentoRepository orcamentoRepository,
        IOrcamentoStatusRepository statusRepository,
        IOrcamentoHistoricoRepository historicoRepository,
        IClienteRepository clienteRepository,
        ITecnicoRepository tecnicoRepository,
        IUsuarioRepository usuarioRepository,
        IEmpresaService empresaService,
        IServicoService servicoService,
        IPecaService pecaService)
    {
        _orcamentoRepository = orcamentoRepository;
        _statusRepository = statusRepository;
        _historicoRepository = historicoRepository;
        _clienteRepository = clienteRepository;
        _tecnicoRepository = tecnicoRepository;
        _usuarioRepository = usuarioRepository;
        _empresaService = empresaService;
        _servicoService = servicoService;
        _pecaService = pecaService;
    }

    public async Task<OrcamentoDto> ObterPorIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var orcamento = await _orcamentoRepository.GetWithDetailsByIdAsync(id, cancellationToken);
        if (orcamento == null)
            throw new NotFoundException("Orçamento", id);

        return MapearParaDto(orcamento);
    }

    public async Task<OrcamentoDto> ObterPorNumeroAsync(string numero, CancellationToken cancellationToken = default)
    {
        var orcamento = await _orcamentoRepository.GetByNumeroAsync(numero.Trim(), cancellationToken);
        if (orcamento == null)
            throw new NotFoundException($"Orçamento com número '{numero}' não foi encontrado.");

        return MapearParaDto(orcamento);
    }

    public async Task<PagedResult<OrcamentoResumoDto>> ListarPaginadoAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _orcamentoRepository.GetPagedAsync(request, cancellationToken);
        var dtos = result.Items.Select(MapearParaResumoDto).ToList();
        return new PagedResult<OrcamentoResumoDto>(dtos, result.TotalCount, result.PageNumber, result.PageSize);
    }

    public async Task<IReadOnlyList<OrcamentoStatusDto>> ListarStatusDisponiveisAsync(CancellationToken cancellationToken = default)
    {
        var statuses = await _statusRepository.GetAllAtivosAsync(cancellationToken);
        return statuses.OrderBy(s => s.Ordem).Select(s => new OrcamentoStatusDto
        {
            Id = s.Id,
            Codigo = s.Codigo,
            Nome = s.Nome,
            CorHex = s.CorHex,
            Ordem = s.Ordem
        }).ToList();
    }

    public async Task<OrcamentoDto> CriarAsync(CriarOrcamentoDto dto, CancellationToken cancellationToken = default)
    {
        ValidarCabecalho(dto);

        var cliente = await _clienteRepository.GetByIdAsync(dto.ClienteId, cancellationToken);
        if (cliente == null)
            throw new NotFoundException("Cliente", dto.ClienteId);

        // O emitente vem do cadastro Minha Empresa: o usuário nunca o digita no orçamento.
        var emitente = await _empresaService.ObterAsync(cancellationToken)
            ?? throw new BusinessException(
                "Minha Empresa não está configurada. Acesse Configurações → Minha Empresa antes de criar um orçamento.");

        var statusRascunho = await _statusRepository.GetByCodigoAsync(OrcamentoStatus.CodigoRascunho, cancellationToken)
            ?? (await _statusRepository.GetAllAtivosAsync(cancellationToken)).FirstOrDefault();

        if (statusRascunho == null)
            throw new BusinessException("Nenhum status padrão de orçamento configurado no sistema.");

        await ValidarItensAsync(dto.ItensIniciais, dto.MaosDeObraIniciais, dto.TecnicosIniciais, cancellationToken);

        var anoAtual = DateTime.UtcNow.Year;
        var sequencial = await _orcamentoRepository.ObterProximoSequencialAsync(anoAtual, cancellationToken);
        var numeroFormatado = $"{sequencial:D4}/{anoAtual}";

        var diasValidade = dto.DiasValidade > 0 ? dto.DiasValidade : 15;
        var orcamento = new Orcamento
        {
            Numero = numeroFormatado,
            Ano = anoAtual,
            Sequencial = sequencial,
            DataEmissao = DateTime.UtcNow,
            DiasValidade = diasValidade,
            DataValidade = DateTime.UtcNow.AddDays(diasValidade),
            ClienteId = dto.ClienteId,
            EmpresaId = emitente.Id,
            UsuarioId = dto.UsuarioId,
            StatusId = statusRascunho.Id,
            Status = statusRascunho,
            ValorAcrescimo = dto.ValorAcrescimo,
            ValorDesconto = dto.ValorDesconto,
            CondicoesPagamento = dto.CondicoesPagamento?.Trim(),
            PrazoEntrega = dto.PrazoEntrega?.Trim(),
            Garantia = dto.Garantia?.Trim(),
            Observacoes = dto.Observacoes?.Trim(),
            ObservacoesInternas = dto.ObservacoesInternas?.Trim(),
            DataCriacao = DateTime.UtcNow
        };

        // Adicionar itens iniciais
        var numItem = 1;
        foreach (var itemDto in dto.ItensIniciais)
        {
            var item = new OrcamentoItem
            {
                NumeroItem = numItem++,
                PecaId = itemDto.PecaId,
                CodigoPeca = itemDto.CodigoPeca.Trim(),
                Descricao = itemDto.Descricao.Trim(),
                UnidadeMedida = string.IsNullOrWhiteSpace(itemDto.UnidadeMedida) ? "UN" : itemDto.UnidadeMedida.Trim(),
                Quantidade = itemDto.Quantidade,
                PrecoUnitario = itemDto.PrecoUnitario,
                ValorDesconto = itemDto.ValorDesconto
            };
            item.CalcularTotal();
            orcamento.Itens.Add(item);
        }

        // Adicionar mão de obra inicial (serviços)
        var numMo = 1;
        foreach (var moDto in dto.MaosDeObraIniciais)
        {
            var mo = new OrcamentoMaoDeObra
            {
                NumeroItem = numMo++,
                ServicoId = moDto.ServicoId,
                Descricao = moDto.Descricao.Trim(),
                QuantidadeHoras = moDto.QuantidadeHoras,
                ValorUnitario = moDto.ValorUnitario,
                ValorDesconto = moDto.ValorDesconto,
                Observacoes = moDto.Observacoes?.Trim()
            };
            mo.CalcularTotal();

            if (moDto.TecnicoIds != null)
            {
                foreach (var tecId in moDto.TecnicoIds)
                {
                    mo.Tecnicos.Add(new OrcamentoMaoDeObraTecnico
                    {
                        TecnicoId = tecId
                    });
                }
            }
            orcamento.MaosDeObra.Add(mo);
        }

        // Associar técnicos iniciais do orçamento
        foreach (var tecDto in dto.TecnicosIniciais)
        {
            orcamento.Tecnicos.Add(new OrcamentoTecnico
            {
                TecnicoId = tecDto.TecnicoId,
                Funcao = tecDto.Funcao?.Trim(),
                Observacoes = tecDto.Observacoes?.Trim()
            });
        }

        // Recalcular totais gerais
        orcamento.RecalcularTotais();

        // Registrar no histórico
        var usuario = await _usuarioRepository.GetByIdAsync(dto.UsuarioId, cancellationToken);
        orcamento.Historicos.Add(new OrcamentoHistorico
        {
            DataRegistro = DateTime.UtcNow,
            UsuarioId = dto.UsuarioId,
            NomeUsuario = usuario?.NomeCompleto ?? "Sistema",
            Acao = "Criacao",
            StatusNovo = statusRascunho.Nome,
            Descricao = $"Orçamento {orcamento.Numero} criado com status {statusRascunho.Nome}."
        });

        // Cabeçalho + itens + mão de obra + técnicos + histórico são gravados na mesma
        // transação: um erro em qualquer etapa não deixa o orçamento pela metade.
        int idCriado = 0;

        await _orcamentoRepository.ExecutarEmTransacaoAsync(async () =>
        {
            var criado = await _orcamentoRepository.AddAsync(orcamento, cancellationToken);
            idCriado = criado.Id;
        }, cancellationToken);

        return await ObterPorIdAsync(idCriado, cancellationToken);
    }

    public async Task<OrcamentoDto> EditarAsync(AtualizarOrcamentoDto dto, int usuarioId, CancellationToken cancellationToken = default)
    {
        var orcamento = await _orcamentoRepository.GetWithDetailsByIdAsync(dto.Id, cancellationToken);
        if (orcamento == null)
            throw new NotFoundException("Orçamento", dto.Id);


        GarantirEditavel(orcamento);

        if (orcamento.ClienteId != dto.ClienteId)
        {
            var clienteExiste = await _clienteRepository.ExistsAsync(dto.ClienteId, cancellationToken);
            if (!clienteExiste)
                throw new NotFoundException("Cliente", dto.ClienteId);
            orcamento.ClienteId = dto.ClienteId;
        }

        orcamento.DiasValidade = dto.DiasValidade > 0 ? dto.DiasValidade : orcamento.DiasValidade;
        orcamento.DataValidade = orcamento.DataEmissao.AddDays(orcamento.DiasValidade);
        orcamento.ValorAcrescimo = dto.ValorAcrescimo;
        orcamento.ValorDesconto = dto.ValorDesconto;
        orcamento.CondicoesPagamento = dto.CondicoesPagamento?.Trim();
        orcamento.PrazoEntrega = dto.PrazoEntrega?.Trim();
        orcamento.Garantia = dto.Garantia?.Trim();
        orcamento.Observacoes = dto.Observacoes?.Trim();
        orcamento.ObservacoesInternas = dto.ObservacoesInternas?.Trim();
        orcamento.DataAtualizacao = DateTime.UtcNow;

        orcamento.RecalcularTotais();

        await RegistrarHistoricoAsync(orcamento.Id, usuarioId, "Edicao", "Dados comerciais do orçamento atualizados.", cancellationToken);

        await _orcamentoRepository.UpdateAsync(orcamento, cancellationToken);
        return await ObterPorIdAsync(orcamento.Id, cancellationToken);
    }

    public async Task<OrcamentoDto> AlterarStatusAsync(AlterarStatusOrcamentoDto dto, CancellationToken cancellationToken = default)
    {
        var orcamento = await _orcamentoRepository.GetWithDetailsByIdAsync(dto.OrcamentoId, cancellationToken);
        if (orcamento == null)
            throw new NotFoundException("Orçamento", dto.OrcamentoId);

        var novoStatus = await _statusRepository.GetByIdAsync(dto.NovoStatusId, cancellationToken);
        if (novoStatus == null)
            throw new NotFoundException("Status de Orçamento", dto.NovoStatusId);

        var statusAnteriorNome = orcamento.Status?.Nome ?? "Indefinido";
        var statusAnteriorCodigo = orcamento.Status?.Codigo;

        // Transições controladas: não é possível, por exemplo, voltar um orçamento
        // aprovado para rascunho nem reativar um orçamento cancelado.
        if (!OrcamentoStatus.PodeTransicionarPara(statusAnteriorCodigo, novoStatus.Codigo))
            throw new BusinessException(
                OrcamentoStatus.MensagemTransicaoInvalida(statusAnteriorNome, novoStatus.Nome));

        orcamento.StatusId = novoStatus.Id;
        orcamento.Status = novoStatus;
        orcamento.DataAtualizacao = DateTime.UtcNow;

        var desc = $"Status alterado de '{statusAnteriorNome}' para '{novoStatus.Nome}'.";
        if (!string.IsNullOrWhiteSpace(dto.ObservacaoMotivo))
            desc += $" Motivo: {dto.ObservacaoMotivo.Trim()}";

        var usuario = await _usuarioRepository.GetByIdAsync(dto.UsuarioId, cancellationToken);
        var historico = new OrcamentoHistorico
        {
            OrcamentoId = orcamento.Id,
            DataRegistro = DateTime.UtcNow,
            UsuarioId = dto.UsuarioId,
            NomeUsuario = usuario?.NomeCompleto ?? "Sistema",
            Acao = "AlteracaoStatus",
            StatusAnterior = statusAnteriorNome,
            StatusNovo = novoStatus.Nome,
            Descricao = desc,
            Detalhes = dto.ObservacaoMotivo
        };
        await _historicoRepository.AddAsync(historico, cancellationToken);

        await _orcamentoRepository.UpdateAsync(orcamento, cancellationToken);
        return await ObterPorIdAsync(orcamento.Id, cancellationToken);
    }

    /// <summary>
    /// Exclui o orçamento. Orçamento finalizado ou cancelado é terminal e não pode ser
    /// excluído — a regra é explícita e o motivo é informado ao usuário.
    /// </summary>
    public async Task ExcluirAsync(ExcluirOrcamentoDto dto, CancellationToken cancellationToken = default)
    {
        var orcamento = await _orcamentoRepository.GetWithDetailsByIdAsync(dto.OrcamentoId, cancellationToken);
        if (orcamento == null)
            throw new NotFoundException("Orçamento", dto.OrcamentoId);

        if (OrcamentoStatus.EhTerminal(orcamento.Status?.Codigo))
            throw new BusinessException(
                $"O orçamento '{orcamento.Numero}' está {orcamento.Status!.Nome} e não pode ser excluído.");

        await _orcamentoRepository.ExecutarEmTransacaoAsync(async () =>
        {
            await _orcamentoRepository.DeleteAsync(orcamento.Id, cancellationToken);
        }, cancellationToken);
    }

    public async Task<OrcamentoDto> ClonarAsync(int orcamentoId, int usuarioId, CancellationToken cancellationToken = default)
    {
        var origem = await _orcamentoRepository.GetWithDetailsByIdAsync(orcamentoId, cancellationToken);
        if (origem == null)
            throw new NotFoundException("Orçamento de Origem", orcamentoId);

        var statusRascunho = await _statusRepository.GetByCodigoAsync(OrcamentoStatus.CodigoRascunho, cancellationToken)
            ?? (await _statusRepository.GetAllAtivosAsync(cancellationToken)).FirstOrDefault();

        if (statusRascunho == null)
            throw new BusinessException("Status padrão de orçamento não encontrado.");

        var anoAtual = DateTime.UtcNow.Year;
        var sequencial = await _orcamentoRepository.ObterProximoSequencialAsync(anoAtual, cancellationToken);
        var numeroFormatado = $"{sequencial:D4}/{anoAtual}";

        var clone = new Orcamento
        {
            Numero = numeroFormatado,
            Ano = anoAtual,
            Sequencial = sequencial,
            DataEmissao = DateTime.UtcNow,
            DiasValidade = origem.DiasValidade,
            DataValidade = DateTime.UtcNow.AddDays(origem.DiasValidade),
            ClienteId = origem.ClienteId,
            EmpresaId = origem.EmpresaId,
            UsuarioId = usuarioId,
            StatusId = statusRascunho.Id,
            Status = statusRascunho,
            ValorAcrescimo = origem.ValorAcrescimo,
            ValorDesconto = origem.ValorDesconto,
            CondicoesPagamento = origem.CondicoesPagamento,
            PrazoEntrega = origem.PrazoEntrega,
            Garantia = origem.Garantia,
            Observacoes = origem.Observacoes,
            ObservacoesInternas = $"Clonado a partir do orçamento {origem.Numero}.",
            DataCriacao = DateTime.UtcNow
        };

        // Clonar itens de peças
        foreach (var item in origem.Itens)
        {
            var novoItem = new OrcamentoItem
            {
                NumeroItem = item.NumeroItem,
                PecaId = item.PecaId,
                CodigoPeca = item.CodigoPeca,
                Descricao = item.Descricao,
                UnidadeMedida = item.UnidadeMedida,
                Quantidade = item.Quantidade,
                PrecoUnitario = item.PrecoUnitario,
                ValorDesconto = item.ValorDesconto
            };
            novoItem.CalcularTotal();
            clone.Itens.Add(novoItem);
        }

        // Clonar linhas de mão de obra
        foreach (var mo in origem.MaosDeObra)
        {
            var novaMo = new OrcamentoMaoDeObra
            {
                NumeroItem = mo.NumeroItem,
                Descricao = mo.Descricao,
                QuantidadeHoras = mo.QuantidadeHoras,
                ValorUnitario = mo.ValorUnitario,
                ValorDesconto = mo.ValorDesconto,
                Observacoes = mo.Observacoes
            };
            novaMo.CalcularTotal();

            foreach (var tec in mo.Tecnicos)
            {
                novaMo.Tecnicos.Add(new OrcamentoMaoDeObraTecnico
                {
                    TecnicoId = tec.TecnicoId,
                    Funcao = tec.Funcao,
                    Observacoes = tec.Observacoes
                });
            }

            clone.MaosDeObra.Add(novaMo);
        }

        // Clonar técnicos associados
        foreach (var tec in origem.Tecnicos)
        {
            clone.Tecnicos.Add(new OrcamentoTecnico
            {
                TecnicoId = tec.TecnicoId,
                Funcao = tec.Funcao,
                Observacoes = tec.Observacoes
            });
        }

        clone.RecalcularTotais();

        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId, cancellationToken);
        clone.Historicos.Add(new OrcamentoHistorico
        {
            DataRegistro = DateTime.UtcNow,
            UsuarioId = usuarioId,
            NomeUsuario = usuario?.NomeCompleto ?? "Sistema",
            Acao = "Clonagem",
            StatusNovo = statusRascunho.Nome,
            Descricao = $"Orçamento {clone.Numero} criado por clonagem do orçamento {origem.Numero}."
        });

        var criado = await _orcamentoRepository.AddAsync(clone, cancellationToken);
        return await ObterPorIdAsync(criado.Id, cancellationToken);
    }

    public async Task<OrcamentoDto> AdicionarItemAsync(int orcamentoId, AdicionarItemDto dto, int usuarioId, CancellationToken cancellationToken = default)
    {
        ValidarItem(dto);

        var orcamento = await _orcamentoRepository.GetWithDetailsByIdAsync(orcamentoId, cancellationToken);
        if (orcamento == null)
            throw new NotFoundException("Orçamento", orcamentoId);

        GarantirEditavel(orcamento);

        // Peça informada precisa existir no cadastro; não há digitação livre.
        if (dto.PecaId.HasValue)
            await _pecaService.ObterPorIdAsync(dto.PecaId.Value, cancellationToken);

        var proximoNumero = orcamento.Itens.Count > 0 ? orcamento.Itens.Max(i => i.NumeroItem) + 1 : 1;

        var item = new OrcamentoItem
        {
            OrcamentoId = orcamentoId,
            NumeroItem = proximoNumero,
            PecaId = dto.PecaId,
            CodigoPeca = dto.CodigoPeca.Trim(),
            Descricao = dto.Descricao.Trim(),
            UnidadeMedida = string.IsNullOrWhiteSpace(dto.UnidadeMedida) ? "UN" : dto.UnidadeMedida.Trim(),
            Quantidade = dto.Quantidade,
            PrecoUnitario = dto.PrecoUnitario,
            ValorDesconto = dto.ValorDesconto
        };
        item.CalcularTotal();

        orcamento.Itens.Add(item);
        orcamento.RecalcularTotais();
        orcamento.DataAtualizacao = DateTime.UtcNow;

        await RegistrarHistoricoAsync(orcamento.Id, usuarioId, "AdicionarItem", $"Item '{item.CodigoPeca} - {item.Descricao}' adicionado.", cancellationToken);

        await _orcamentoRepository.UpdateAsync(orcamento, cancellationToken);
        return await ObterPorIdAsync(orcamentoId, cancellationToken);
    }

    public async Task<OrcamentoDto> RemoverItemAsync(int orcamentoId, int itemId, int usuarioId, CancellationToken cancellationToken = default)
    {
        var orcamento = await _orcamentoRepository.GetWithDetailsByIdAsync(orcamentoId, cancellationToken);
        if (orcamento == null)
            throw new NotFoundException("Orçamento", orcamentoId);

        var item = orcamento.Itens.FirstOrDefault(i => i.Id == itemId);
        if (item == null)
            throw new NotFoundException("Item do Orçamento", itemId);


        GarantirEditavel(orcamento);

        orcamento.Itens.Remove(item);
        orcamento.RecalcularTotais();
        orcamento.DataAtualizacao = DateTime.UtcNow;

        await RegistrarHistoricoAsync(orcamento.Id, usuarioId, "RemoverItem", $"Item '{item.CodigoPeca} - {item.Descricao}' removido.", cancellationToken);

        await _orcamentoRepository.UpdateAsync(orcamento, cancellationToken);
        return await ObterPorIdAsync(orcamentoId, cancellationToken);
    }

    public async Task<OrcamentoDto> AdicionarMaoDeObraAsync(int orcamentoId, AdicionarMaoDeObraDto dto, int usuarioId, CancellationToken cancellationToken = default)
    {
        ValidarMaoDeObra(dto);

        var orcamento = await _orcamentoRepository.GetWithDetailsByIdAsync(orcamentoId, cancellationToken);
        if (orcamento == null)
            throw new NotFoundException("Orçamento", orcamentoId);

        GarantirEditavel(orcamento);

        // Serviço informado precisa existir no cadastro de Serviços.
        if (dto.ServicoId.HasValue)
            await _servicoService.ObterPorIdAsync(dto.ServicoId.Value, cancellationToken);

        if (dto.TecnicoIds is not null)
        {
            foreach (var tecId in dto.TecnicoIds)
            {
                if (!await _tecnicoRepository.ExistsAsync(tecId, cancellationToken))
                    throw new NotFoundException("Técnico", tecId);
            }
        }

        var proximoNumero = orcamento.MaosDeObra.Count > 0 ? orcamento.MaosDeObra.Max(m => m.NumeroItem) + 1 : 1;

        var mo = new OrcamentoMaoDeObra
        {
            OrcamentoId = orcamentoId,
            NumeroItem = proximoNumero,
            ServicoId = dto.ServicoId,
            Descricao = dto.Descricao.Trim(),
            QuantidadeHoras = dto.QuantidadeHoras,
            ValorUnitario = dto.ValorUnitario,
            ValorDesconto = dto.ValorDesconto,
            Observacoes = dto.Observacoes?.Trim()
        };
        mo.CalcularTotal();

        if (dto.TecnicoIds != null)
        {
            foreach (var tecId in dto.TecnicoIds)
            {
                mo.Tecnicos.Add(new OrcamentoMaoDeObraTecnico
                {
                    TecnicoId = tecId
                });
            }
        }

        orcamento.MaosDeObra.Add(mo);
        orcamento.RecalcularTotais();
        orcamento.DataAtualizacao = DateTime.UtcNow;

        await RegistrarHistoricoAsync(orcamento.Id, usuarioId, "AdicionarMaoDeObra", $"Serviço '{mo.Descricao}' adicionado.", cancellationToken);

        await _orcamentoRepository.UpdateAsync(orcamento, cancellationToken);
        return await ObterPorIdAsync(orcamentoId, cancellationToken);
    }

    public async Task<OrcamentoDto> RemoverMaoDeObraAsync(int orcamentoId, int maoDeObraId, int usuarioId, CancellationToken cancellationToken = default)
    {
        var orcamento = await _orcamentoRepository.GetWithDetailsByIdAsync(orcamentoId, cancellationToken);
        if (orcamento == null)
            throw new NotFoundException("Orçamento", orcamentoId);

        var mo = orcamento.MaosDeObra.FirstOrDefault(m => m.Id == maoDeObraId);
        if (mo == null)
            throw new NotFoundException("Mão de Obra do Orçamento", maoDeObraId);


        GarantirEditavel(orcamento);

        orcamento.MaosDeObra.Remove(mo);
        orcamento.RecalcularTotais();
        orcamento.DataAtualizacao = DateTime.UtcNow;

        await RegistrarHistoricoAsync(orcamento.Id, usuarioId, "RemoverMaoDeObra", $"Serviço '{mo.Descricao}' removido.", cancellationToken);

        await _orcamentoRepository.UpdateAsync(orcamento, cancellationToken);
        return await ObterPorIdAsync(orcamentoId, cancellationToken);
    }

    public async Task<OrcamentoDto> AssociarTecnicoAsync(int orcamentoId, AssociarTecnicoDto dto, int usuarioId, CancellationToken cancellationToken = default)
    {
        var orcamento = await _orcamentoRepository.GetWithDetailsByIdAsync(orcamentoId, cancellationToken);
        if (orcamento == null)
            throw new NotFoundException("Orçamento", orcamentoId);

        var tecnico = await _tecnicoRepository.GetByIdAsync(dto.TecnicoId, cancellationToken);
        if (tecnico == null)
            throw new NotFoundException("Técnico", dto.TecnicoId);

        if (orcamento.Tecnicos.Any(t => t.TecnicoId == dto.TecnicoId))
            throw new BusinessException($"O técnico '{tecnico.Nome}' já está associado a este orçamento.");

        orcamento.Tecnicos.Add(new OrcamentoTecnico
        {
            OrcamentoId = orcamentoId,
            TecnicoId = dto.TecnicoId,
            Funcao = dto.Funcao?.Trim(),
            Observacoes = dto.Observacoes?.Trim()
        });

        orcamento.DataAtualizacao = DateTime.UtcNow;

        await RegistrarHistoricoAsync(orcamento.Id, usuarioId, "AssociarTecnico", $"Técnico '{tecnico.Nome}' associado ao orçamento.", cancellationToken);

        await _orcamentoRepository.UpdateAsync(orcamento, cancellationToken);
        return await ObterPorIdAsync(orcamentoId, cancellationToken);
    }

    public async Task<OrcamentoDto> DesassociarTecnicoAsync(int orcamentoId, int tecnicoId, int usuarioId, CancellationToken cancellationToken = default)
    {
        var orcamento = await _orcamentoRepository.GetWithDetailsByIdAsync(orcamentoId, cancellationToken);
        if (orcamento == null)
            throw new NotFoundException("Orçamento", orcamentoId);

        var associacao = orcamento.Tecnicos.FirstOrDefault(t => t.TecnicoId == tecnicoId);
        if (associacao == null)
            throw new NotFoundException($"Técnico id '{tecnicoId}' não está associado a este orçamento.");


        GarantirEditavel(orcamento);

        orcamento.Tecnicos.Remove(associacao);
        orcamento.DataAtualizacao = DateTime.UtcNow;

        await RegistrarHistoricoAsync(orcamento.Id, usuarioId, "DesassociarTecnico", "Técnico removido do orçamento.", cancellationToken);

        await _orcamentoRepository.UpdateAsync(orcamento, cancellationToken);
        return await ObterPorIdAsync(orcamentoId, cancellationToken);
    }

    public async Task<decimal> CalcularTotalPecasAsync(int orcamentoId, CancellationToken cancellationToken = default)
    {
        var orcamento = await _orcamentoRepository.GetWithDetailsByIdAsync(orcamentoId, cancellationToken);
        if (orcamento == null)
            throw new NotFoundException("Orçamento", orcamentoId);

        return orcamento.Itens.Sum(i => i.CalcularTotal());
    }

    public async Task<decimal> CalcularTotalMaoDeObraAsync(int orcamentoId, CancellationToken cancellationToken = default)
    {
        var orcamento = await _orcamentoRepository.GetWithDetailsByIdAsync(orcamentoId, cancellationToken);
        if (orcamento == null)
            throw new NotFoundException("Orçamento", orcamentoId);

        return orcamento.MaosDeObra.Sum(m => m.CalcularTotal());
    }

    public async Task<decimal> CalcularTotalGeralAsync(int orcamentoId, CancellationToken cancellationToken = default)
    {
        var orcamento = await _orcamentoRepository.GetWithDetailsByIdAsync(orcamentoId, cancellationToken);
        if (orcamento == null)
            throw new NotFoundException("Orçamento", orcamentoId);

        orcamento.RecalcularTotais();
        return orcamento.ValorTotal;
    }

        // ======================= Validações de negócio =======================
    //
    // Centralizadas aqui (e não na View/XAML) para que as mesmas regras valham para a
    // tela, para os testes e para qualquer consumidor futuro do serviço.

    private static void ValidarCabecalho(CriarOrcamentoDto dto)
    {
        if (dto.ClienteId <= 0)
            throw new ValidationException("Selecione o cliente do orçamento.");

        if (dto.UsuarioId <= 0)
            throw new ValidationException("O orçamento deve ter um usuário responsável.");

        if (dto.DiasValidade <= 0)
            throw new ValidationException("A validade do orçamento deve ser maior que zero.");

        if (dto.ValorDesconto < 0)
            throw new ValidationException("O desconto não pode ser negativo.");

        if (dto.ValorAcrescimo < 0)
            throw new ValidationException("O acréscimo não pode ser negativo.");
    }

    private async Task ValidarItensAsync(
        List<AdicionarItemDto> itens,
        List<AdicionarMaoDeObraDto> maosDeObra,
        List<AssociarTecnicoDto> tecnicos,
        CancellationToken cancellationToken)
    {
        if (itens.Count == 0 && maosDeObra.Count == 0)
            throw new ValidationException("Adicione ao menos um item de peça ou um serviço ao orçamento.");

        foreach (var item in itens)
        {
            ValidarItem(item);

            var bruto = item.Quantidade * item.PrecoUnitario;
            if (item.ValorDesconto > bruto)
                throw new ValidationException(
                    $"O desconto do item '{item.Descricao}' é maior que o valor bruto ({bruto:N2}).");
        }

        foreach (var mo in maosDeObra)
        {
            ValidarMaoDeObra(mo);

            var bruto = mo.QuantidadeHoras * mo.ValorUnitario;
            if (mo.ValorDesconto > bruto)
                throw new ValidationException(
                    $"O desconto do serviço '{mo.Descricao}' é maior que o valor bruto ({bruto:N2}).");

            // Serviço informado deve existir no cadastro (não há digitação livre).
            if (mo.ServicoId.HasValue)
            {
                await _servicoService.ObterPorIdAsync(mo.ServicoId.Value, cancellationToken);
            }
        }

        // Técnicos vêm exclusivamente do cadastro existente, sem duplicidade.
        var tecnicosDoOrcamento = new HashSet<int>();

        foreach (var tec in tecnicos)
        {
            if (!tecnicosDoOrcamento.Add(tec.TecnicoId))
                throw new BusinessException("O mesmo técnico foi informado mais de uma vez.");
        }

        foreach (var tecId in tecnicosDoOrcamento)
        {
            if (!await _tecnicoRepository.ExistsAsync(tecId, cancellationToken))
                throw new NotFoundException("Técnico", tecId);
        }

        foreach (var mo in maosDeObra.Where(m => m.TecnicoIds is not null))
        {
            foreach (var tecId in mo.TecnicoIds!)
            {
                if (!await _tecnicoRepository.ExistsAsync(tecId, cancellationToken))
                    throw new NotFoundException("Técnico", tecId);
            }
        }
    }

    private static void ValidarItem(AdicionarItemDto dto)
    {
        if (dto.Quantidade <= 0)
            throw new ValidationException("A quantidade do item deve ser maior que zero.");

        if (dto.PrecoUnitario < 0)
            throw new ValidationException("O valor unitário do item não pode ser negativo.");

        if (dto.ValorDesconto < 0)
            throw new ValidationException("O desconto do item não pode ser negativo.");
    }

    private static void ValidarMaoDeObra(AdicionarMaoDeObraDto dto)
    {
        if (dto.QuantidadeHoras <= 0)
            throw new ValidationException("A quantidade de horas do serviço deve ser maior que zero.");

        if (dto.ValorUnitario < 0)
            throw new ValidationException("O valor do serviço não pode ser negativo.");

        if (dto.ValorDesconto < 0)
            throw new ValidationException("O desconto do serviço não pode ser negativo.");
    }

    /// <summary>Orçamento finalizado/cancelado/recusado não aceita alterações de conteúdo.</summary>
    private static void GarantirEditavel(Orcamento orcamento)
    {
        if (!OrcamentoStatus.PermiteEdicao(orcamento.Status?.Codigo))
            throw new BusinessException(
                $"O orçamento '{orcamento.Numero}' está {orcamento.Status?.Nome} e não aceita alterações.");
    }

    private async Task RegistrarHistoricoAsync(int orcamentoId, int usuarioId, string acao, string descricao, CancellationToken cancellationToken)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId, cancellationToken);
        var historico = new OrcamentoHistorico
        {
            OrcamentoId = orcamentoId,
            DataRegistro = DateTime.UtcNow,
            UsuarioId = usuarioId,
            NomeUsuario = usuario?.NomeCompleto ?? "Sistema",
            Acao = acao,
            Descricao = descricao
        };
        await _historicoRepository.AddAsync(historico, cancellationToken);
    }

    private static OrcamentoResumoDto MapearParaResumoDto(Orcamento o)
    {
        return new OrcamentoResumoDto
        {
            Id = o.Id,
            Numero = o.Numero,
            DataEmissao = o.DataEmissao,
            DataValidade = o.DataValidade,
            ClienteId = o.ClienteId,
            ClienteNome = o.Cliente?.NomeRazaoSocial ?? string.Empty,
            ClienteCpfCnpj = o.Cliente?.CpfCnpj ?? string.Empty,
            ClienteCidade = o.Cliente?.Cidade ?? string.Empty,
            ClienteUf = o.Cliente?.Uf ?? string.Empty,
            StatusId = o.StatusId,
            StatusCodigo = o.Status?.Codigo ?? string.Empty,
            StatusNome = o.Status?.Nome ?? string.Empty,
            StatusCorHex = o.Status?.CorHex,
            ValorTotal = o.ValorTotal,
            VendedorNome = o.Usuario?.NomeCompleto ?? string.Empty
        };
    }

    private static OrcamentoDto MapearParaDto(Orcamento o)
    {
        return new OrcamentoDto
        {
            Id = o.Id,
            Numero = o.Numero,
            Ano = o.Ano,
            Sequencial = o.Sequencial,
            DataEmissao = o.DataEmissao,
            DiasValidade = o.DiasValidade,
            DataValidade = o.DataValidade,
            ClienteId = o.ClienteId,
            ClienteNome = o.Cliente?.NomeRazaoSocial ?? string.Empty,
            ClienteCpfCnpj = o.Cliente?.CpfCnpj ?? string.Empty,
            ClienteTelefone = o.Cliente?.Telefone,
            ClienteCelular = o.Cliente?.Celular,
            ClienteEmail = o.Cliente?.Email,
            ClienteEnderecoCompleto = $"{o.Cliente?.Logradouro}, {o.Cliente?.Numero} - {o.Cliente?.Bairro}, {o.Cliente?.Cidade}/{o.Cliente?.Uf}",
            EmpresaId = o.EmpresaId,
            EmpresaRazaoSocial = o.Empresa?.RazaoSocial ?? string.Empty,
            EmpresaLogoPath = o.Empresa?.LogoPath,
            UsuarioId = o.UsuarioId,
            UsuarioNome = o.Usuario?.NomeCompleto ?? string.Empty,
            StatusId = o.StatusId,
            StatusCodigo = o.Status?.Codigo ?? string.Empty,
            StatusNome = o.Status?.Nome ?? string.Empty,
            StatusCorHex = o.Status?.CorHex,
            ValorTotalItens = o.ValorTotalItens,
            ValorTotalMaoDeObra = o.ValorTotalMaoDeObra,
            ValorDesconto = o.ValorDesconto,
            ValorAcrescimo = o.ValorAcrescimo,
            ValorTotal = o.ValorTotal,
            CondicoesPagamento = o.CondicoesPagamento,
            PrazoEntrega = o.PrazoEntrega,
            Garantia = o.Garantia,
            Observacoes = o.Observacoes,
            ObservacoesInternas = o.ObservacoesInternas,
            Itens = o.Itens.Select(i => new OrcamentoItemDto
            {
                Id = i.Id,
                OrcamentoId = i.OrcamentoId,
                PecaId = i.PecaId,
                NumeroItem = i.NumeroItem,
                CodigoPeca = i.CodigoPeca,
                Descricao = i.Descricao,
                UnidadeMedida = i.UnidadeMedida,
                Quantidade = i.Quantidade,
                PrecoUnitario = i.PrecoUnitario,
                ValorDesconto = i.ValorDesconto,
                ValorTotal = i.ValorTotal
            }).OrderBy(i => i.NumeroItem).ToList(),
            MaosDeObra = o.MaosDeObra.Select(m => new OrcamentoMaoDeObraDto
            {
                Id = m.Id,
                OrcamentoId = m.OrcamentoId,
                ServicoId = m.ServicoId,
                NumeroItem = m.NumeroItem,
                Descricao = m.Descricao,
                QuantidadeHoras = m.QuantidadeHoras,
                ValorUnitario = m.ValorUnitario,
                ValorDesconto = m.ValorDesconto,
                ValorTotal = m.ValorTotal,
                Observacoes = m.Observacoes,
                Tecnicos = m.Tecnicos.Select(t => new OrcamentoMaoDeObraTecnicoDto
                {
                    Id = t.Id,
                    TecnicoId = t.TecnicoId,
                    TecnicoNome = t.Tecnico?.Nome ?? string.Empty,
                    Funcao = t.Funcao,
                    Observacoes = t.Observacoes
                }).ToList()
            }).OrderBy(m => m.NumeroItem).ToList(),
            Tecnicos = o.Tecnicos.Select(t => new OrcamentoTecnicoDto
            {
                Id = t.Id,
                OrcamentoId = t.OrcamentoId,
                TecnicoId = t.TecnicoId,
                TecnicoNome = t.Tecnico?.Nome ?? string.Empty,
                Funcao = t.Funcao,
                Observacoes = t.Observacoes
            }).ToList(),
            Historicos = o.Historicos.Select(h => new OrcamentoHistoricoDto
            {
                Id = h.Id,
                DataRegistro = h.DataRegistro,
                NomeUsuario = h.NomeUsuario,
                Acao = h.Acao,
                StatusAnterior = h.StatusAnterior,
                StatusNovo = h.StatusNovo,
                Descricao = h.Descricao,
                Detalhes = h.Detalhes
            }).OrderByDescending(h => h.DataRegistro).ToList()
        };
    }
}

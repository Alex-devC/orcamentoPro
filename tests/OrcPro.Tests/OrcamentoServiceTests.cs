using System.Threading.Tasks;
using OrcPro.Application.DTOs.Orcamento;
using OrcPro.Application.Exceptions;
using OrcPro.Application.Services;
using OrcPro.Domain.Entities.Cliente;
using OrcPro.Domain.Entities.Orcamento;
using OrcPro.Domain.Entities.Peca;
using OrcPro.Domain.Entities.Seguranca;
using OrcPro.Domain.Entities.Servico;
using OrcPro.Domain.Entities.Tecnico;
using Xunit;

namespace OrcPro.Tests;

public class OrcamentoServiceTests
{
    private readonly InMemoryOrcamentoRepository _orcamentoRepo = new();
    private readonly InMemoryOrcamentoStatusRepository _statusRepo = new();
    private readonly InMemoryOrcamentoHistoricoRepository _historicoRepo = new();
    private readonly InMemoryClienteRepository _clienteRepo = new();
    private readonly InMemoryTecnicoRepository _tecnicoRepo = new();
    private readonly InMemoryUsuarioRepository _usuarioRepo = new();
    private readonly InMemoryEmpresaRepository _empresaRepo = new();
    private readonly InMemoryPecaRepository _pecaRepo = new();
    private readonly InMemoryServicoRepository _servicoRepo = new();

    private OrcamentoService Service => new(
        _orcamentoRepo, _statusRepo, _historicoRepo, _clienteRepo, _tecnicoRepo, _usuarioRepo,
        new EmpresaService(_empresaRepo, new FakeEmpresaLogoStorage()),
        new ServicoService(_servicoRepo),
        new PecaService(_pecaRepo));

    private async Task<(Cliente, Usuario, Peca, Servico, Tecnico)> PrepararAsync(bool comEmitente = true)
    {
        foreach (var s in OrcamentoStatus.CriarStatusIniciais())
            await _statusRepo.AddAsync(s);

        if (comEmitente)
            await _empresaRepo.AddAsync(new OrcPro.Domain.Entities.Empresa.Empresa
            {
                Id = 1,
                RazaoSocial = "ALEX T.I. LTDA",
                Cnpj = "11222333000181",
                Ativo = true
            });

        var cliente = await _clienteRepo.AddAsync(new Cliente
        {
            Id = 1, Codigo = "CLI-00001", NomeRazaoSocial = "CLIENTE TESTE", Celular = "11988887777", Ativo = true
        });

        var usuario = await _usuarioRepo.AddAsync(new Usuario
        {
            Id = 1, Username = "vendedor", NomeCompleto = "Vendedor Teste", Ativo = true
        });

        var peca = await _pecaRepo.AddAsync(new Peca
        {
            Id = 1, Codigo = "PEC-0001", Descricao = "CABO DE REDE", PrecoVenda = 100.00m, Ativo = true
        });

        var servico = await _servicoRepo.AddAsync(new Servico
        {
            Id = 1, Codigo = "SRV-0001", Descricao = "INSTALACAO", Valor = 150.00m, TempoEstimado = 1m, Ativo = true
        });

        var tecnico = await _tecnicoRepo.AddAsync(new Tecnico
        {
            Id = 1, Codigo = "TEC-0001", Nome = "Tecnico Teste", Ativo = true
        });

        return (cliente, usuario, peca, servico, tecnico);
    }

    private static CriarOrcamentoDto Novo(int clienteId, int usuarioId) => new()
    {
        ClienteId = clienteId,
        UsuarioId = usuarioId,
        DiasValidade = 15,
        ItensIniciais = new()
        {
            new AdicionarItemDto { PecaId = 1, CodigoPeca = "PEC-0001", Descricao = "CABO DE REDE", Quantidade = 2, PrecoUnitario = 100m }
        },
        MaosDeObraIniciais = new()
        {
            new AdicionarMaoDeObraDto { ServicoId = 1, Descricao = "INSTALACAO", QuantidadeHoras = 1, ValorUnitario = 150m }
        }
    };

    // ---------- Criação e código automático ----------

    [Fact]
    public async Task Criar_DeveGerarNumeroAutomatico()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();

        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        Assert.False(string.IsNullOrWhiteSpace(orcamento.Numero));
        Assert.Contains(DateTime.UtcNow.Year.ToString(), orcamento.Numero);
    }

    [Fact]
    public async Task Criar_DeveAvancarOSequencial()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();

        var primeiro = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));
        var segundo = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        Assert.NotEqual(primeiro.Numero, segundo.Numero);
        Assert.Equal(primeiro.Sequencial + 1, segundo.Sequencial);
    }

    [Fact]
    public async Task Criar_SemEmitenteConfigurado_DeveBloquear()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync(comEmitente: false);

        var erro = await Assert.ThrowsAsync<BusinessException>(() => Service.CriarAsync(Novo(cliente.Id, usuario.Id)));

        Assert.Contains("Minha Empresa", erro.Message);
        Assert.Empty(_orcamentoRepo.ItemsList);
    }

    [Fact]
    public async Task Criar_UsaOEmitenteDoCadastro()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();

        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        Assert.Equal(1, orcamento.EmpresaId);
    }

    [Fact]
    public async Task Criar_SemCliente_DeveFalhar()
    {
        await PrepararAsync();

        await Assert.ThrowsAsync<ValidationException>(() => Service.CriarAsync(Novo(0, 1)));
    }

    [Fact]
    public async Task Criar_ClienteInexistente_DeveFalhar()
    {
        await PrepararAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => Service.CriarAsync(Novo(999, 1)));
    }

    [Fact]
    public async Task Criar_SemItensNemServicos_DeveFalhar()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();

        var dto = Novo(cliente.Id, usuario.Id);
        dto.ItensIniciais.Clear();
        dto.MaosDeObraIniciais.Clear();

        var erro = await Assert.ThrowsAsync<ValidationException>(() => Service.CriarAsync(dto));
        Assert.Contains("ao menos um item", erro.Message);
    }

    // ---------- Itens, serviços e totais ----------

    [Fact]
    public async Task Criar_ComItemEServico_DeveCalcularOsTotais()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();

        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        Assert.Equal(200m, orcamento.ValorTotalItens);
        Assert.Equal(150m, orcamento.ValorTotalMaoDeObra);
        Assert.Equal(350m, orcamento.ValorTotal);
    }

    [Fact]
    public async Task Criar_ComDesconto_DeveDescontarDoTotal()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();

        var dto = Novo(cliente.Id, usuario.Id);
        dto.ValorDesconto = 50m;

        var orcamento = await Service.CriarAsync(dto);

        Assert.Equal(300m, orcamento.ValorTotal);
    }

    [Fact]
    public async Task Criar_ComMultiplasPecasEServicos_DeveSomarTodos()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();

        var dto = Novo(cliente.Id, usuario.Id);
        dto.ItensIniciais.Add(new AdicionarItemDto { CodigoPeca = "PEC-0002", Descricao = "OUTRA PECA", Quantidade = 3, PrecoUnitario = 10m });
        dto.MaosDeObraIniciais.Add(new AdicionarMaoDeObraDto { Descricao = "SEGUNDO SERVICO", QuantidadeHoras = 2, ValorUnitario = 50m });

        var orcamento = await Service.CriarAsync(dto);

        // Base: 1 peça do helper + 1 adicionada = 2 itens; 1 serviço + 1 adicionado = 2.
        Assert.Equal(2, orcamento.Itens.Count);
        Assert.Equal(2, orcamento.MaosDeObra.Count);
        Assert.Equal(230m, orcamento.ValorTotalItens);  // 200 + 30
        Assert.Equal(250m, orcamento.ValorTotalMaoDeObra); // 150 + 100
        Assert.Equal(480m, orcamento.ValorTotal);
    }

    [Fact]
    public async Task Criar_ComDezPecas_DeveAceitar()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();

        var dto = Novo(cliente.Id, usuario.Id);
        dto.ItensIniciais.Clear();

        for (int i = 1; i <= 10; i++)
            dto.ItensIniciais.Add(new AdicionarItemDto
            {
                CodigoPeca = $"PEC-{i:D4}",
                Descricao = $"PECA {i}",
                Quantidade = 1,
                PrecoUnitario = 10m
            });

        var orcamento = await Service.CriarAsync(dto);

        Assert.Equal(10, orcamento.Itens.Count);
        Assert.Equal(100m, orcamento.ValorTotalItens);
    }

    [Fact]
    public async Task Criar_PrecoDaPecaNoOrcamento_NaoDependeDoCadastro()
    {
        var (cliente, usuario, peca, _, _) = await PrepararAsync();

        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        // O cadastro muda depois; o orçamento guarda o valor usado no momento.
        peca.PrecoVenda = 9999m;

        var relido = await Service.ObterPorIdAsync(orcamento.Id);

        Assert.Equal(100m, relido.Itens[0].PrecoUnitario);
        Assert.Equal(200m, relido.ValorTotalItens);
    }

    [Fact]
    public async Task Criar_QuantidadeZeroOuNegativa_DeveFalhar()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();

        var dto = Novo(cliente.Id, usuario.Id);
        dto.ItensIniciais[0].Quantidade = 0;

        await Assert.ThrowsAsync<ValidationException>(() => Service.CriarAsync(dto));

        dto.ItensIniciais[0].Quantidade = -5;
        await Assert.ThrowsAsync<ValidationException>(() => Service.CriarAsync(dto));
    }

    [Fact]
    public async Task Criar_ValorNegativo_DeveFalhar()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();

        var dto = Novo(cliente.Id, usuario.Id);
        dto.ItensIniciais[0].PrecoUnitario = -10m;

        await Assert.ThrowsAsync<ValidationException>(() => Service.CriarAsync(dto));
    }

    [Fact]
    public async Task Criar_DescontoMaiorQueOBruto_DeveFalhar()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();

        var dto = Novo(cliente.Id, usuario.Id);
        dto.ItensIniciais[0].ValorDesconto = 500m;

        await Assert.ThrowsAsync<ValidationException>(() => Service.CriarAsync(dto));
    }

    [Fact]
    public async Task Criar_ServicoInexistente_DeveFalhar()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();

        var dto = Novo(cliente.Id, usuario.Id);
        dto.MaosDeObraIniciais[0].ServicoId = 999;

        await Assert.ThrowsAsync<NotFoundException>(() => Service.CriarAsync(dto));
    }

    // ---------- Técnicos ----------

    [Fact]
    public async Task Criar_ComTecnicos_DeveAssociar()
    {
        var (cliente, usuario, _, _, tecnico) = await PrepararAsync();

        var dto = Novo(cliente.Id, usuario.Id);
        dto.TecnicosIniciais.Add(new AssociarTecnicoDto { TecnicoId = tecnico.Id, Funcao = "INSTALADOR" });

        var orcamento = await Service.CriarAsync(dto);

        Assert.Single(orcamento.Tecnicos);
        Assert.Equal(tecnico.Id, orcamento.Tecnicos[0].TecnicoId);
    }

    [Fact]
    public async Task Criar_TecnicoInexistente_DeveFalhar()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();

        var dto = Novo(cliente.Id, usuario.Id);
        dto.TecnicosIniciais.Add(new AssociarTecnicoDto { TecnicoId = 999 });

        await Assert.ThrowsAsync<NotFoundException>(() => Service.CriarAsync(dto));
    }

    [Fact]
    public async Task RemoverTecnico_NaoDeveApagarOCadastroGlobal()
    {
        var (cliente, usuario, _, _, tecnico) = await PrepararAsync();

        var dto = Novo(cliente.Id, usuario.Id);
        dto.TecnicosIniciais.Add(new AssociarTecnicoDto { TecnicoId = tecnico.Id });

        var orcamento = await Service.CriarAsync(dto);
        await Service.DesassociarTecnicoAsync(orcamento.Id, tecnico.Id, usuario.Id);

        var relido = await Service.ObterPorIdAsync(orcamento.Id);

        Assert.Empty(relido.Tecnicos);
        // O técnico continua no cadastro.
        Assert.True(await _tecnicoRepo.ExistsAsync(tecnico.Id));
    }

    // ---------- Histórico e usuário responsável ----------

    [Fact]
    public async Task Criar_DeveRegistrarHistoricoComOUsuarioDaSessao()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();

        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        var historico = Assert.Single(orcamento.Historicos);
        Assert.Equal("Criacao", historico.Acao);
        Assert.Equal(usuario.Id, orcamento.UsuarioId);
        Assert.Contains("Vendedor Teste", historico.NomeUsuario!);
    }

    [Fact]
    public async Task Historico_DeveRegistrarAlteracaoDeStatusComUsuario()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();
        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        var aguardando = await _statusRepo.GetByCodigoAsync(OrcamentoStatus.CodigoAguardandoAprovacao);
        Assert.NotNull(aguardando);

        await Service.AlterarStatusAsync(new AlterarStatusOrcamentoDto
        {
            OrcamentoId = orcamento.Id,
            NovoStatusId = aguardando!.Id,
            UsuarioId = usuario.Id,
            ObservacaoMotivo = "Enviado ao cliente"
        });

        var relido = await Service.ObterPorIdAsync(orcamento.Id);

        Assert.Equal(OrcamentoStatus.CodigoAguardandoAprovacao, relido.StatusCodigo);

        // O histórico vive no repositório próprio (vinculado por OrcamentoId).
        var registro = Assert.Single(_historicoRepo.ItemsList,
            h => h.OrcamentoId == orcamento.Id && h.Acao == "AlteracaoStatus");

        Assert.Equal(aguardando.Nome, registro.StatusNovo);
        Assert.Contains("Enviado ao cliente", registro.Detalhes!);
    }

    [Fact]
    public async Task Historico_UsuarioDiferente_DeveSerRegistradoCorretamente()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();
        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        var outro = await _usuarioRepo.AddAsync(new Usuario
        {
            Id = 2, Username = "gerente", NomeCompleto = "Gerente Teste", Ativo = true
        });

        var aprovado = await _statusRepo.GetByCodigoAsync(OrcamentoStatus.CodigoAguardandoAprovacao);
        await Service.AlterarStatusAsync(new AlterarStatusOrcamentoDto
        {
            OrcamentoId = orcamento.Id, NovoStatusId = aprovado!.Id, UsuarioId = usuario.Id
        });

        await Service.AlterarStatusAsync(new AlterarStatusOrcamentoDto
        {
            OrcamentoId = orcamento.Id, NovoStatusId = (await _statusRepo.GetByCodigoAsync(OrcamentoStatus.CodigoAprovado))!.Id, UsuarioId = outro.Id
        });

        var relido = await Service.ObterPorIdAsync(orcamento.Id);
        var doGerente = Assert.Single(_historicoRepo.ItemsList, h => h.NomeUsuario == "Gerente Teste");

        Assert.Equal("AlteracaoStatus", doGerente.Acao);
        Assert.Equal(orcamento.Id, doGerente.OrcamentoId);
        Assert.Equal(OrcamentoStatus.CodigoAprovado, relido.StatusCodigo);
    }

    // ---------- Alteração de status ----------

    private static AlterarStatusOrcamentoDto Alterar(int orcamentoId, int statusId, int usuarioId, string? motivo = null)
        => new() { OrcamentoId = orcamentoId, NovoStatusId = statusId, UsuarioId = usuarioId, ObservacaoMotivo = motivo };

    [Fact]
    public async Task AlterarStatus_FluxoNormal_RascunhoParaAprovadoParaFinalizado()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();
        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        var aguardando = await _statusRepo.GetByCodigoAsync(OrcamentoStatus.CodigoAguardandoAprovacao);
        var aprovado = await _statusRepo.GetByCodigoAsync(OrcamentoStatus.CodigoAprovado);
        var emExecucao = await _statusRepo.GetByCodigoAsync(OrcamentoStatus.CodigoEmExecucao);
        var finalizado = await _statusRepo.GetByCodigoAsync(OrcamentoStatus.CodigoFinalizado);

        await Service.AlterarStatusAsync(Alterar(orcamento.Id, aguardando!.Id, usuario.Id));
        Assert.Equal(OrcamentoStatus.CodigoAguardandoAprovacao, (await Service.ObterPorIdAsync(orcamento.Id)).StatusCodigo);

        await Service.AlterarStatusAsync(Alterar(orcamento.Id, aprovado!.Id, usuario.Id));
        Assert.Equal(OrcamentoStatus.CodigoAprovado, (await Service.ObterPorIdAsync(orcamento.Id)).StatusCodigo);

        await Service.AlterarStatusAsync(Alterar(orcamento.Id, emExecucao!.Id, usuario.Id));
        await Service.AlterarStatusAsync(Alterar(orcamento.Id, finalizado!.Id, usuario.Id));

        Assert.Equal(OrcamentoStatus.CodigoFinalizado, (await Service.ObterPorIdAsync(orcamento.Id)).StatusCodigo);
    }

    [Fact]
    public async Task AlterarStatus_PermiteCancelar()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();
        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        var cancelado = await _statusRepo.GetByCodigoAsync(OrcamentoStatus.CodigoCancelado);
        await Service.AlterarStatusAsync(Alterar(orcamento.Id, cancelado!.Id, usuario.Id, "Cliente desistiu"));

        Assert.Equal(OrcamentoStatus.CodigoCancelado, (await Service.ObterPorIdAsync(orcamento.Id)).StatusCodigo);
    }

    [Fact]
    public async Task AlterarStatus_TransicaoAbsurda_DeveSerRecusada()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();
        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        // Rascunho -> Aprovado não é permitido (passaria por Aguardando Aprovação).
        var aprovado = await _statusRepo.GetByCodigoAsync(OrcamentoStatus.CodigoAprovado);

        await Assert.ThrowsAsync<BusinessException>(() =>
            Service.AlterarStatusAsync(Alterar(orcamento.Id, aprovado!.Id, usuario.Id)));

        Assert.Equal(OrcamentoStatus.CodigoRascunho, (await Service.ObterPorIdAsync(orcamento.Id)).StatusCodigo);
    }

    [Fact]
    public async Task AlterarStatus_OrcamentoCancelado_NaoVoltaAoRascunho()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();
        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        var cancelado = await _statusRepo.GetByCodigoAsync(OrcamentoStatus.CodigoCancelado);
        var rascunho = await _statusRepo.GetByCodigoAsync(OrcamentoStatus.CodigoRascunho);

        await Service.AlterarStatusAsync(Alterar(orcamento.Id, cancelado!.Id, usuario.Id));

        await Assert.ThrowsAsync<BusinessException>(() =>
            Service.AlterarStatusAsync(Alterar(orcamento.Id, rascunho!.Id, usuario.Id)));
    }

    [Fact]
    public async Task AlterarStatus_DeveGerarHistoricoComStatusAnteriorENovo()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();
        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        var aguardando = await _statusRepo.GetByCodigoAsync(OrcamentoStatus.CodigoAguardandoAprovacao);
        await Service.AlterarStatusAsync(Alterar(orcamento.Id, aguardando!.Id, usuario.Id));

        var registro = _historicoRepo.ItemsList.Last(h => h.Acao == "AlteracaoStatus");
        Assert.Equal("Rascunho", registro.StatusAnterior);
        Assert.Equal(aguardando.Nome, registro.StatusNovo);
    }

    [Fact]
    public async Task AlterarStatus_StatusInexistente_DeveFalhar()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();
        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Service.AlterarStatusAsync(Alterar(orcamento.Id, 999, usuario.Id)));
    }

    // ---------- Exclusão ----------

    [Fact]
    public async Task Excluir_DeveRemoverOrcamento()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();
        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        await Service.ExcluirAsync(new ExcluirOrcamentoDto { OrcamentoId = orcamento.Id, UsuarioId = usuario.Id });

        Assert.Empty(_orcamentoRepo.ItemsList);
        await Assert.ThrowsAsync<NotFoundException>(() => Service.ObterPorIdAsync(orcamento.Id));
    }

    [Fact]
    public async Task Excluir_OrcamentoFinalizado_DeveSerBloqueado()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();
        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        foreach (var codigo in new[]
        {
            OrcamentoStatus.CodigoAguardandoAprovacao,
            OrcamentoStatus.CodigoAprovado,
            OrcamentoStatus.CodigoEmExecucao,
            OrcamentoStatus.CodigoFinalizado
        })
        {
            var status = await _statusRepo.GetByCodigoAsync(codigo);
            await Service.AlterarStatusAsync(Alterar(orcamento.Id, status!.Id, usuario.Id));
        }

        var erro = await Assert.ThrowsAsync<BusinessException>(() =>
            Service.ExcluirAsync(new ExcluirOrcamentoDto { OrcamentoId = orcamento.Id, UsuarioId = usuario.Id }));

        Assert.Contains("exclu", erro.Message);
        Assert.Single(_orcamentoRepo.ItemsList);
    }

    [Fact]
    public async Task Excluir_OrcamentoCancelado_DeveSerBloqueado()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();
        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        var cancelado = await _statusRepo.GetByCodigoAsync(OrcamentoStatus.CodigoCancelado);
        await Service.AlterarStatusAsync(Alterar(orcamento.Id, cancelado!.Id, usuario.Id));

        await Assert.ThrowsAsync<BusinessException>(() =>
            Service.ExcluirAsync(new ExcluirOrcamentoDto { OrcamentoId = orcamento.Id, UsuarioId = usuario.Id }));
    }

    [Fact]
    public async Task Excluir_Inexistente_DeveFalhar()
    {
        await PrepararAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Service.ExcluirAsync(new ExcluirOrcamentoDto { OrcamentoId = 999, UsuarioId = 1 }));
    }

    // ---------- Edição ----------

    [Fact]
    public async Task Editar_DeveAtualizarCabecalhoERecalcularTotais()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();
        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        await Service.EditarAsync(new AtualizarOrcamentoDto
        {
            Id = orcamento.Id,
            ClienteId = cliente.Id,
            DiasValidade = 30,
            ValorDesconto = 50m,
            CondicoesPagamento = "28/56",
            Observacoes = "CLIENTE PEDIU ENVIO"
        }, usuario.Id);

        var relido = await Service.ObterPorIdAsync(orcamento.Id);

        Assert.Equal(30, relido.DiasValidade);
        Assert.Equal(50m, relido.ValorDesconto);
        Assert.Equal(300m, relido.ValorTotal);
        Assert.Equal("CLIENTE PEDIU ENVIO", relido.Observacoes);
    }

    [Fact]
    public async Task Editar_NaoPodeAlterarONumero()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();
        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));
        var numeroOriginal = orcamento.Numero;

        await Service.EditarAsync(new AtualizarOrcamentoDto
        {
            Id = orcamento.Id, ClienteId = cliente.Id, DiasValidade = 15, Observacoes = "ALTERADO"
        }, usuario.Id);

        Assert.Equal(numeroOriginal, (await Service.ObterPorIdAsync(orcamento.Id)).Numero);
    }

    [Fact]
    public async Task Editar_OrcamentoCancelado_DeveSerBloqueado()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();
        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        var cancelado = await _statusRepo.GetByCodigoAsync(OrcamentoStatus.CodigoCancelado);
        await Service.AlterarStatusAsync(Alterar(orcamento.Id, cancelado!.Id, usuario.Id));

        await Assert.ThrowsAsync<BusinessException>(() =>
            Service.EditarAsync(new AtualizarOrcamentoDto
            {
                Id = orcamento.Id, ClienteId = cliente.Id, DiasValidade = 15
            }, usuario.Id));
    }

    // ---------- Itens e serviços ----------

    [Fact]
    public async Task AdicionarItem_PosCriacao_DeveAtualizarTotal()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();
        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        await Service.AdicionarItemAsync(orcamento.Id, new AdicionarItemDto
        {
            CodigoPeca = "PEC-0009", Descricao = "ITEM EXTRA", Quantidade = 4, PrecoUnitario = 25m
        }, usuario.Id);

        var relido = await Service.ObterPorIdAsync(orcamento.Id);

        Assert.Equal(2, relido.Itens.Count);
        Assert.Equal(300m, relido.ValorTotalItens);
        Assert.Equal(450m, relido.ValorTotal);
    }

    [Fact]
    public async Task RemoverItem_DeveRecalcularTotal()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();
        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        var item = (await Service.ObterPorIdAsync(orcamento.Id)).Itens[0];
        await Service.RemoverItemAsync(orcamento.Id, item.Id, usuario.Id);

        var relido = await Service.ObterPorIdAsync(orcamento.Id);

        Assert.Empty(relido.Itens);
        Assert.Equal(0m, relido.ValorTotalItens);
        Assert.Equal(150m, relido.ValorTotal);
    }

    [Fact]
    public async Task AdicionarItem_QuantidadeZero_DeveFalhar()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();
        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        await Assert.ThrowsAsync<ValidationException>(() =>
            Service.AdicionarItemAsync(orcamento.Id, new AdicionarItemDto
            {
                CodigoPeca = "X", Descricao = "INVALIDO", Quantidade = 0, PrecoUnitario = 10m
            }, usuario.Id));
    }

    [Fact]
    public async Task AdicionarMaoDeObra_ComServicoDoCadastro_DeveVincularServicoId()
    {
        var (cliente, usuario, _, servico, _) = await PrepararAsync();
        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        await Service.AdicionarMaoDeObraAsync(orcamento.Id, new AdicionarMaoDeObraDto
        {
            ServicoId = servico.Id,
            Descricao = "SERVICO DO CADASTRO",
            QuantidadeHoras = 2,
            ValorUnitario = 80m
        }, usuario.Id);

        var relido = await Service.ObterPorIdAsync(orcamento.Id);
        var linha = relido.MaosDeObra.Single(m => m.Descricao == "SERVICO DO CADASTRO");

        Assert.Equal(servico.Id, linha.ServicoId);
        Assert.Equal(160m, linha.ValorTotal);
    }

    [Fact]
    public async Task AdicionarMaoDeObra_ValorFicaGravadoNoOrcamento()
    {
        var (cliente, usuario, _, servico, _) = await PrepararAsync();
        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        await Service.AdicionarMaoDeObraAsync(orcamento.Id, new AdicionarMaoDeObraDto
        {
            ServicoId = servico.Id, Descricao = "AJUSTADO", QuantidadeHoras = 1, ValorUnitario = 999m
        }, usuario.Id);

        // O cadastro muda depois; o orçamento não muda junto.
        servico.Valor = 1m;

        var relido = await Service.ObterPorIdAsync(orcamento.Id);
        Assert.Equal(999m, relido.MaosDeObra.Single(m => m.Descricao == "AJUSTADO").ValorUnitario);
    }

    [Fact]
    public async Task AssociarTecnico_Duplicado_DeveSerBloqueado()
    {
        var (cliente, usuario, _, _, tecnico) = await PrepararAsync();
        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        await Service.AssociarTecnicoAsync(orcamento.Id, new AssociarTecnicoDto { TecnicoId = tecnico.Id }, usuario.Id);

        await Assert.ThrowsAsync<BusinessException>(() =>
            Service.AssociarTecnicoAsync(orcamento.Id, new AssociarTecnicoDto { TecnicoId = tecnico.Id }, usuario.Id));
    }

    // ---------- Totais via serviço ----------

    [Fact]
    public async Task CalcularTotais_DeveRetornarOsSubtotais()
    {
        var (cliente, usuario, _, _, _) = await PrepararAsync();
        var orcamento = await Service.CriarAsync(Novo(cliente.Id, usuario.Id));

        Assert.Equal(200m, await Service.CalcularTotalPecasAsync(orcamento.Id));
        Assert.Equal(150m, await Service.CalcularTotalMaoDeObraAsync(orcamento.Id));
        Assert.Equal(350m, await Service.CalcularTotalGeralAsync(orcamento.Id));
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OrcPro.Application.DTOs.Auth;
using OrcPro.Application.DTOs.Empresa;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Common;
using OrcPro.Domain.Common.Formatters;
using CpfCnpjValidatorEx = OrcPro.Domain.Common.Formatters.CpfCnpjValidator;

namespace OrcPro.App.ViewModels;

/// <summary>
/// Tela "Minha Empresa" / Emitente: registro único da instalação que alimentará Orçamentos,
/// PDF e relatórios.
///
/// <para>Não é um CRUD de empresas: se ainda não houver emitente, a tela abre vazia para
/// preenchimento; se já existir, carrega e permite editar. O botão Salvar sempre resolve o
/// registro existente, de modo que um segundo emitente não possa ser criado.</para>
///
/// <para>A consulta de CEP acontece <b>somente</b> no clique explícito na lupa — digitar,
/// apagar, sair do campo ou pressionar ENTER nunca consultam, exatamente como em Clientes
/// e Técnicos.</para>
/// </summary>
public class MinhaEmpresaViewModel : ViewModelBase
{
    // Nomes lógicos dos campos: chave da validação compartilhada (resumo no topo,
    // destaque do campo inválido e foco no primeiro erro) e Tag usada pelo FormFocusHelper.
    private const string CampoRazaoSocial = "RazaoSocial";
    private const string CampoCnpj = "Cnpj";
    private const string CampoEmail = "Email";
    private const string CampoEmailFinanceiro = "EmailFinanceiro";
    private const string CampoCep = "CEP";
    private const string CampoUf = "Uf";

    private readonly IEmpresaService _empresaService;
    private readonly ICepService? _cepService;
    private readonly Action<string>? _reportStatus;

    private readonly bool _podeVisualizar;
    private readonly bool _podeEditar;

    private int _formId;
    private bool _estaConfigurado;
    private bool _carregando;
    private bool _cepCarregando;

    private string _formRazaoSocial = string.Empty;
    private string _formNomeFantasia = string.Empty;
    private string _formCnpj = string.Empty;
    private string _formInscricaoEstadual = string.Empty;
    private string _formInscricaoMunicipal = string.Empty;
    private string _formTelefone = string.Empty;
    private string _formCelular = string.Empty;
    private string _formEmail = string.Empty;
    private string _formEmailFinanceiro = string.Empty;
    private string _formWebsite = string.Empty;
    private string _formCep = string.Empty;
    private string _formLogradouro = string.Empty;
    private string _formNumero = string.Empty;
    private string _formComplemento = string.Empty;
    private string _formBairro = string.Empty;
    private string _formCidade = string.Empty;
    private string _formUf = string.Empty;
    private string _formObservacoes = string.Empty;

    private string _editorMensagem = string.Empty;

    /// <summary>Caminho absoluto da imagem escolhida, ainda não importada.</summary>
    private string? _novoLogoCaminhoOrigem;

    /// <summary>Marca para apagar a logo ao salvar (o arquivo só é removido no Salvar).</summary>
    private bool _removerLogo;

    private string? _logoPersistido;
    private ImageSource? _logoPreview;
    private string _logoNome = string.Empty;

    public MinhaEmpresaViewModel(
        IEmpresaService empresaService,
        UsuarioSessaoDto sessao,
        ICepService? cepService = null,
        Action<string>? reportStatus = null)
    {
        _empresaService = empresaService;
        _cepService = cepService;
        _reportStatus = reportStatus;

        _podeVisualizar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Emitente.Visualizar);
        _podeEditar = sessao.PossuiPermissao(PermissaoCatalogo.Codigos.Emitente.Editar);

        SalvarCommand = new AsyncRelayCommand(_ => SalvarAsync(), _ => _podeEditar);
        CancelarCommand = new AsyncRelayCommand(_ => CancelarAsync());
        SelecionarLogoCommand = new RelayCommand(_ => SelecionarLogo(), _ => _podeEditar);
        RemoverLogoCommand = new RelayCommand(_ => SolicitarRemoverLogo(), _ => _podeEditar);
        ConsultarCepCommand = new AsyncRelayCommand(_ => ConsultarCepManualmenteAsync());
    }

    public bool PodeVisualizar => _podeVisualizar;
    public bool PodeEditar => _podeEditar;

    /// <summary>Emitente ainda não cadastrado: a tela mostra formulário vazio.</summary>
    public bool EstaConfigurado => _estaConfigurado;

    public bool Carregando
    {
        get => _carregando;
        private set => SetField(ref _carregando, value);
    }

    public bool CepCarregando
    {
        get => _cepCarregando;
        private set
        {
            if (SetField(ref _cepCarregando, value))
                OnPropertyChanged(nameof(CepPodeConsultarManualmente));
        }
    }

    /// <summary>Consulta via lupa liberada apenas com serviço disponível e 8 dígitos.</summary>
    public bool CepPodeConsultarManualmente => _cepService is not null && !CepCarregando && CepMaskHelper.EhValido(FormCep);

    public bool CepTemErro => CampoInvalido(CampoCep);
    public string CepMensagemErro => ErrosValidacao.TryGetValue(CampoCep, out var msg) ? msg : string.Empty;

    public string EditorMensagem
    {
        get => _editorMensagem;
        private set
        {
            if (SetField(ref _editorMensagem, value))
                OnPropertyChanged(nameof(EditorMensagemVisivel));
        }
    }

    public bool EditorMensagemVisivel => !string.IsNullOrWhiteSpace(EditorMensagem);

    /// <summary>
    /// Última mensagem enviada ao rodapé de status. Fica exposta para que a UI e os testes
    /// saibam quando uma operação assíncrona (ex.: salvar) terminou de fato.
    /// </summary>
    public string UltimaMensagemStatus { get; private set; } = string.Empty;

    /// <summary>
    /// Quantidade de mensagens já publicadas no status. Serve como sinal inequívoco de
    /// conclusão de uma operação (o texto pode se repetir entre salvamentos).
    /// </summary>
    public int StatusVersion { get; private set; }

    /// <summary>Publica um aviso no rodapé de status.</summary>
    private void Report(string mensagem)
    {
        UltimaMensagemStatus = mensagem;
        StatusVersion++;
        OnPropertyChanged(nameof(UltimaMensagemStatus));
        OnPropertyChanged(nameof(StatusVersion));
        _reportStatus?.Invoke(mensagem);
    }

    public AsyncRelayCommand SalvarCommand { get; }
    public AsyncRelayCommand CancelarCommand { get; }
    public RelayCommand SelecionarLogoCommand { get; }
    public RelayCommand RemoverLogoCommand { get; }
    public AsyncRelayCommand ConsultarCepCommand { get; }
    // ---------- Identificação / Fiscal ----------
    public string FormRazaoSocial
    {
        get => _formRazaoSocial;
        set => SetField(ref _formRazaoSocial, value);
    }

    public bool RazaoSocialTemErro => CampoInvalido(CampoRazaoSocial);

    public string FormNomeFantasia
    {
        get => _formNomeFantasia;
        set => SetField(ref _formNomeFantasia, value);
    }

    public string FormCnpj
    {
        get => _formCnpj;
        set => SetField(ref _formCnpj, value);
    }

    public bool CnpjTemErro => CampoInvalido(CampoCnpj);

    public string FormInscricaoEstadual
    {
        get => _formInscricaoEstadual;
        set => SetField(ref _formInscricaoEstadual, value);
    }

    public string FormInscricaoMunicipal
    {
        get => _formInscricaoMunicipal;
        set => SetField(ref _formInscricaoMunicipal, value);
    }

    // ---------- Contato ----------
    public string FormTelefone
    {
        get => _formTelefone;
        set => SetField(ref _formTelefone, value);
    }

    public string FormCelular
    {
        get => _formCelular;
        set => SetField(ref _formCelular, value);
    }

    public string FormEmail
    {
        get => _formEmail;
        set => SetField(ref _formEmail, value);
    }

    public bool EmailTemErro => CampoInvalido(CampoEmail);

    public string FormEmailFinanceiro
    {
        get => _formEmailFinanceiro;
        set => SetField(ref _formEmailFinanceiro, value);
    }

    public bool EmailFinanceiroTemErro => CampoInvalido(CampoEmailFinanceiro);

    public string FormWebsite
    {
        get => _formWebsite;
        set => SetField(ref _formWebsite, value);
    }

    // ---------- Endereço ----------
    public string FormCep
    {
        get => _formCep;
        set
        {
            if (!SetField(ref _formCep, value)) return;
            OnPropertyChanged(nameof(CepPodeConsultarManualmente));
        }
    }

    public string FormLogradouro
    {
        get => _formLogradouro;
        set => SetField(ref _formLogradouro, value);
    }

    public string FormNumero
    {
        get => _formNumero;
        set => SetField(ref _formNumero, value);
    }

    public string FormComplemento
    {
        get => _formComplemento;
        set => SetField(ref _formComplemento, value);
    }

    public string FormBairro
    {
        get => _formBairro;
        set => SetField(ref _formBairro, value);
    }

    public string FormCidade
    {
        get => _formCidade;
        set => SetField(ref _formCidade, value);
    }

    public string FormUf
    {
        get => _formUf;
        set => SetField(ref _formUf, value);
    }

    public bool UfTemErro => CampoInvalido(CampoUf);

    public string FormObservacoes
    {
        get => _formObservacoes;
        set => SetField(ref _formObservacoes, value);
    }

    // ---------- Logo ----------
    /// <summary>Imagem escolhida na sessão, ainda não importada pelo serviço.</summary>
    public string? NovoLogoCaminhoOrigem
    {
        get => _novoLogoCaminhoOrigem;
        private set
        {
            if (SetField(ref _novoLogoCaminhoOrigem, value))
                OnPropertyChanged(nameof(LogoSelecionada));
        }
    }

    /// <summary>Marca de remoção da logo, aplicada ao salvar.</summary>
    public bool RemoverLogo
    {
        get => _removerLogo;
        private set
        {
            if (SetField(ref _removerLogo, value))
                OnPropertyChanged(nameof(LogoSelecionada));
        }
    }

    public ImageSource? LogoPreview
    {
        get => _logoPreview;
        private set => SetField(ref _logoPreview, value);
    }

    /// <summary>Nome do arquivo de logo já persistido (mostrado ao usuário).</summary>
    public string LogoNome
    {
        get => _logoNome;
        private set => SetField(ref _logoNome, value);
    }

    /// <summary>Há logo para exibir (recém-escolhida ou já persistida e ainda não removida).</summary>
    public bool LogoSelecionada => !RemoverLogo && (NovoLogoCaminhoOrigem is not null || _logoPersistido is not null);

    protected override void AoAlterarValidacao(string campo)
    {
        base.AoAlterarValidacao(campo);

        switch (campo)
        {
            case CampoRazaoSocial:
                OnPropertyChanged(nameof(RazaoSocialTemErro));
                break;
            case CampoCnpj:
                OnPropertyChanged(nameof(CnpjTemErro));
                break;
            case CampoEmail:
                OnPropertyChanged(nameof(EmailTemErro));
                break;
            case CampoEmailFinanceiro:
                OnPropertyChanged(nameof(EmailFinanceiroTemErro));
                break;
            case CampoCep:
                OnPropertyChanged(nameof(CepTemErro));
                OnPropertyChanged(nameof(CepMensagemErro));
                break;
            case CampoUf:
                OnPropertyChanged(nameof(UfTemErro));
                break;
        }
    }

    public override async Task InitializeAsync()
    {
        await CarregarAsync();
    }
    // ======================= Carga =======================

    /// <summary>
    /// Carrega o emitente. Sem emitente configurado, o formulário abre vazio — nenhuma
    /// empresa fictícia é criada automaticamente.
    /// </summary>
    public async Task CarregarAsync()
    {
        Carregando = true;
        try
        {
            var empresa = await _empresaService.ObterAsync();

            _estaConfigurado = empresa is not null;
            OnPropertyChanged(nameof(EstaConfigurado));

            if (empresa is null)
            {
                LimparFormulario();
                EditorMensagem = string.Empty;
                return;
            }

            await PreencherFormulario(empresa);
            EditorMensagem = string.Empty;
        }
        catch (Exception ex)
        {
            EditorMensagem = $"Falha ao carregar a empresa: {ex.Message}";
        }
        finally
        {
            Carregando = false;
        }
    }

    private async Task PreencherFormulario(EmpresaDto e)
    {
        _formId = e.Id;
        FormRazaoSocial = e.RazaoSocial;
        FormNomeFantasia = e.NomeFantasia;
        FormCnpj = CpfCnpjValidatorEx.Formatar(e.Cnpj);
        FormInscricaoEstadual = e.InscricaoEstadual ?? string.Empty;
        FormInscricaoMunicipal = e.InscricaoMunicipal ?? string.Empty;
        FormTelefone = TelefoneParaEdicao(e.Telefone);
        FormCelular = TelefoneParaEdicao(e.Celular);
        FormEmail = e.Email ?? string.Empty;
        FormEmailFinanceiro = e.EmailFinanceiro ?? string.Empty;
        FormWebsite = e.Website ?? string.Empty;
        FormCep = CepMaskHelper.Formatar(e.Cep);
        FormLogradouro = e.Logradouro ?? string.Empty;
        FormNumero = e.Numero ?? string.Empty;
        FormComplemento = e.Complemento ?? string.Empty;
        FormBairro = e.Bairro ?? string.Empty;
        FormCidade = e.Cidade ?? string.Empty;
        FormUf = e.Uf ?? string.Empty;
        FormObservacoes = e.Observacoes ?? string.Empty;

        _novoLogoCaminhoOrigem = null;
        _removerLogo = false;
        _logoPersistido = e.LogoPath;
        LogoNome = e.LogoPath ?? string.Empty;
        await CarregarPreviewDaLogoPersistida();

        OnPropertyChanged(nameof(NovoLogoCaminhoOrigem));
        OnPropertyChanged(nameof(RemoverLogo));
        OnPropertyChanged(nameof(LogoSelecionada));

        LimparErrosValidacao();
    }

    private void LimparFormulario()
    {
        _formId = 0;
        FormRazaoSocial = string.Empty;
        FormNomeFantasia = string.Empty;
        FormCnpj = string.Empty;
        FormInscricaoEstadual = string.Empty;
        FormInscricaoMunicipal = string.Empty;
        FormTelefone = string.Empty;
        FormCelular = string.Empty;
        FormEmail = string.Empty;
        FormEmailFinanceiro = string.Empty;
        FormWebsite = string.Empty;
        FormCep = string.Empty;
        FormLogradouro = string.Empty;
        FormNumero = string.Empty;
        FormComplemento = string.Empty;
        FormBairro = string.Empty;
        FormCidade = string.Empty;
        FormUf = string.Empty;
        FormObservacoes = string.Empty;

        _novoLogoCaminhoOrigem = null;
        _removerLogo = false;
        _logoPersistido = null;
        LogoNome = string.Empty;
        LogoPreview = null;

        OnPropertyChanged(nameof(NovoLogoCaminhoOrigem));
        OnPropertyChanged(nameof(RemoverLogo));
        OnPropertyChanged(nameof(LogoSelecionada));

        LimparErrosValidacao();
    }

    /// <summary>Telefone é persistido só com dígitos; exibe com máscara.</summary>
    private static string TelefoneParaEdicao(string? valor)
        => PhoneMaskHelper.EhValido(valor) ? PhoneMaskHelper.Formatar(valor) : string.Empty;

    // ======================= Validação e salvamento =======================

    private void AtualizarErroCampo(string campo, ValidationResult resultado)
    {
        if (resultado.IsValid)
            LimparErroValidacao(campo);
        else
            DefinirErroValidacao(campo, resultado.FirstError);
    }

    private async Task SalvarAsync()
    {
        var resultadoRazao = FormValidator.Required(FormRazaoSocial, "a razão social");
        var resultadoCnpj = ValidarCnpj();
        var resultadoEmail = FormValidator.Email(FormEmail, "e-mail principal");
        var resultadoEmailFin = FormValidator.Email(FormEmailFinanceiro, "e-mail financeiro");
        var resultadoCep = ValidarCep();
        var resultadoUf = ValidarUf();

        AtualizarErroCampo(CampoRazaoSocial, resultadoRazao);
        AtualizarErroCampo(CampoCnpj, resultadoCnpj);
        AtualizarErroCampo(CampoEmail, resultadoEmail);
        AtualizarErroCampo(CampoEmailFinanceiro, resultadoEmailFin);
        AtualizarErroCampo(CampoCep, resultadoCep);
        AtualizarErroCampo(CampoUf, resultadoUf);

        if (!resultadoRazao.IsValid || !resultadoCnpj.IsValid || !resultadoEmail.IsValid
            || !resultadoEmailFin.IsValid || !resultadoCep.IsValid || !resultadoUf.IsValid)
        {
            var primeiro = new[] { resultadoRazao, resultadoCnpj, resultadoEmail, resultadoEmailFin, resultadoCep, resultadoUf }
                .First(r => !r.IsValid);

            EditorMensagem = primeiro.FirstError;
            SolicitarFocoPrimeiroCampoInvalido();
            return;
        }

        try
        {
            var dto = new SalvarEmpresaDto
            {
                Id = _formId,
                RazaoSocial = FormRazaoSocial,
                NomeFantasia = FormNomeFantasia,
                Cnpj = FormCnpj,
                InscricaoEstadual = FormInscricaoEstadual,
                InscricaoMunicipal = FormInscricaoMunicipal,
                Telefone = FormTelefone,
                Celular = FormCelular,
                Email = FormEmail,
                EmailFinanceiro = FormEmailFinanceiro,
                Website = FormWebsite,
                Cep = FormCep,
                Logradouro = FormLogradouro,
                Numero = FormNumero,
                Complemento = FormComplemento,
                Bairro = FormBairro,
                Cidade = FormCidade,
                Uf = FormUf,
                Observacoes = FormObservacoes,
                NovoLogoCaminhoOrigem = NovoLogoCaminhoOrigem,
                RemoverLogo = RemoverLogo
            };

            await _empresaService.SalvarAsync(dto);

            await CarregarAsync();

            Report("Minha empresa salva com sucesso.");
            EditorMensagem = string.Empty;
        }
        catch (Exception ex)
        {
            // Mensagem real do serviço (CNPJ inválido, logo não suportada, etc.).
            EditorMensagem = ex.Message;
        }
    }

    private ValidationResult ValidarCnpj()
    {
        if (string.IsNullOrWhiteSpace(FormCnpj))
            return ValidationResult.Success();

        // Aceita CNPJ numérico tradicional e CNPJ alfanumérico oficial.
        var resultado = CpfCnpjValidatorEx.Validar(FormCnpj);

        if (resultado.IsValid && resultado.Tipo != DocumentoTipo.Cpf)
            return ValidationResult.Success();

        return ValidationResult.Failure("O CNPJ informado é inválido. Confira os dígitos.");
    }

    private ValidationResult ValidarCep()
    {
        if (string.IsNullOrWhiteSpace(FormCep))
            return ValidationResult.Success();

        return CepMaskHelper.EhValido(FormCep)
            ? ValidationResult.Success()
            : ValidationResult.Failure("O CEP deve ter 8 dígitos.");
    }

    private ValidationResult ValidarUf()
    {
        if (string.IsNullOrWhiteSpace(FormUf))
            return ValidationResult.Success();

        var uf = FormUf.Trim().ToUpperInvariant();
        var validas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AC","AL","AP","AM","BA","CE","DF","ES","GO","MA","MT","MS","MG",
            "PA","PB","PR","PE","PI","RJ","RN","RS","RO","RR","SC","SP","SE","TO"
        };

        return validas.Contains(uf)
            ? ValidationResult.Success()
            : ValidationResult.Failure("A UF informada é inválida.");
    }

    /// <summary>Descarta alterações não salvas recarregando do banco.</summary>
    public async Task CancelarAsync()
    {
        await CarregarAsync();
        Report("Alterações descartadas.");
    }

    // ======================= CEP =======================
    //
    // A lupa é a ÚNICA porta de entrada de consulta. Digitar, apagar, alterar, sair do
    // campo ou pressionar ENTER NUNCA consultam — não há handler de LostFocus, TextChanged
    // ou KeyDown ligado a este ViewModel. Idêntico ao comportamento de Clientes/Técnicos.

    private async Task ConsultarCepManualmenteAsync()
    {
        if (_cepService is null)
            return;

        if (string.IsNullOrWhiteSpace(FormCep))
            return;

        var cep = CepMaskHelper.Normalizar(FormCep);

        if (!CepMaskHelper.EstaCompletoParaConsulta(cep))
        {
            EditorMensagem = "Digite um CEP com 8 dígitos para consultar.";
            DefinirErroValidacao(CampoCep, "O CEP deve ter 8 dígitos.");
            return;
        }

        CepCarregando = true;
        EditorMensagem = string.Empty;

        try
        {
            var result = await _cepService.ConsultarAsync(cep);
            AplicarEndereco(result);
        }
        catch (Exception ex)
        {
            EditorMensagem = $"Erro na consulta de CEP: {ex.Message}";
            DefinirErroValidacao(CampoCep, "Não foi possível consultar o CEP. Tente novamente.");
        }
        finally
        {
            CepCarregando = false;
        }
    }

    /// <summary>
    /// Aplica o endereço nas propriedades do formulário (SetField dispara PropertyChanged),
    /// refletindo na UI imediatamente, sem salvar e sem reabrir a tela.
    /// </summary>
    private void AplicarEndereco(CepAddressResult result)
    {
        if (result.Success)
        {
            FormLogradouro = result.Logradouro ?? string.Empty;
            FormBairro = result.Bairro ?? string.Empty;
            FormCidade = result.Cidade ?? string.Empty;
            FormUf = result.Uf ?? string.Empty;

            LimparErroValidacao(CampoCep);
        }
        else
        {
            FormLogradouro = string.Empty;
            FormBairro = string.Empty;
            FormCidade = string.Empty;
            FormUf = string.Empty;

            var mensagem = $"Não foi possível localizar o CEP '{CepMaskHelper.Formatar(result.Cep)}'. Preencha o endereço manualmente.";
            EditorMensagem = mensagem;
            DefinirErroValidacao(CampoCep, mensagem);
        }

        OnPropertyChanged(nameof(CepPodeConsultarManualmente));
    }

    // ======================= Logo =======================

    /// <summary>
    /// Registra a imagem escolhida para a logo. Sem <paramref name="caminhoOrigem"/>, abre o
    /// explorador de arquivos (fluxo da tela); com o caminho, a imagem é apenas registrada e
    /// pré-visualizada — é o que permite testar o fluxo sem abrir diálogo.
    ///
    /// <para>O arquivo só é copiado para a pasta gerenciada pela aplicação ao salvar.</para>
    /// </summary>
    public void SelecionarLogo(string? caminhoOrigem = null)
    {
        var caminho = caminhoOrigem;

        if (caminho is null)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Selecionar logo da empresa",
                Filter = "Imagens (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Todos os arquivos (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
                return;

            caminho = dialog.FileName;
        }

        try
        {
            var preview = CarregarPreview(caminho);
            if (preview is null)
            {
                EditorMensagem = "Não foi possível carregar a imagem selecionada.";
                return;
            }

            NovoLogoCaminhoOrigem = caminho;
            LogoPreview = preview;
            LogoNome = System.IO.Path.GetFileName(caminho);
            RemoverLogo = false;

            EditorMensagem = string.Empty;
        }
        catch (Exception ex)
        {
            EditorMensagem = $"Não foi possível abrir a imagem: {ex.Message}";
        }
    }

    /// <summary>Marca a logo para remoção; o arquivo é apagado de fato apenas ao salvar.</summary>
    public void SolicitarRemoverLogo()
    {
        RemoverLogo = true;
        NovoLogoCaminhoOrigem = null;
        LogoPreview = null;
        LogoNome = string.Empty;
    }

    private async Task CarregarPreviewDaLogoPersistida()
    {
        if (_logoPersistido is null)
        {
            LogoPreview = null;
            return;
        }

        LogoPreview = CarregarPreview(await ObterCaminhoLogoAsync());
    }

    private async Task<string?> ObterCaminhoLogoAsync()
        => await _empresaService.ObterLogoCaminhoAbsolutoAsync();

    /// <summary>Carrega a imagem para pré-visualização; devolve null se não for decodificável.</summary>
    private static ImageSource? CarregarPreview(string? caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho) || !File.Exists(caminho))
            return null;

        try
        {
            using var stream = new FileStream(caminho, FileMode.Open, FileAccess.Read);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }
        catch (NotSupportedException)
        {
            // Arquivo não é uma imagem válida para o WPF.
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}

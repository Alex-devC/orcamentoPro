using System;
using System.Threading.Tasks;
using OrcPro.Application.DTOs.Auth;
using OrcPro.Application.Interfaces.Services;

namespace OrcPro.App.ViewModels;

/// <summary>
/// Módulo "Usuários e Perfis" (Ribbon: Cadastros → Usuários e Perfis). Reúne as duas telas —
/// Usuários e Perfis — em abas, com recarregamento cruzado entre elas.
/// </summary>
public class UsuariosPerfisViewModel : ViewModelBase
{
    private readonly Action<string>? _reportStatus;
    private int _abaSelecionada;

    public UsuariosPerfisViewModel(
        IUsuarioService usuarioService,
        IPerfilService perfilService,
        IPermissaoService permissaoService,
        UsuarioSessaoDto sessao,
        Action<string>? reportStatus = null)
    {
        _reportStatus = reportStatus;

        Usuarios = new UsuariosViewModel(usuarioService, perfilService, sessao, reportStatus);
        Perfis = new PerfisViewModel(perfilService, permissaoService, sessao, reportStatus);

        // Quando um perfil é criado/alterado/excluído, as opções de perfil da aba Usuários
        // são recarregadas para manter o formulário coerente.
        Perfis.PerfisAlterados += async (_, _) => await Usuarios.CarregarPerfisAsync();
    }

    /// <summary>Aba Usuários: cadastro, edição, ativação/inativação e exclusão.</summary>
    public UsuariosViewModel Usuarios { get; }

    /// <summary>Aba Perfis: cadastro, edição, ativação/inativação e exclusão.</summary>
    public PerfisViewModel Perfis { get; }

    /// <summary>0 = aba Usuários, 1 = aba Perfis (usado pelos botões do Ribbon).</summary>
    public int AbaSelecionada
    {
        get => _abaSelecionada;
        set => SetField(ref _abaSelecionada, value);
    }

    public override async Task InitializeAsync()
    {
        await Usuarios.InitializeAsync();
        await Perfis.InitializeAsync();
    }
}
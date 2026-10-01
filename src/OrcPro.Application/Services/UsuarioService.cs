using OrcPro.Application.DTOs.Common;
using OrcPro.Application.DTOs.Usuario;
using OrcPro.Application.Exceptions;
using OrcPro.Application.Interfaces.Repositories;
using OrcPro.Application.Interfaces.Services;
using OrcPro.Domain.Entities.Seguranca;

namespace OrcPro.Application.Services;

public class UsuarioService : IUsuarioService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IPerfilRepository _perfilRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOrcamentoRepository _orcamentoRepository;

    public UsuarioService(
        IUsuarioRepository usuarioRepository,
        IPerfilRepository perfilRepository,
        IPasswordHasher passwordHasher,
        IOrcamentoRepository orcamentoRepository)
    {
        _usuarioRepository = usuarioRepository;
        _perfilRepository = perfilRepository;
        _passwordHasher = passwordHasher;
        _orcamentoRepository = orcamentoRepository;
    }

    public async Task<UsuarioDto> ObterPorIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarioRepository.GetWithPerfilAndPermissoesAsync(id, cancellationToken);
        if (usuario == null)
            throw new NotFoundException("Usuário", id);

        return MapearParaDto(usuario);
    }

    public async Task<UsuarioDto> ObterPorUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarioRepository.GetWithPerfilAndPermissoesByUsernameAsync(username, cancellationToken);
        if (usuario == null)
            throw new NotFoundException($"Usuário '{username}' não foi encontrado.");

        return MapearParaDto(usuario);
    }

    public async Task<PagedResult<UsuarioDto>> ListarPaginadoAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _usuarioRepository.GetPagedAsync(request, cancellationToken);
        var dtos = result.Items.Select(MapearParaDto).ToList();
        return new PagedResult<UsuarioDto>(dtos, result.TotalCount, result.PageNumber, result.PageSize);
    }

    public async Task<UsuarioDto> CriarAsync(CriarUsuarioDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Username))
            throw new ValidationException("O nome de usuário (username) é obrigatório.");

        if (string.IsNullOrWhiteSpace(dto.NomeCompleto))
            throw new ValidationException("O nome completo é obrigatório.");

        if (string.IsNullOrWhiteSpace(dto.Senha))
            throw new ValidationException("A senha inicial é obrigatória.");

        var usernameNormalizado = dto.Username.Trim().ToLowerInvariant();

        if (await _usuarioRepository.ExistsUsernameAsync(usernameNormalizado, null, cancellationToken))
            throw new BusinessException($"O nome de usuário '{dto.Username}' já está em uso.");

        var perfilExiste = await _perfilRepository.ExistsAsync(dto.PerfilId, cancellationToken);
        if (!perfilExiste)
            throw new NotFoundException("Perfil", dto.PerfilId);

        var usuario = new Usuario
        {
            Username = usernameNormalizado,
            PasswordHash = _passwordHasher.HashPassword(dto.Senha),
            NomeCompleto = dto.NomeCompleto.Trim(),
            Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
            PerfilId = dto.PerfilId,
            Ativo = dto.Ativo,
            DataCriacao = DateTime.UtcNow
        };

        var criado = await _usuarioRepository.AddAsync(usuario, cancellationToken);
        return await ObterPorIdAsync(criado.Id, cancellationToken);
    }

    public async Task<UsuarioDto> AtualizarAsync(AtualizarUsuarioDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.NomeCompleto))
            throw new ValidationException("O nome completo é obrigatório.");

        var usuario = await _usuarioRepository.GetByIdAsync(dto.Id, cancellationToken);
        if (usuario == null)
            throw new NotFoundException("Usuário", dto.Id);

        var perfilExiste = await _perfilRepository.ExistsAsync(dto.PerfilId, cancellationToken);
        if (!perfilExiste)
            throw new NotFoundException("Perfil", dto.PerfilId);

        usuario.NomeCompleto = dto.NomeCompleto.Trim();
        usuario.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
        usuario.PerfilId = dto.PerfilId;
        usuario.Ativo = dto.Ativo;
        usuario.DataAtualizacao = DateTime.UtcNow;

        // O login usa o campo "Usuário", que não é alterado na edição (garante unicidade).
        // A senha só é trocada quando o formulário informa uma nova: sempre via IPasswordHasher.
        if (!string.IsNullOrWhiteSpace(dto.NovaSenha))
        {
            usuario.PasswordHash = _passwordHasher.HashPassword(dto.NovaSenha);
        }

        await _usuarioRepository.UpdateAsync(usuario, cancellationToken);
        return await ObterPorIdAsync(usuario.Id, cancellationToken);
    }

    public async Task AlterarSenhaAsync(AlterarSenhaDto dto, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(dto.UsuarioId, cancellationToken);
        if (usuario == null)
            throw new NotFoundException("Usuário", dto.UsuarioId);

        if (string.IsNullOrWhiteSpace(dto.NovaSenha))
            throw new ValidationException("A nova senha não pode ser vazia.");

        if (!_passwordHasher.VerifyPassword(dto.SenhaAtual, usuario.PasswordHash))
            throw new ValidationException("A senha atual informada está incorreta.");

        usuario.PasswordHash = _passwordHasher.HashPassword(dto.NovaSenha);
        usuario.DataAtualizacao = DateTime.UtcNow;

        await _usuarioRepository.UpdateAsync(usuario, cancellationToken);
    }

    public async Task AlterarStatusAtivoAsync(int id, bool ativo, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(id, cancellationToken);
        if (usuario == null)
            throw new NotFoundException("Usuário", id);

        usuario.Ativo = ativo;
        usuario.DataAtualizacao = DateTime.UtcNow;

        await _usuarioRepository.UpdateAsync(usuario, cancellationToken);
    }

    /// <summary>
    /// Exclui definitivamente o usuário. Usuários com orçamentos vinculados não podem ser
    /// excluídos (a recomendação é inativar); o login continua usando o campo Usuário, sem
    /// qualquer alteração nas regras de autenticação.
    /// </summary>
    public async Task ExcluirAsync(int id, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(id, cancellationToken);
        if (usuario == null)
            throw new NotFoundException("Usuário", id);

        var orcamentos = await _orcamentoRepository.CountByUsuarioIdAsync(id, cancellationToken);
        if (orcamentos > 0)
            throw new BusinessException(
                $"O usuário '{usuario.Username}' possui {orcamentos} orçamento(s) vinculado(s) e não pode ser excluído. Inative-o em vez de excluir.");

        await _usuarioRepository.DeleteAsync(id, cancellationToken);
    }

    private static UsuarioDto MapearParaDto(Usuario u)
    {
        return new UsuarioDto
        {
            Id = u.Id,
            Username = u.Username,
            NomeCompleto = u.NomeCompleto,
            Email = u.Email,
            Ativo = u.Ativo,
            UltimoLogin = u.UltimoLogin,
            DataCriacao = u.DataCriacao,
            PerfilId = u.PerfilId,
            PerfilNome = u.Perfil?.Nome ?? "Sem Perfil"
        };
    }
}

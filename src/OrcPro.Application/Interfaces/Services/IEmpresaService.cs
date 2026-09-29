using OrcPro.Application.DTOs.Empresa;

namespace OrcPro.Application.Interfaces.Services;

public interface IEmpresaService
{
    Task<EmpresaDto> ObterEmitentePrincipalAsync(CancellationToken cancellationToken = default);
    Task<EmpresaDto> AtualizarAsync(AtualizarEmpresaDto dto, CancellationToken cancellationToken = default);
}

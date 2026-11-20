using Retail.Application.DTOs.Sistema;

namespace Retail.Application.Interfaces.Infrastructure;

/// <summary>
/// Puerto que responde si esta PC es la que aloja SQL Server (MT-12). Las copias y la restauración escriben y leen
/// archivos en el disco del servidor: desde otra terminal no tienen sentido y se rechazan.
/// </summary>
public interface IEquipoServidor
{
    Task<InformacionServidorDto> ObtenerAsync(CancellationToken cancellationToken = default);
}

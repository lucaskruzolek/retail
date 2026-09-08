using Retail.Application.DTOs.Proveedores;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Contrato para la gestión de proveedores e importación masiva de catálogos mediante streaming.
/// </summary>
public interface IProveedorService
{
    Task<IReadOnlyList<ProveedorDto>> ListarProveedoresAsync(CancellationToken cancellationToken = default);

    Task<ProveedorDto?> ObtenerPorIdAsync(int idProveedor, CancellationToken cancellationToken = default);

    Task<ProveedorDto> CrearProveedorAsync(CrearProveedorDto dto, CancellationToken cancellationToken = default);

    Task ActualizarProveedorAsync(ProveedorDto dto, CancellationToken cancellationToken = default);

    Task BajaProveedorAsync(int idProveedor, CancellationToken cancellationToken = default);

    Task<ResultadoImportacionDto> ImportarPlanillaProveedorAsync(
        Stream archivoStream,
        MapeoColumnasDto mapeo,
        IProgress<int>? progreso = null,
        CancellationToken cancellationToken = default);
}

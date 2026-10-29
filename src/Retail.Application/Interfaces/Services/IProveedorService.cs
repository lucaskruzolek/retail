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

    Task ActualizarProveedorAsync(ActualizarProveedorDto dto, CancellationToken cancellationToken = default);

    Task BajaProveedorAsync(int idProveedor, CancellationToken cancellationToken = default);

    Task<ResultadoImportacionDto> ImportarPlanillaProveedorAsync(
        Stream archivoStream,
        MapeoColumnasDto mapeo,
        IProgress<int>? progreso = null,
        CancellationToken cancellationToken = default);

    Task<CatalogoPaginadoDto> ListarItemsCatalogoAsync(ConsultaCatalogoProveedorDto consulta, CancellationToken cancellationToken = default);

    Task<ResultadoIncorporacionDto> IncorporarArticulosATiendaAsync(IncorporarCatalogoArticulosDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra el ítem como fuente del artículo (Track 4.3, J4). Con <paramref name="comoPrincipal"/>, además lo hace
    /// principal y toma su costo; la primera fuente de un artículo es principal aunque no se pida.
    /// </summary>
    Task VincularArticuloACatalogoAsync(
        int idArticulo,
        int idCatalogoProveedor,
        bool comoPrincipal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Quita el ítem de las fuentes de su artículo (J5). Si era la principal, el artículo queda sin principal.
    /// </summary>
    Task DesvincularFuenteAsync(int idCatalogoProveedor, CancellationToken cancellationToken = default);
}

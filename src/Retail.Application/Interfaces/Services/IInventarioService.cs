using Retail.Application.DTOs.Articulos;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Contrato para la administración del catálogo de artículos, stock, cálculo de markup y alertas de inventario.
/// </summary>
public interface IInventarioService
{
    Task<IReadOnlyList<ArticuloDto>> ListarArticulosAsync(CancellationToken cancellationToken = default);

    Task<ArticulosPaginadosDto> ListarArticulosPaginadosAsync(ConsultaArticulosDto consulta, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ArticuloVentaDto>> BuscarArticulosParaVentaAsync(string terminoBusqueda, CancellationToken cancellationToken = default);

    Task<ArticuloDto?> ObtenerPorIdAsync(int idArticulo, CancellationToken cancellationToken = default);

    Task<ArticuloDto?> ObtenerPorCodigoBarrasAsync(string codigoBarras, CancellationToken cancellationToken = default);

    Task<ArticuloDto> CrearArticuloAsync(CrearArticuloDto dto, CancellationToken cancellationToken = default);

    Task<ArticuloDto> ActualizarArticuloAsync(ActualizarArticuloDto dto, CancellationToken cancellationToken = default);

    Task BajaArticuloAsync(int idArticulo, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertaStockDto>> ObtenerAlertasStockMinimoAsync(CancellationToken cancellationToken = default);

    Task ActualizarCostoYPrecioAsync(int idArticulo, decimal nuevoCostoReposicion, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MarcaDto>> ListarMarcasAsync(CancellationToken cancellationToken = default);
}

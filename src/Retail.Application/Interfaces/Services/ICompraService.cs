using Retail.Application.DTOs.Compras;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Contrato para el registro de compras a proveedores con recálculo automático de precios por markup (RF-19).
/// </summary>
public interface ICompraService
{
    Task<CompraResponseDto> RegistrarCompraAsync(CrearCompraDto dto, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CompraResponseDto>> ListarComprasAsync(CancellationToken cancellationToken = default);

    Task<CompraResponseDto?> ObtenerCompraPorIdAsync(int idCompra, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hasta <paramref name="limite"/> artículos comprables del proveedor indicado (los vinculados a su catálogo) cuya
    /// descripción o código de barras contienen el término. Un término vacío devuelve una lista vacía.
    /// </summary>
    Task<IReadOnlyList<ArticuloParaCompraDto>> BuscarArticulosAsync(string termino, int idProveedor, int limite = 15, CancellationToken cancellationToken = default);
}

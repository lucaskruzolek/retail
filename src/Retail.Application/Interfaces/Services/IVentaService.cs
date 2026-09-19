using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Ventas;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Contrato para el Punto de Venta (POS), orquestación de cobros multimedio y descuento atómico de stock.
/// </summary>
public interface IVentaService
{
    Task<ArticuloVentaDto?> BuscarArticuloParaVentaAsync(string codigoOBusqueda, CancellationToken cancellationToken = default);

    Task<ArticuloVentaDto?> BuscarPorCodigoBarrasAsync(string codigoBarras, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ArticuloVentaDto>> BuscarPorTextoAsync(string termino, int limite = 15, CancellationToken cancellationToken = default);

    Task<VentaResponseDto> RegistrarVentaAsync(CrearVentaDto dto, CancellationToken cancellationToken = default);

    Task<VentaResponseDto?> ObtenerVentaPorIdAsync(int idVenta, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VentaResponseDto>> ListarVentasTurnoAsync(int idTurno, CancellationToken cancellationToken = default);
}

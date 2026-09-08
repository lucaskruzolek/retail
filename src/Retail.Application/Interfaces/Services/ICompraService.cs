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
}

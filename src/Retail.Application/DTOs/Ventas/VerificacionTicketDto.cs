namespace Retail.Application.DTOs.Ventas;

/// <summary>
/// Resultado de verificar el ticket contra el catálogo antes de abrir el cobro. Es una ayuda para el cajero:
/// <c>RegistrarVentaAsync</c> vuelve a validar todo al confirmar, porque otra terminal puede vender en el medio.
/// </summary>
public record class VerificacionTicketDto
{
    public required IReadOnlyList<PrecioActualizadoDto> PreciosActualizados { get; init; }
    public required IReadOnlyList<FaltanteStockDto> Faltantes { get; init; }

    /// <summary>
    /// Descripciones de los artículos del ticket que fueron dados de baja.
    /// </summary>
    public required IReadOnlyList<string> ArticulosNoDisponibles { get; init; }
}

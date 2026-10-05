namespace Retail.Application.DTOs.Proveedores;

/// <summary>
/// Resumen de la incorporación de ítems de catálogo de proveedor a la tienda (RF-05).
/// </summary>
public record class ResultadoIncorporacionDto
{
    public required int Incorporados { get; init; }

    /// <summary>Ítems que no se incorporaron porque ya tenían un artículo vinculado en la tienda.</summary>
    public required int OmitidosYaVinculados { get; init; }
}

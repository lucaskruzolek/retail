namespace Retail.Application.DTOs.Articulos;

/// <summary>
/// Una de las fuentes de un artículo, tal como la lista la sección Fuentes del formulario (Track 4.3, A-19, RF-28): con
/// qué proveedor, a qué costo y con qué fecha de lista se lo puede comprar, y si es la principal. Proyección de solo
/// lectura (Ley 8).
/// </summary>
public record class FuenteArticuloDto
{
    public int IdCatalogoProveedor { get; init; }

    public int IdProveedor { get; init; }

    public string RazonSocialProveedor { get; init; } = string.Empty;

    public string CodigoProveedor { get; init; } = string.Empty;

    public string Descripcion { get; init; } = string.Empty;

    public decimal CostoActual { get; init; }

    /// <summary>
    /// Fecha (UTC) de la última planilla que actualizó el ítem: una lista vieja y barata es la trampa que motivó A-2.
    /// </summary>
    public DateTime FechaActualizacion { get; init; }

    public bool EsPrincipal { get; init; }
}

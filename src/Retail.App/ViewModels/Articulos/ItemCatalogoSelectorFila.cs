using Retail.Application.DTOs.Proveedores;

namespace Retail.App.ViewModels.Articulos;

/// <summary>
/// Una fila del selector de fuentes (Track 4.3, A-19): el ítem del catálogo más si se puede elegir y por qué no.
/// Un ítem de otro artículo no es elegible (A-1). La fecha de la lista llega en UTC y se muestra en hora local.
/// </summary>
public sealed class ItemCatalogoSelectorFila
{
    public ItemCatalogoSelectorFila(CatalogoProveedorDto item, bool esSeleccionable, TimeProvider reloj)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(reloj);

        Item = item;
        EsSeleccionable = esSeleccionable;
        TextoEstado = esSeleccionable
            ? "Disponible"
            : string.IsNullOrWhiteSpace(item.NombreArticuloTienda)
                ? "Fuente de otro artículo"
                : $"Fuente de: {item.NombreArticuloTienda}";
        FechaLocal = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(item.FechaActualizacion, DateTimeKind.Utc),
            reloj.LocalTimeZone);
    }

    public CatalogoProveedorDto Item { get; }

    public bool EsSeleccionable { get; }

    public string TextoEstado { get; }

    public DateTime FechaLocal { get; }
}

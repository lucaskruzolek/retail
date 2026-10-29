using CommunityToolkit.Mvvm.ComponentModel;
using Retail.App.ViewModels.Proveedores;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Proveedores;

namespace Retail.App.ViewModels.Articulos;

/// <summary>
/// Una fuente en la sección Fuentes del formulario de artículo (Track 4.3, A-19, RF-28): proveedor, código, costo y
/// fecha de la lista. Puede venir de la base o recién agregada con el selector; nada se guarda hasta Guardar. La fecha
/// llega en UTC y se muestra en la hora local del reloj, que también decide la antigüedad de la lista.
/// </summary>
public partial class FuenteFormularioFila : ObservableObject
{
    private FuenteFormularioFila(
        int idCatalogoProveedor,
        string razonSocialProveedor,
        string codigoProveedor,
        string descripcion,
        decimal costoActual,
        DateTime fechaActualizacionUtc,
        TimeProvider reloj)
    {
        IdCatalogoProveedor = idCatalogoProveedor;
        RazonSocialProveedor = razonSocialProveedor;
        CodigoProveedor = codigoProveedor;
        Descripcion = descripcion;
        CostoActual = costoActual;
        FechaLocal = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(fechaActualizacionUtc, DateTimeKind.Utc),
            reloj.LocalTimeZone);
        var hoyLocal = reloj.GetLocalNow().DateTime;
        ListaDesactualizada = (hoyLocal.Date - FechaLocal.Date).Days > ImportadorCatalogosViewModel.DiasListaDesactualizada;
    }

    public int IdCatalogoProveedor { get; }

    public string RazonSocialProveedor { get; }

    public string CodigoProveedor { get; }

    public string Descripcion { get; }

    public decimal CostoActual { get; }

    public DateTime FechaLocal { get; }

    /// <summary>
    /// La lista tiene más días que el umbral del importador: un costo bajo puede no estar vigente (A-2).
    /// </summary>
    public bool ListaDesactualizada { get; }

    public string AvisoListaDesactualizada { get; } =
        $"La lista tiene más de {ImportadorCatalogosViewModel.DiasListaDesactualizada} días: su costo puede no estar vigente.";

    [ObservableProperty]
    private bool _esPrincipal;

    public static FuenteFormularioFila DesdeFuente(FuenteArticuloDto fuente, TimeProvider reloj)
    {
        ArgumentNullException.ThrowIfNull(fuente);
        ArgumentNullException.ThrowIfNull(reloj);

        return new FuenteFormularioFila(
            fuente.IdCatalogoProveedor,
            fuente.RazonSocialProveedor,
            fuente.CodigoProveedor,
            fuente.Descripcion,
            fuente.CostoActual,
            fuente.FechaActualizacion,
            reloj)
        {
            EsPrincipal = fuente.EsPrincipal
        };
    }

    public static FuenteFormularioFila DesdeItemDeCatalogo(CatalogoProveedorDto item, TimeProvider reloj)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(reloj);

        return new FuenteFormularioFila(
            item.Id,
            item.ProveedorRazonSocial ?? string.Empty,
            item.CodigoProveedor,
            item.DescripcionProveedor,
            item.CostoReposicion,
            item.FechaActualizacion,
            reloj);
    }
}

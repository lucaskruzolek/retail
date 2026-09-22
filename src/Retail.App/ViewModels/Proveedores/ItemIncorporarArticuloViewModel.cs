using CommunityToolkit.Mvvm.ComponentModel;
using Retail.Domain.Entities;

namespace Retail.App.ViewModels.Proveedores;

/// <summary>
/// Modelo de fila para la grilla del modal de incorporación de artículos a tienda.
/// Contiene datos del ítem mayorista, margen de ganancia individual editable y recálculo reactivo del precio de venta final.
/// </summary>
public partial class ItemIncorporarArticuloViewModel : ObservableObject
{
    public int IdCatalogo { get; }
    public string CodigoProveedor { get; }
    public string? CodigoBarras { get; }
    public string Descripcion { get; }
    public decimal CostoReposicion { get; }

    [ObservableProperty]
    private decimal _porcentajeGanancia;

    [ObservableProperty]
    private decimal _precioVenta;

    public ItemIncorporarArticuloViewModel(
        int idCatalogo,
        string codigoProveedor,
        string? codigoBarras,
        string descripcion,
        decimal costoReposicion,
        decimal porcentajeGananciaInicial)
    {
        IdCatalogo = idCatalogo;
        CodigoProveedor = codigoProveedor;
        CodigoBarras = codigoBarras;
        Descripcion = descripcion;
        CostoReposicion = costoReposicion;
        _porcentajeGanancia = porcentajeGananciaInicial;
        _precioVenta = Articulo.CalcularPrecioVenta(costoReposicion, porcentajeGananciaInicial);
    }

    partial void OnPorcentajeGananciaChanged(decimal value)
    {
        PrecioVenta = Articulo.CalcularPrecioVenta(CostoReposicion, value);
    }
}

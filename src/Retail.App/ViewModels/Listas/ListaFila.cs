using Retail.App.ViewModels.Presupuestos;
using Retail.Application.DTOs.Listas;

namespace Retail.App.ViewModels.Listas;

/// <summary>
/// Fila de la grilla de listas. Agrega al <see cref="ListaResumenDto"/> lo que es solo de presentación: la fecha de
/// última modificación en hora local.
/// </summary>
public sealed class ListaFila
{
    public ListaFila(ListaResumenDto resumen, TimeProvider reloj)
    {
        Resumen = resumen ?? throw new ArgumentNullException(nameof(resumen));
        ArgumentNullException.ThrowIfNull(reloj);

        ModificadaLocal = PresentacionPresupuesto.ALocal(resumen.FechaModificacion, reloj);
    }

    public ListaResumenDto Resumen { get; }

    public int IdLista => Resumen.IdLista;

    public string Nombre => Resumen.Nombre;

    /// <summary>Renglones cuyo artículo sigue activo.</summary>
    public int CantidadArticulos => Resumen.CantidadArticulos;

    /// <summary>Total a los precios de catálogo de hoy (informativo: la lista no guarda precios).</summary>
    public decimal TotalDeHoy => Resumen.TotalEstimado;

    /// <summary>Ventas activas que se originaron en esta lista.</summary>
    public int CantidadVentas => Resumen.CantidadVentas;

    public DateTime ModificadaLocal { get; }
}

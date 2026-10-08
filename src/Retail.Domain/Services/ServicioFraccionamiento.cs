using Retail.Domain.Entities;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Services;

/// <summary>
/// Servicio de dominio que fracciona un artículo de origen en una de sus presentaciones derivadas (RF-21).
/// Coordina dos raíces de agregado, por eso no pertenece a ninguna de ellas.
/// </summary>
public static class ServicioFraccionamiento
{
    /// <summary>
    /// Resta <paramref name="cantidadOrigen"/> del stock del origen y suma las unidades equivalentes al derivado.
    /// Si alguna validación falla, ninguno de los dos artículos se modifica.
    /// </summary>
    /// <returns>Las unidades que recibió la presentación derivada.</returns>
    public static int Fraccionar(Articulo origen, Articulo derivado, int cantidadOrigen)
    {
        ArgumentNullException.ThrowIfNull(origen);
        ArgumentNullException.ThrowIfNull(derivado);

        if (cantidadOrigen < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(cantidadOrigen), "La cantidad a fraccionar debe ser al menos 1.");
        }

        if (derivado.IdArticuloOrigen != origen.Id || derivado.UnidadesPorOrigen is not int unidadesPorOrigen)
        {
            throw new DomainException($"El artículo {derivado.Id} no es una presentación derivada del artículo {origen.Id}.");
        }

        // Se calcula antes de tocar el stock: un desborde no puede dejar el origen descontado y el derivado sin incrementar.
        var unidadesObtenidas = checked(cantidadOrigen * unidadesPorOrigen);
        _ = checked(derivado.StockActual + unidadesObtenidas);

        origen.DescontarStock(cantidadOrigen);
        derivado.IncrementarStock(unidadesObtenidas);

        return unidadesObtenidas;
    }
}

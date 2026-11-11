using Retail.Domain.Enums;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Parámetros comerciales globales del negocio (D-38): hoy, la política de redondeo del precio de venta calculado con el
/// markup. Los comparten todas las terminales porque viven en la base.
/// </summary>
public interface IConfiguracionComercialService
{
    /// <summary>
    /// Política de redondeo vigente. La consulta cualquier operador: la usan las vistas previas de precio de los
    /// formularios de artículos, compras e importación. Si la fila de configuración no existe, rige el valor
    /// predeterminado (<see cref="RedondeoPrecioEnum.EnteroMasCercano"/>).
    /// </summary>
    Task<RedondeoPrecioEnum> ObtenerRedondeoPrecioAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cambia la política de redondeo. Solo un Gerente. Rige para los próximos recálculos de precio; no reescribe los
    /// precios existentes.
    /// </summary>
    /// <exception cref="Domain.Exceptions.OperacionNoAutorizadaException">El actor no existe, está dado de baja o no es Gerente.</exception>
    /// <exception cref="Domain.Exceptions.DomainException">La política no es válida.</exception>
    Task CambiarRedondeoPrecioAsync(int idActor, RedondeoPrecioEnum redondeo, CancellationToken cancellationToken = default);
}

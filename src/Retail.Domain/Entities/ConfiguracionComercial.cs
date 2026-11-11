using Retail.Domain.Common;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Entities;

/// <summary>
/// Parámetros comerciales globales del negocio, compartidos por todas las terminales (D-38). Hay una sola fila: la
/// siembra la migración que crea la tabla, como los roles. Vive en la base y no en <c>appsettings.json</c> porque es
/// una regla del comercio y no de un equipo: dos terminales con políticas distintas pondrían precios distintos al mismo
/// artículo.
/// </summary>
public class ConfiguracionComercial : BaseEntity, IAggregateRoot
{
    /// <summary>Identificador de la única fila de configuración (la siembra la migración).</summary>
    public const int IdUnico = 1;

    /// <summary>
    /// Política de redondeo del precio de venta calculado con el markup. Un cambio rige para los próximos recálculos
    /// (alta o edición de un artículo, una compra o una importación que cambie su costo); no reescribe los precios
    /// existentes, porque una modificación masiva de precios debe ser un acto explícito.
    /// </summary>
    public RedondeoPrecioEnum RedondeoPrecio { get; private set; } = RedondeoPrecioEnum.EnteroMasCercano;

    /// <summary>Cambia la política de redondeo del precio de venta.</summary>
    /// <exception cref="DomainException">El valor no es una de las políticas definidas.</exception>
    public void CambiarRedondeoPrecio(RedondeoPrecioEnum redondeo)
    {
        if (!Enum.IsDefined(redondeo))
        {
            throw new DomainException("La política de redondeo de precios no es válida.");
        }

        RedondeoPrecio = redondeo;
    }
}

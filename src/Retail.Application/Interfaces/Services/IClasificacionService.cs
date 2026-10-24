using Retail.Application.DTOs.Clasificacion;
using Retail.Domain.Exceptions;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Motor de sugerencia de categoría y marca al incorporar artículos del catálogo de un proveedor (Track 6.6, RF-27).
/// Recorre los niveles de la sección 4 en el orden de M-14: lo que informó el proveedor, los diccionarios y el modelo.
/// Las sugerencias se calculan al momento y no se guardan (M-4); ninguna se aplica sin que una persona la confirme.
/// </summary>
public interface IClasificacionService
{
    /// <summary>
    /// Sugiere categoría y marca para cada ítem del lote, agrupa los términos nuevos frecuentes para decidirlos en
    /// bloque (M-26) e informa los valores del proveedor sin equivalencia (M-13). Lanza
    /// <see cref="FluentValidation.ValidationException"/> si la solicitud es inválida y <see cref="DomainException"/>
    /// si algún ítem no existe o pertenece a otro proveedor.
    /// </summary>
    Task<SugerenciasClasificacionDto> SugerirAsync(SugerirClasificacionDto dto, CancellationToken cancellationToken = default);
}

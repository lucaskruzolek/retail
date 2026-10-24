using Retail.Application.DTOs.Clasificacion;

namespace Retail.Application.Interfaces.Persistence;

/// <summary>
/// Puerto de lectura de los diccionarios del motor de catalogación (Track 6.6, sección 9). Lo implementa Infrastructure
/// con proyecciones <c>AsNoTracking</c>: términos y prefijos se filtran en SQL Server por los valores del lote
/// (<c>Contains</c> → <c>OPENJSON</c>, Ley 8); las marcas se leen completas porque buscar una frase dentro de la
/// descripción no se traduce a SQL (son unos cientos de filas).
/// </summary>
public interface IDiccionariosClasificacionQueryService
{
    /// <param name="idProveedor">Proveedor del lote: se leen solo sus equivalencias.</param>
    /// <param name="terminos">Primeras palabras del lote en forma de comparación (<c>ReglasClasificacion.TerminoDeDescripcion</c>).</param>
    /// <param name="prefijos">Prefijos del titular de los códigos de barras válidos del lote.</param>
    Task<DiccionariosClasificacionDto> ObtenerParaLoteAsync(
        int idProveedor,
        IReadOnlyCollection<string> terminos,
        IReadOnlyCollection<string> prefijos,
        CancellationToken cancellationToken = default);
}

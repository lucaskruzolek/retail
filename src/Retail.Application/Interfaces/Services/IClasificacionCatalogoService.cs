using Retail.Application.DTOs.Clasificacion;
using Retail.Domain.Exceptions;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Clasificación del catálogo de un proveedor con las reglas vigentes del comercio (Track 6.6, Fase 8). Guarda en cada
/// ítem la categoría y la marca sugeridas para poder filtrar el explorador sin incorporar los artículos (M-72). Es
/// pasiva (M-73): no arma bloques de términos nuevos ni pide revisión por ítem, y la sugerencia nunca llega sola al
/// artículo. Lo que escribe es un dato derivado y descartable, por eso no requiere un rol especial.
/// </summary>
public interface IClasificacionCatalogoService
{
    /// <summary>
    /// Rubros, categorías y marcas del proveedor que todavía no tienen equivalencia (M-76), uno por valor distinto y
    /// con la cantidad de ítems que lo traen. Una marca que coincide con una del comercio no se pide traducir.
    /// </summary>
    Task<IReadOnlyList<ValorSinTraducirDto>> ObtenerValoresSinTraducirAsync(int idProveedor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clasifica los ítems vigentes del proveedor y guarda el resultado en una sola unidad de trabajo. Con
    /// <paramref name="soloPendientes"/> procesa los ítems sin clasificar (importación, M-75); sin él reclasifica todos
    /// con las reglas vigentes y activa la preferencia del proveedor (botón "Clasificar catálogo"). Lanza
    /// <see cref="DomainException"/> si el proveedor no existe.
    /// </summary>
    Task<ResultadoClasificacionCatalogoDto> ClasificarAsync(int idProveedor, bool soloPendientes, CancellationToken cancellationToken = default);
}

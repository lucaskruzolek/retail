namespace Retail.Domain.Enums;

/// <summary>
/// Qué traduce una equivalencia de clasificación de un proveedor (Track 6.6, M-13): su rubro y categoría, o su marca.
/// </summary>
public enum TipoEquivalenciaEnum
{
    /// <summary>
    /// Rubro y categoría del proveedor → una categoría del comercio.
    /// </summary>
    Categoria = 1,

    /// <summary>
    /// Marca escrita por el proveedor → una marca del comercio.
    /// </summary>
    Marca = 2
}

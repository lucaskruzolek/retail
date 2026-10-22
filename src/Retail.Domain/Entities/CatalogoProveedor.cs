using Retail.Domain.Common;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Entities;

/// <summary>
/// Catálogo de precios y códigos de distribución importados desde planillas de proveedores.
/// </summary>
public class CatalogoProveedor : BaseEntity
{
    public int IdProveedor { get; set; }

    public Proveedor? Proveedor { get; set; }

    public string CodigoProveedor { get; set; } = string.Empty;

    public string? CodigoBarras { get; set; }

    public string DescripcionProveedor { get; set; } = string.Empty;

    public decimal CostoReposicion { get; set; }

    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Rubro con que el proveedor clasifica el ítem en su planilla, tal como lo escribe (Track 6.6, M-12). Es un
    /// dato para mostrar y la entrada de sus equivalencias; nunca la categoría del artículo.
    /// </summary>
    public string? RubroProveedor { get; private set; }

    /// <summary>
    /// Categoría o familia con que el proveedor clasifica el ítem, tal como la escribe (M-12).
    /// </summary>
    public string? CategoriaProveedor { get; private set; }

    /// <summary>
    /// Marca que el proveedor informa en una columna propia, tal como la escribe (M-12).
    /// </summary>
    public string? MarcaProveedor { get; private set; }

    public ICollection<Articulo> Articulos { get; set; } = new List<Articulo>();

    /// <summary>
    /// Actualiza el ítem con una fila de la planilla. Un texto vacío conserva el valor anterior: una planilla que no
    /// trae esa columna no borra lo que trajo otra. Todo se valida antes de asignar, para que una fila con error no
    /// quede a medio actualizar (atomicidad por fila del importador, Ley 9 de persistencia).
    /// </summary>
    public void ActualizarPrecio(
        decimal nuevoCosto,
        string? descripcion = null,
        string? codigoBarras = null,
        string? rubroProveedor = null,
        string? categoriaProveedor = null,
        string? marcaProveedor = null)
    {
        if (nuevoCosto <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(nuevoCosto), "El costo de reposición debe ser estrictamente positivo.");
        }

        var rubro = NormalizarTextoDeClasificacion(rubroProveedor, "rubro del proveedor");
        var categoria = NormalizarTextoDeClasificacion(categoriaProveedor, "categoría del proveedor");
        var marca = NormalizarTextoDeClasificacion(marcaProveedor, "marca del proveedor");

        RubroProveedor = rubro ?? RubroProveedor;
        CategoriaProveedor = categoria ?? CategoriaProveedor;
        MarcaProveedor = marca ?? MarcaProveedor;
        CostoReposicion = nuevoCosto;
        FechaActualizacion = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(descripcion))
        {
            DescripcionProveedor = descripcion.Trim();
        }

        if (!string.IsNullOrWhiteSpace(codigoBarras))
        {
            CodigoBarras = codigoBarras.Trim();
        }
    }

    // Se guarda como lo escribe el proveedor (solo espacios normalizados): la forma de comparación vive en la
    // equivalencia (M-31). Nulo = la planilla no trajo el dato.
    private static string? NormalizarTextoDeClasificacion(string? valor, string nombreCampo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var normalizado = ReglasTexto.NormalizarEspacios(valor);
        if (normalizado.Length > ReglasClasificacion.LongitudMaximaTextoDeProveedor)
        {
            throw new DomainException(
                $"El {nombreCampo} '{normalizado}' supera los {ReglasClasificacion.LongitudMaximaTextoDeProveedor} caracteres.");
        }

        return normalizado;
    }
}

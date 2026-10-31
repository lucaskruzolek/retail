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

    /// <summary>
    /// Artículos que tienen este ítem como fuente principal (A-16). El índice único filtrado de ARTICULOS admite a lo
    /// sumo uno activo.
    /// </summary>
    public ICollection<Articulo> Articulos { get; set; } = new List<Articulo>();

    /// <summary>
    /// Artículo del que este ítem es fuente, principal o adicional (Track 4.3, A-1 y A-16). Nulo si el ítem no está
    /// vinculado. Solo cambia con <see cref="AsignarArticulo"/> y <see cref="LiberarArticulo"/>.
    /// </summary>
    public int? IdArticulo { get; private set; }

    public Articulo? Articulo { get; private set; }

    public bool EstaVinculado => IdArticulo.HasValue || Articulo is not null;

    /// <summary>
    /// Indica si este ítem es fuente del artículo. Compara también la instancia: un artículo recién incorporado no
    /// tiene id hasta el SaveChanges.
    /// </summary>
    public bool EsFuenteDe(Articulo articulo)
    {
        ArgumentNullException.ThrowIfNull(articulo);

        return ReferenceEquals(Articulo, articulo) || (articulo.Id > 0 && IdArticulo == articulo.Id);
    }

    /// <summary>
    /// Registra este ítem como fuente del artículo (J1). Un ítem es fuente de un solo artículo (A-1) y nunca de una
    /// presentación derivada (RF-21). Volver a asignarlo al mismo artículo no cambia nada. La navegación se asigna
    /// junto con el id para que EF Core complete la FK de un artículo nuevo, por eso los dos deben pertenecer a la
    /// misma unidad de trabajo.
    /// </summary>
    public void AsignarArticulo(Articulo articulo)
    {
        ArgumentNullException.ThrowIfNull(articulo);

        if (articulo.EsDerivado)
        {
            throw new DomainException("Una presentación derivada no puede vincularse a un catálogo de proveedor: solo se vincula su artículo de origen.");
        }

        if (EsFuenteDe(articulo))
        {
            return;
        }

        if (EstaVinculado)
        {
            throw new DomainException(
                $"El ítem '{CodigoProveedor}' ya es fuente de otro artículo. Desvincúlelo antes de vincularlo a otro.");
        }

        Articulo = articulo;
        IdArticulo = articulo.Id > 0 ? articulo.Id : null;
    }

    /// <summary>
    /// Deja el ítem sin artículo (J5). Si era la fuente principal, Application también se la quita al artículo.
    /// </summary>
    public void LiberarArticulo()
    {
        IdArticulo = null;
        Articulo = null;
    }

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

        var textos = NormalizarTextosDeClasificacion(rubroProveedor, categoriaProveedor, marcaProveedor);

        AsignarTextosDeClasificacion(textos);
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

    /// <summary>
    /// Registra el rubro, la categoría y la marca que trae la planilla para un ítem nuevo, que todavía no pasa por
    /// <see cref="ActualizarPrecio"/> (Track 6.6, M-12). Un texto vacío conserva el valor anterior; los tres se validan
    /// antes de asignar alguno.
    /// </summary>
    public void ActualizarClasificacionDelProveedor(string? rubroProveedor, string? categoriaProveedor, string? marcaProveedor)
    {
        AsignarTextosDeClasificacion(NormalizarTextosDeClasificacion(rubroProveedor, categoriaProveedor, marcaProveedor));
    }

    private static (string? Rubro, string? Categoria, string? Marca) NormalizarTextosDeClasificacion(
        string? rubroProveedor,
        string? categoriaProveedor,
        string? marcaProveedor)
    {
        return (
            NormalizarTextoDeClasificacion(rubroProveedor, "rubro del proveedor"),
            NormalizarTextoDeClasificacion(categoriaProveedor, "categoría del proveedor"),
            NormalizarTextoDeClasificacion(marcaProveedor, "marca del proveedor"));
    }

    private void AsignarTextosDeClasificacion((string? Rubro, string? Categoria, string? Marca) textos)
    {
        RubroProveedor = textos.Rubro ?? RubroProveedor;
        CategoriaProveedor = textos.Categoria ?? CategoriaProveedor;
        MarcaProveedor = textos.Marca ?? MarcaProveedor;
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

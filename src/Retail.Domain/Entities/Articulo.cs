using Retail.Domain.Common;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Entities;

/// <summary>
/// Raíz de Agregado que representa a un artículo comercial, producto artesanal o servicio en el catálogo.
/// Un artículo puede ser una presentación derivada de otro (RF-21): por ejemplo, la unidad suelta de un pack.
/// </summary>
public class Articulo : BaseEntity, IAggregateRoot
{
    public string? CodigoBarras { get; set; }

    public string Descripcion { get; set; } = string.Empty;

    /// <summary>
    /// Solo cambia con <see cref="Clasificar"/> o <see cref="ActualizarDatos"/>, que registran el origen (M-30).
    /// </summary>
    public int? IdCategoria { get; private set; }

    public Categoria? Categoria { get; set; }

    /// <summary>
    /// Solo cambia con <see cref="Clasificar"/> o <see cref="ActualizarDatos"/>, que registran el origen (M-30).
    /// </summary>
    public int? IdMarca { get; private set; }

    public Marca? Marca { get; set; }

    /// <summary>
    /// Cómo se asignó la categoría (Track 6.6, sección 8.2). Tiene valor si y solo si hay categoría.
    /// </summary>
    public OrigenClasificacionEnum? OrigenCategoria { get; private set; }

    /// <summary>
    /// Cómo se asignó la marca. Con <see cref="IdMarca"/> nulo, un origen indica "Sin marca" confirmada por una
    /// persona (Track 6.5, D-18); sin origen, el artículo todavía no tiene la marca clasificada.
    /// </summary>
    public OrigenClasificacionEnum? OrigenMarca { get; private set; }

    /// <summary>
    /// Fuente principal (Track 4.3, A-16): el único ítem de catálogo cuya planilla fija el costo y el precio (A-2,
    /// RF-07). Siempre es una de las fuentes del artículo: solo cambia con <see cref="MarcarFuentePrincipal"/> y
    /// <see cref="QuitarFuentePrincipal"/>.
    /// </summary>
    public int? IdCatalogoProveedor { get; private set; }

    public CatalogoProveedor? CatalogoProveedor { get; set; }

    /// <summary>
    /// Artículo de compra del que deriva esta presentación (RF-21). Nulo si no es un derivado.
    /// </summary>
    public int? IdArticuloOrigen { get; set; }

    public Articulo? ArticuloOrigen { get; set; }

    /// <summary>
    /// Unidades de esta presentación que contiene cada unidad del origen (entero mayor o igual a 1).
    /// </summary>
    public int? UnidadesPorOrigen { get; set; }

    public ICollection<Articulo> Presentaciones { get; set; } = new List<Articulo>();

    public decimal CostoReposicion { get; set; }

    public decimal PorcentajeGanancia { get; set; }

    public decimal PrecioVenta { get; set; }

    public int StockActual { get; set; }

    public int StockMinimo { get; set; }

    public bool EsServicio { get; set; }

    public ICollection<DetalleVenta> DetallesVentas { get; set; } = new List<DetalleVenta>();

    public ICollection<DetallePresupuesto> DetallesPresupuestos { get; set; } = new List<DetallePresupuesto>();

    public ICollection<DetalleCompra> DetallesCompras { get; set; } = new List<DetalleCompra>();

    public bool TieneStockBajo => !EsServicio && StockActual <= StockMinimo;

    public bool EsDerivado => IdArticuloOrigen.HasValue;

    public static decimal CalcularPrecioVenta(decimal costoReposicion, decimal porcentajeGanancia)
    {
        if (costoReposicion < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(costoReposicion), "El costo de reposición no puede ser negativo.");
        }

        if (porcentajeGanancia < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(porcentajeGanancia), "El porcentaje de ganancia no puede ser negativo.");
        }

        return RedondearMoneda(costoReposicion * (1m + (porcentajeGanancia / 100m)));
    }

    /// <summary>
    /// Costo de reposición de una presentación derivada: costo del origen dividido por las unidades que contiene (RF-21).
    /// </summary>
    public static decimal CalcularCostoPresentacion(decimal costoOrigen, int unidadesPorOrigen)
    {
        if (costoOrigen < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(costoOrigen), "El costo del artículo de origen no puede ser negativo.");
        }

        if (unidadesPorOrigen < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(unidadesPorOrigen), "Las unidades por origen deben ser al menos 1.");
        }

        return RedondearMoneda(costoOrigen / unidadesPorOrigen);
    }

    public void ActualizarCostoYRecalcularPrecio(decimal nuevoCosto)
    {
        // Se calcula antes de asignar: si el cálculo falla, el artículo no queda con un costo nuevo y un precio viejo (H-08).
        var nuevoPrecio = CalcularPrecioVenta(nuevoCosto, PorcentajeGanancia);

        CostoReposicion = nuevoCosto;
        PrecioVenta = nuevoPrecio;
    }

    public void ActualizarDatos(
        string descripcion,
        int? idCategoria,
        int? idMarca,
        string? codigoBarras,
        decimal costoReposicion,
        decimal porcentajeGanancia,
        int stockActual,
        int stockMinimo,
        bool esServicio)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(descripcion);
        ValidarIdentificadoresDeClasificacion(idCategoria, idMarca);

        // Edición manual (M-30): lo que cambia lo eligió una persona; lo que no cambia conserva su origen, para que
        // editar el precio no borre que la marca vino de una sugerencia aceptada. Quitar la marca a mano es "Sin
        // marca" confirmada; quitar la categoría deja el artículo sin clasificar.
        var origenCategoria = idCategoria == IdCategoria
            ? OrigenCategoria
            : idCategoria.HasValue ? OrigenClasificacionEnum.Manual : null;
        var origenMarca = idMarca == IdMarca ? OrigenMarca : OrigenClasificacionEnum.Manual;

        if (EsDerivado && esServicio)
        {
            throw new DomainException("Una presentación derivada no puede ser un servicio.");
        }

        // Todas las validaciones y el cálculo ocurren antes de asignar el primer campo (H-08).
        var codigo = ReglasCodigoBarras.ExigirCodigoDeArticulo(codigoBarras);
        var precioVenta = CalcularPrecioVenta(costoReposicion, porcentajeGanancia);

        Descripcion = descripcion.Trim();
        IdCategoria = idCategoria;
        OrigenCategoria = origenCategoria;
        IdMarca = idMarca;
        OrigenMarca = origenMarca;
        CodigoBarras = codigo;
        CostoReposicion = costoReposicion;
        PorcentajeGanancia = porcentajeGanancia;
        PrecioVenta = precioVenta;
        EsServicio = esServicio;
        StockActual = esServicio ? 0 : Math.Max(0, stockActual);
        StockMinimo = esServicio ? 0 : Math.Max(0, stockMinimo);
    }

    /// <summary>
    /// Asigna categoría y marca con el origen que indique quien confirma la clasificación (Track 6.6, sección 8.2):
    /// la incorporación de catálogo, con la sugerencia aceptada o corregida. Un origen nulo deja ese dato sin
    /// clasificar.
    /// </summary>
    public void Clasificar(
        int? idCategoria,
        OrigenClasificacionEnum? origenCategoria,
        int? idMarca,
        OrigenClasificacionEnum? origenMarca)
    {
        ValidarIdentificadoresDeClasificacion(idCategoria, idMarca);
        ValidarOrigen(origenCategoria);
        ValidarOrigen(origenMarca);

        if (idCategoria.HasValue != origenCategoria.HasValue)
        {
            throw new DomainException("La categoría y su origen se asignan juntos: no hay categoría sin origen ni origen sin categoría.");
        }

        if (idMarca.HasValue && !origenMarca.HasValue)
        {
            throw new DomainException("Una marca asignada debe indicar su origen.");
        }

        IdCategoria = idCategoria;
        OrigenCategoria = origenCategoria;
        IdMarca = idMarca;
        OrigenMarca = origenMarca;
    }

    /// <summary>
    /// Hace principal una fuente que ya es de este artículo (A-16, J1) y toma su costo, porque solo la principal fija
    /// el costo (A-2). La fuente anterior, si había, sigue siendo fuente (J2). Las presentaciones derivadas las
    /// recalcula Application, que las conoce. Un servicio no tiene fuentes, así que tampoco principal.
    /// </summary>
    public void MarcarFuentePrincipal(CatalogoProveedor fuente)
    {
        ArgumentNullException.ThrowIfNull(fuente);

        if (EsServicio)
        {
            throw new DomainException($"El servicio '{Descripcion}' no puede tener fuente principal: un servicio no se compra.");
        }

        if (fuente.Id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fuente), "La fuente principal debe ser un ítem de catálogo ya guardado.");
        }

        if (!fuente.EsFuenteDe(this))
        {
            throw new DomainException(
                $"El ítem '{fuente.CodigoProveedor}' no es fuente de este artículo: vincúlelo antes de marcarlo como principal.");
        }

        ActualizarCostoYRecalcularPrecio(fuente.CostoReposicion);
        IdCatalogoProveedor = fuente.Id;
    }

    /// <summary>
    /// Deja el artículo sin fuente principal; conserva el último costo. No se elige otra sola (J5).
    /// </summary>
    public void QuitarFuentePrincipal()
    {
        IdCatalogoProveedor = null;
        CatalogoProveedor = null;
    }

    /// <summary>
    /// Convierte este artículo en una presentación derivada del origen indicado y toma de él su costo (RF-21).
    /// Las reglas que miran a otros artículos (por ejemplo, que el origen no se dé de baja) viven en Application.
    /// </summary>
    public void DefinirComoPresentacionDe(Articulo origen, int unidadesPorOrigen)
    {
        ArgumentNullException.ThrowIfNull(origen);

        if (unidadesPorOrigen < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(unidadesPorOrigen), "Las unidades por origen deben ser al menos 1.");
        }

        if (ReferenceEquals(origen, this) || (Id > 0 && origen.Id == Id))
        {
            throw new DomainException("Un artículo no puede ser presentación derivada de sí mismo.");
        }

        if (origen.EsDerivado)
        {
            throw new DomainException("El artículo de origen ya es una presentación derivada: la derivación es de un solo nivel.");
        }

        if (IdCatalogoProveedor.HasValue)
        {
            throw new DomainException("Un artículo vinculado a un catálogo de proveedor no puede ser una presentación derivada.");
        }

        if (EsServicio || origen.EsServicio)
        {
            throw new DomainException("Ni la presentación derivada ni su artículo de origen pueden ser servicios.");
        }

        if (Presentaciones.Count > 0)
        {
            throw new DomainException("Un artículo que ya es origen de otras presentaciones no puede ser, a su vez, una presentación derivada.");
        }

        var costoDerivado = CalcularCostoPresentacion(origen.CostoReposicion, unidadesPorOrigen);
        ActualizarCostoYRecalcularPrecio(costoDerivado);

        IdArticuloOrigen = origen.Id;
        ArticuloOrigen = origen;
        UnidadesPorOrigen = unidadesPorOrigen;
    }

    /// <summary>
    /// Recalcula el costo de la presentación a partir del nuevo costo del origen y su precio con el markup propio.
    /// Pisa un costo editado a mano: es el comportamiento acordado (RF-21).
    /// </summary>
    public void RecalcularCostoDesdeOrigen(decimal costoOrigen)
    {
        if (!EsDerivado || UnidadesPorOrigen is not int unidadesPorOrigen)
        {
            throw new InvalidOperationException("Solo una presentación derivada puede recalcular su costo desde un artículo de origen.");
        }

        ActualizarCostoYRecalcularPrecio(CalcularCostoPresentacion(costoOrigen, unidadesPorOrigen));
    }

    public void DescontarStock(int cantidad)
    {
        VerificarStockDisponible(cantidad);

        StockActual -= cantidad;
    }

    /// <summary>
    /// Verifica, sin modificar el artículo, que se puedan descontar <paramref name="cantidad"/> unidades.
    /// Permite validar todos los artículos de una operación antes de mutar cualquiera de ellos.
    /// </summary>
    public void VerificarStockDisponible(int cantidad)
    {
        ValidarMovimientoDeStock(cantidad);

        if (cantidad > StockActual)
        {
            throw new StockInsuficienteException(Id, StockActual, cantidad);
        }
    }

    public void IncrementarStock(int cantidad)
    {
        ValidarMovimientoDeStock(cantidad);

        StockActual = checked(StockActual + cantidad);
    }

    private void ValidarMovimientoDeStock(int cantidad)
    {
        if (cantidad < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(cantidad), "La cantidad debe ser al menos 1.");
        }

        if (EsServicio)
        {
            throw new DomainException("Un servicio no maneja stock.");
        }
    }

    /// <summary>
    /// Único punto de redondeo monetario del artículo: 2 decimales, alejándose del cero en el punto medio (D-06).
    /// </summary>
    private static decimal RedondearMoneda(decimal importe)
    {
        return ReglasMonetarias.Redondear(importe);
    }

    private static void ValidarIdentificadoresDeClasificacion(int? idCategoria, int? idMarca)
    {
        if (idCategoria.HasValue && idCategoria.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idCategoria), "La categoría debe ser válida.");
        }

        if (idMarca.HasValue && idMarca.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idMarca), "La marca debe ser válida.");
        }
    }

    private static void ValidarOrigen(OrigenClasificacionEnum? origen)
    {
        if (origen.HasValue && !Enum.IsDefined(origen.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(origen), "El origen de la clasificación no es válido.");
        }
    }
}

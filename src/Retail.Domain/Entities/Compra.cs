using Retail.Domain.Common;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Entities;

/// <summary>
/// Raíz de Agregado que representa la adquisición de mercadería a un proveedor con impacto en costos y stock.
/// Toda creación y modificación de renglones ocurre a través de los métodos de esta raíz (Ley 2).
/// </summary>
public class Compra : BaseEntity, IAggregateRoot
{
    // ── Backing field para la colección privada de renglones ──────────────────
    private readonly List<DetalleCompra> _detalles = new();

    // ── Constructor privado: la instanciación se hace únicamente por el método fábrica ──
    private Compra() { }

    // ── Propiedades de solo lectura pública ───────────────────────────────────

    public int IdProveedor { get; private set; }

    public Proveedor? Proveedor { get; private set; }

    public int IdUsuario { get; private set; }

    public Usuario? Usuario { get; private set; }

    public int? IdPedidoOrigen { get; private set; }

    public Pedido? PedidoOrigen { get; private set; }

    public string TipoComprobante { get; private set; } = string.Empty;

    public string NumeroComprobante { get; private set; } = string.Empty;

    public DateTime FechaEmision { get; private set; }

    public decimal Subtotal { get; private set; }

    public decimal Total { get; private set; }

    /// <summary>
    /// Estado del comprobante. Valor inicial: "INGRESADA".
    /// Queda reservado para una futura máquina de estados (Etapa 4 avanzada).
    /// </summary>
    public string Estado { get; private set; } = "INGRESADA";

    /// <summary>
    /// Colección de renglones de compra. Solo lectura para el exterior; se puebla con <see cref="AgregarDetalle"/>.
    /// </summary>
    public IReadOnlyCollection<DetalleCompra> Detalles => _detalles.AsReadOnly();

    // ── Método fábrica ────────────────────────────────────────────────────────

    /// <summary>
    /// Crea y devuelve una nueva compra válida. Único punto de instanciación del agregado.
    /// </summary>
    public static Compra Registrar(
        int idProveedor,
        int idUsuario,
        string tipoComprobante,
        string numeroComprobante,
        DateTime fechaEmision,
        int? idPedidoOrigen = null)
    {
        if (idProveedor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idProveedor), "El identificador de proveedor debe ser mayor a cero.");
        }

        if (idUsuario <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idUsuario), "El identificador de usuario debe ser mayor a cero.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(tipoComprobante);
        ArgumentException.ThrowIfNullOrWhiteSpace(numeroComprobante);

        if (fechaEmision > DateTime.UtcNow.AddDays(1))
        {
            throw new DomainException("La fecha de emisión no puede ser una fecha futura.");
        }

        if (idPedidoOrigen.HasValue && idPedidoOrigen.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idPedidoOrigen), "El identificador del pedido de origen debe ser mayor a cero.");
        }

        return new Compra
        {
            IdProveedor = idProveedor,
            IdUsuario = idUsuario,
            TipoComprobante = tipoComprobante.Trim(),
            NumeroComprobante = numeroComprobante.Trim(),
            FechaEmision = fechaEmision,
            IdPedidoOrigen = idPedidoOrigen,
        };
    }

    // ── Método de dominio: agregar renglón ────────────────────────────────────

    /// <summary>
    /// Agrega un renglón de artículo a la compra y acumula el subtotal y el total del comprobante.
    /// El costo unitario se redondea con la política monetaria oficial (D-06).
    /// </summary>
    /// <param name="idArticulo">Identificador del artículo adquirido.</param>
    /// <param name="cantidad">Unidades compradas (mínimo 1).</param>
    /// <param name="costoUnitario">Costo de reposición por unidad (mayor a cero).</param>
    public void AgregarDetalle(int idArticulo, int cantidad, decimal costoUnitario)
    {
        if (idArticulo <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idArticulo), "El identificador de artículo debe ser mayor a cero.");
        }

        if (cantidad < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(cantidad), "La cantidad debe ser al menos 1.");
        }

        if (costoUnitario <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(costoUnitario), "El costo unitario debe ser mayor a cero.");
        }

        // C-2: Dos renglones del mismo artículo con distintos costos dejan el costo final indeterminado según el orden.
        if (_detalles.Any(d => d.IdArticulo == idArticulo))
        {
            throw new DomainException(
                $"El artículo con Id {idArticulo} ya figura en la compra. " +
                "No se puede agregar el mismo artículo dos veces.");
        }

        var costoRedondeado = ReglasMonetarias.Redondear(costoUnitario);
        var subtotalItem = ReglasMonetarias.Redondear(costoRedondeado * cantidad);

        var detalle = new DetalleCompra(idArticulo, cantidad, costoRedondeado, subtotalItem);
        _detalles.Add(detalle);

        Subtotal = ReglasMonetarias.Redondear(Subtotal + subtotalItem);
        Total = Subtotal; // Sin IVA discriminado en esta etapa; extensible a Total = Subtotal * (1 + alicuota).
    }
}

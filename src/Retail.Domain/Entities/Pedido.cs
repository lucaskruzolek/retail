using Retail.Domain.Common;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Entities;

/// <summary>
/// Raíz de Agregado del pedido a proveedor (RF-28): la intención de compra que Compras da por recibida (RF-29).
/// Sus renglones solo se modifican a través de esta raíz. Referencia al proveedor, al usuario, al artículo y al
/// ítem de catálogo por identidad.
/// </summary>
/// <remarks>
/// La fecha de creación es la heredada <see cref="BaseEntity.CreatedAt"/>: no hay un <c>FechaCreacion</c> aparte.
/// El control de concurrencia (<c>RowVersion</c>) no es una propiedad del dominio: se configura como propiedad
/// sombra en Infraestructura, igual que en <see cref="Articulo"/> (D-12).
/// </remarks>
public class Pedido : BaseEntity, IAggregateRoot
{
    public const int LongitudMaximaCodigoProveedor = 50;
    public const int LongitudMaximaDescripcion = 200;
    public const int LongitudMaximaMotivoCancelacion = 250;

    private readonly List<DetallePedido> _detalles = new();

    public int IdProveedor { get; private set; }

    public Proveedor? Proveedor { get; private set; }

    public int IdUsuario { get; private set; }

    public Usuario? Usuario { get; private set; }

    public EstadoPedidoEnum Estado { get; private set; }

    public DateTime? FechaEmision { get; private set; }

    /// <summary>
    /// Cuándo terminó el pedido: al recibirse o al cancelarse.
    /// </summary>
    public DateTime? FechaCierre { get; private set; }

    public string? MotivoCancelacion { get; private set; }

    public IReadOnlyCollection<DetallePedido> Detalles => _detalles.AsReadOnly();

    /// <summary>
    /// Suma de los subtotales de los renglones, redondeada una sola vez (D-06). Es calculado: no se persiste.
    /// </summary>
    public decimal TotalEstimado => ReglasMonetarias.Redondear(_detalles.Sum(d => d.SubtotalEstimado));

    protected Pedido() { }

    public static Pedido CrearBorrador(int idProveedor, int idUsuario)
    {
        if (idProveedor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idProveedor), "El pedido debe dirigirse a un proveedor válido.");
        }

        if (idUsuario <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idUsuario), "El pedido debe registrar al usuario que lo realiza.");
        }

        return new Pedido
        {
            IdProveedor = idProveedor,
            IdUsuario = idUsuario,
            Estado = EstadoPedidoEnum.Borrador,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Agrega un ítem del catálogo del proveedor al borrador. Si el ítem ya figura, suma la cantidad y actualiza la
    /// foto (código, descripción y costo) con los valores recibidos: a diferencia de una venta, un borrador es una
    /// estimación, así que el último costo del catálogo es el mejor que se conoce.
    /// </summary>
    public void AgregarRenglon(
        int idArticulo,
        int idCatalogoProveedor,
        string codigoProveedor,
        string descripcion,
        int cantidad,
        decimal costoPactado)
    {
        ExigirEstado("agregar renglones a", EstadoPedidoEnum.Borrador);

        if (idArticulo <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idArticulo), "El identificador del artículo no es válido.");
        }

        if (idCatalogoProveedor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idCatalogoProveedor), "El identificador del ítem de catálogo no es válido.");
        }

        ExigirCantidad(cantidad);
        ExigirCosto(costoPactado);
        var codigo = ExigirTexto(codigoProveedor, "el código del proveedor", LongitudMaximaCodigoProveedor);
        var texto = ExigirTexto(descripcion, "la descripción", LongitudMaximaDescripcion);

        var existente = _detalles.FirstOrDefault(d => d.IdCatalogoProveedor == idCatalogoProveedor);
        if (existente is null)
        {
            _detalles.Add(new DetallePedido(idArticulo, idCatalogoProveedor, codigo, texto, cantidad, costoPactado));
            return;
        }

        // El ítem de catálogo fue reasignado a otro artículo: la línea no puede cambiar de artículo en silencio.
        if (existente.IdArticulo != idArticulo)
        {
            throw new DomainException($"El ítem de catálogo ID {idCatalogoProveedor} ya figura en el pedido para otro artículo.");
        }

        existente.SumarCantidad(cantidad);
        existente.ActualizarFoto(codigo, texto, costoPactado);
    }

    public void CambiarCantidad(int idCatalogoProveedor, int cantidad)
    {
        ExigirEstado("cambiar la cantidad de un renglón de", EstadoPedidoEnum.Borrador);
        ExigirCantidad(cantidad);

        ObtenerRenglon(idCatalogoProveedor).CambiarCantidad(cantidad);
    }

    /// <summary>
    /// Quita un renglón del borrador (solo se admite en <c>Borrador</c>). El dominio solo lo saca de la colección: al
    /// guardar, la persistencia convierte el renglón huérfano en una baja lógica (Ley 3, D4), así que queda registro
    /// de lo que se quitó. Puede dejar el borrador vacío; no lo cancela.
    /// </summary>
    public void QuitarRenglon(int idCatalogoProveedor)
    {
        ExigirEstado("quitar renglones de", EstadoPedidoEnum.Borrador);

        _detalles.Remove(ObtenerRenglon(idCatalogoProveedor));
    }

    public void Emitir()
    {
        ExigirEstado("emitir", EstadoPedidoEnum.Borrador);

        if (_detalles.Count == 0)
        {
            throw new DomainException("No se puede emitir un pedido sin renglones.");
        }

        Estado = EstadoPedidoEnum.Emitido;
        FechaEmision = DateTime.UtcNow;
    }

    /// <summary>
    /// Contrato de frontera con Compras (§5.1 del rector docs/roadmap/etapa-4-track-4.3-abastecimiento.md): lo
    /// invoca la registración de la compra que recibe este pedido (RF-29).
    /// </summary>
    public void MarcarRecibido()
    {
        ExigirEstado("marcar como recibido", EstadoPedidoEnum.Emitido);

        Estado = EstadoPedidoEnum.Recibido;
        FechaCierre = DateTime.UtcNow;
    }

    public void Cancelar(string motivo)
    {
        ExigirEstado("cancelar", EstadoPedidoEnum.Borrador, EstadoPedidoEnum.Emitido);

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new DomainException("Debe indicarse el motivo de la cancelación del pedido.");
        }

        var texto = ReglasTexto.NormalizarEspacios(motivo);
        if (texto.Length > LongitudMaximaMotivoCancelacion)
        {
            throw new DomainException($"El motivo de la cancelación supera los {LongitudMaximaMotivoCancelacion} caracteres.");
        }

        Estado = EstadoPedidoEnum.Cancelado;
        MotivoCancelacion = texto;
        FechaCierre = DateTime.UtcNow;
    }

    private DetallePedido ObtenerRenglon(int idCatalogoProveedor)
    {
        return _detalles.FirstOrDefault(d => d.IdCatalogoProveedor == idCatalogoProveedor)
            ?? throw new DomainException($"El ítem de catálogo ID {idCatalogoProveedor} no figura en el pedido.");
    }

    private void ExigirEstado(string operacion, params EstadoPedidoEnum[] permitidos)
    {
        if (!permitidos.Contains(Estado))
        {
            throw new TransicionPedidoInvalidaException(Estado, operacion);
        }
    }

    private static void ExigirCantidad(int cantidad)
    {
        if (cantidad < 1)
        {
            throw new DomainException("La cantidad pedida debe ser al menos 1.");
        }
    }

    private static void ExigirCosto(decimal costoPactado)
    {
        if (costoPactado <= 0m)
        {
            throw new DomainException("El costo pactado debe ser mayor a cero.");
        }
    }

    private static string ExigirTexto(string valor, string campo, int longitudMaxima)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new DomainException($"Debe indicarse {campo} del renglón.");
        }

        var texto = ReglasTexto.NormalizarEspacios(valor);
        if (texto.Length > longitudMaxima)
        {
            throw new DomainException($"{char.ToUpperInvariant(campo[0])}{campo[1..]} del renglón supera los {longitudMaxima} caracteres.");
        }

        return texto;
    }
}

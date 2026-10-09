using Retail.Domain.Common;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Entities;

/// <summary>
/// Raíz de Agregado que representa una transacción de venta formalizada en el mostrador (RF-09).
/// Sus ítems y pagos solo se modifican a través de esta raíz, que mantiene los totales consistentes.
/// Referencia al artículo, al turno y al cliente por identidad: el stock, la caja y la cuenta corriente
/// pertenecen a otros agregados y se modifican desde la capa de Aplicación.
/// </summary>
public class Venta : BaseEntity, IAggregateRoot
{
    private readonly List<DetalleVenta> _detalles = new();
    private readonly List<PagoVenta> _pagos = new();

    public int IdTurno { get; private set; }

    public TurnoCaja? TurnoCaja { get; private set; }

    public int IdUsuario { get; private set; }

    public Usuario? Usuario { get; private set; }

    /// <summary>
    /// Cliente asociado a la venta. <c>null</c> representa al Consumidor Final (RF-09).
    /// </summary>
    public int? IdCliente { get; private set; }

    public Cliente? Cliente { get; private set; }

    public int? IdPresupuestoOrigen { get; private set; }

    public Presupuesto? PresupuestoOrigen { get; private set; }

    public DateTime FechaHora { get; private set; } = DateTime.UtcNow;

    public decimal Subtotal { get; private set; }

    public decimal Descuento { get; private set; }

    public decimal Total { get; private set; }

    public EstadoFiscalEnum EstadoFiscal { get; private set; } = EstadoFiscalEnum.NoAplica;

    public IReadOnlyCollection<DetalleVenta> Detalles => _detalles.AsReadOnly();

    public IReadOnlyCollection<PagoVenta> Pagos => _pagos.AsReadOnly();

    public ComprobanteFiscal? ComprobanteFiscal { get; private set; }

    public decimal TotalPagado => _pagos.Sum(p => p.Monto);

    /// <summary>
    /// Pagos que ingresan como efectivo a la gaveta del turno.
    /// </summary>
    public decimal TotalEfectivo => _pagos.Where(p => p.MedioPago == MedioPagoEnum.Efectivo).Sum(p => p.Monto);

    /// <summary>
    /// Pagos que se imputan como deuda del cliente: no ingresan dinero a la caja.
    /// </summary>
    public decimal TotalCuentaCorriente => _pagos.Where(p => p.MedioPago == MedioPagoEnum.CuentaCorriente).Sum(p => p.Monto);

    /// <summary>
    /// Pagos con tarjeta o transferencia, que se concilian con el cierre de lote (RF-15).
    /// </summary>
    public decimal TotalElectronico => TotalPagado - TotalEfectivo - TotalCuentaCorriente;

    protected Venta() { }

    public static Venta Registrar(int idTurno, int idUsuario, int? idCliente)
    {
        if (idTurno <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idTurno), "La venta debe pertenecer a un turno de caja válido.");
        }

        if (idUsuario <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idUsuario), "La venta debe registrar al usuario que la realiza.");
        }

        if (idCliente is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idCliente), "El identificador del cliente no es válido.");
        }

        return new Venta
        {
            IdTurno = idTurno,
            IdUsuario = idUsuario,
            IdCliente = idCliente,
            FechaHora = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Agrega un artículo al ticket. Si el artículo ya figura, suma la cantidad a la misma línea.
    /// </summary>
    public void AgregarItem(int idArticulo, int cantidad, decimal precioUnitario)
    {
        var existente = _detalles.FirstOrDefault(d => d.IdArticulo == idArticulo);
        if (existente != null)
        {
            if (existente.PrecioUnitario != precioUnitario)
            {
                throw new DomainException($"El artículo ID {idArticulo} ya figura en la venta con otro precio unitario.");
            }

            existente.SumarCantidad(cantidad);
        }
        else
        {
            _detalles.Add(new DetalleVenta(idArticulo, cantidad, precioUnitario));
        }

        RecalcularTotales();
    }

    public void AplicarDescuento(decimal monto)
    {
        if (monto < 0m)
        {
            throw new DomainException("El descuento no puede ser negativo.");
        }

        if (monto > Subtotal)
        {
            throw new DomainException($"El descuento ({monto:C}) no puede superar el subtotal de la venta ({Subtotal:C}).");
        }

        Descuento = monto;
        RecalcularTotales();
    }

    public void ImputarPago(MedioPagoEnum medioPago, decimal monto, string? referenciaPago = null)
    {
        if (medioPago == MedioPagoEnum.CuentaCorriente && IdCliente is null)
        {
            throw new CuentaCorrienteNoHabilitadaException("El Consumidor Final no puede pagar con cuenta corriente: asocie un cliente a la venta.");
        }

        _pagos.Add(new PagoVenta(medioPago, monto, referenciaPago));
    }

    /// <summary>
    /// Verifica que la venta pueda confirmarse: al menos un ítem y pagos que cubran exactamente el total.
    /// El vuelto del efectivo no forma parte de los pagos: se calcula en el mostrador.
    /// </summary>
    public void ValidarCierre()
    {
        if (_detalles.Count == 0)
        {
            throw new VentaVaciaException();
        }

        decimal totalPagado = TotalPagado;

        if (totalPagado < Total)
        {
            throw new MontoPagoInsuficienteException(Total, totalPagado);
        }

        if (totalPagado > Total)
        {
            throw new DomainException($"Los pagos imputados ({totalPagado:C}) superan el total de la venta ({Total:C}).");
        }
    }

    private void RecalcularTotales()
    {
        Subtotal = _detalles.Sum(d => d.SubtotalItem);
        Total = Subtotal - Descuento;
    }
}

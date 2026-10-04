using Retail.Domain.Common;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Entities;

/// <summary>
/// Raíz de Agregado que custodia el ciclo de vida de una sesión de mostrador y balance de gaveta de efectivo.
/// </summary>
public class TurnoCaja : BaseEntity, IAggregateRoot
{
    private readonly List<MovimientoCaja> _movimientos = new();
    private readonly List<Venta> _ventas = new();
    private readonly List<CobranzaCliente> _cobranzas = new();

    public int IdUsuario { get; private set; }

    public Usuario? Usuario { get; private set; }

    public DateTime FechaApertura { get; private set; } = DateTime.UtcNow;

    public decimal SaldoInicial { get; private set; }

    public DateTime? FechaCierre { get; private set; }

    public decimal TotalVentasEfectivo { get; private set; }

    public decimal TotalIngresosEfectivo { get; private set; }

    public decimal TotalEgresosEfectivo { get; private set; }

    public decimal SaldoTeoricoEfectivo { get; private set; }

    public decimal? SaldoDeclaradoEfectivo { get; private set; }

    public decimal? DiferenciaEfectivo { get; private set; }

    public decimal TotalVentasElectronicas { get; private set; }

    public decimal MontoRetenidoEnCaja { get; private set; }

    public EstadoTurnoEnum Estado { get; private set; } = EstadoTurnoEnum.Abierto;

    public IReadOnlyCollection<MovimientoCaja> Movimientos => _movimientos.AsReadOnly();

    public IReadOnlyCollection<Venta> Ventas => _ventas.AsReadOnly();

    public IReadOnlyCollection<CobranzaCliente> Cobranzas => _cobranzas.AsReadOnly();

    protected TurnoCaja() { }

    public TurnoCaja(int idUsuario, decimal saldoInicial)
    {
        if (saldoInicial < 0)
            throw new DomainException("El saldo inicial de caja no puede ser negativo.");

        IdUsuario = idUsuario;
        SaldoInicial = saldoInicial;
        SaldoTeoricoEfectivo = saldoInicial;
        Estado = EstadoTurnoEnum.Abierto;
        FechaApertura = DateTime.UtcNow;
    }

    public static TurnoCaja Abrir(int idUsuario, decimal saldoInicial)
    {
        return new TurnoCaja(idUsuario, saldoInicial);
    }

    public void RegistrarMovimiento(TipoMovimientoCajaEnum tipo, decimal monto, string concepto)
    {
        if (Estado != EstadoTurnoEnum.Abierto)
            throw new TurnoYaCerradoException("No se pueden registrar movimientos en un turno de caja cerrado.");

        if (monto <= 0)
            throw new DomainException("El monto del movimiento debe ser mayor a cero.");

        if (string.IsNullOrWhiteSpace(concepto))
            throw new DomainException("El concepto del movimiento es obligatorio.");

        if (tipo == TipoMovimientoCajaEnum.RetiroExtraordinario)
        {
            if (monto > SaldoTeoricoEfectivo)
                throw new SaldoCajaInsuficienteException(Id, SaldoTeoricoEfectivo, monto);

            TotalEgresosEfectivo += monto;
            SaldoTeoricoEfectivo -= monto;
        }
        else if (tipo == TipoMovimientoCajaEnum.IngresoExtraordinario)
        {
            TotalIngresosEfectivo += monto;
            SaldoTeoricoEfectivo += monto;
        }

        var movimiento = new MovimientoCaja
        {
            IdTurno = Id,
            TipoMovimiento = tipo,
            Monto = monto,
            Concepto = concepto.Trim(),
            FechaHora = DateTime.UtcNow
        };
        _movimientos.Add(movimiento);
    }

    public void ImputarCobranza(decimal monto, MedioPagoEnum medioPago)
    {
        if (Estado != EstadoTurnoEnum.Abierto)
            throw new TurnoYaCerradoException("No se puede imputar cobranzas en un turno cerrado.");

        if (monto <= 0) return;

        if (medioPago == MedioPagoEnum.Efectivo)
        {
            TotalIngresosEfectivo += monto;
            SaldoTeoricoEfectivo += monto;
        }
        else
        {
            TotalVentasElectronicas += monto;
        }
    }

    public void ImputarVenta(decimal totalEfectivo, decimal totalElectronico)
    {
        if (Estado != EstadoTurnoEnum.Abierto)
            throw new TurnoYaCerradoException("No se puede imputar ventas en un turno cerrado.");

        if (totalEfectivo < 0 || totalElectronico < 0)
            throw new DomainException("Los montos de venta no pueden ser negativos.");

        if (totalEfectivo > 0)
        {
            TotalVentasEfectivo += totalEfectivo;
            SaldoTeoricoEfectivo += totalEfectivo;
        }

        if (totalElectronico > 0)
        {
            TotalVentasElectronicas += totalElectronico;
        }
    }

    public void CerrarConArqueoCiego(decimal saldoDeclaradoEfectivo, decimal montoRetenidoEnCaja)
    {
        if (Estado != EstadoTurnoEnum.Abierto)
            throw new TurnoYaCerradoException("El turno de caja ya se encuentra cerrado.");

        if (saldoDeclaradoEfectivo < 0)
            throw new DomainException("El saldo declarado en efectivo no puede ser negativo.");

        if (montoRetenidoEnCaja < 0 || montoRetenidoEnCaja > saldoDeclaradoEfectivo)
            throw new DomainException("El monto retenido en caja es inválido respecto al efectivo declarado.");

        SaldoDeclaradoEfectivo = saldoDeclaradoEfectivo;
        MontoRetenidoEnCaja = montoRetenidoEnCaja;
        DiferenciaEfectivo = saldoDeclaradoEfectivo - SaldoTeoricoEfectivo;
        FechaCierre = DateTime.UtcNow;
        Estado = EstadoTurnoEnum.Cerrado;
    }
}

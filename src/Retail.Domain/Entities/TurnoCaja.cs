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

    /// <summary>
    /// Terminal (PC de mostrador) donde se abrió el turno. Un turno pertenece a una cajera Y a una terminal (MT-1).
    /// </summary>
    public int IdTerminal { get; private set; }

    public Terminal? Terminal { get; private set; }

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

    /// <summary>
    /// Cantidad de ventas imputadas al turno. Además de su valor de negocio, garantiza que toda venta modifique la
    /// fila del turno: así el token de concurrencia (rowversion) se verifica aun cuando la venta no mueve dinero de
    /// la gaveta (100 % cuenta corriente o total $0). Sin este contador, EF Core no emitiría ningún UPDATE y la venta
    /// podría confirmarse sobre un turno que un encargado cerró desde otra terminal (hallazgo H18 de
    /// docs/auditorias/auditoria-concurrencia-multiterminal.md).
    /// </summary>
    public int CantidadVentas { get; private set; }

    public EstadoTurnoEnum Estado { get; private set; } = EstadoTurnoEnum.Abierto;

    public IReadOnlyCollection<MovimientoCaja> Movimientos => _movimientos.AsReadOnly();

    public IReadOnlyCollection<Venta> Ventas => _ventas.AsReadOnly();

    public IReadOnlyCollection<CobranzaCliente> Cobranzas => _cobranzas.AsReadOnly();

    protected TurnoCaja() { }

    public TurnoCaja(int idUsuario, int idTerminal, decimal saldoInicial)
    {
        if (idUsuario <= 0)
            throw new DomainException("El turno debe pertenecer a un usuario válido.");

        if (idTerminal <= 0)
            throw new DomainException("El turno debe abrirse en una terminal válida.");

        if (saldoInicial < 0)
            throw new DomainException("El saldo inicial de caja no puede ser negativo.");

        IdUsuario = idUsuario;
        IdTerminal = idTerminal;
        SaldoInicial = saldoInicial;
        SaldoTeoricoEfectivo = saldoInicial;
        Estado = EstadoTurnoEnum.Abierto;
        FechaApertura = DateTime.UtcNow;
    }

    public static TurnoCaja Abrir(int idUsuario, int idTerminal, decimal saldoInicial)
    {
        return new TurnoCaja(idUsuario, idTerminal, saldoInicial);
    }

    /// <summary>
    /// Indica si el turno es del usuario dado. Solo el dueño de un turno abierto puede operarlo (MT-14).
    /// </summary>
    public bool EsDe(int idUsuario)
    {
        return IdUsuario == idUsuario;
    }

    /// <summary>
    /// Control optimista "fuera de línea" para el cierre: quien cierra (por ejemplo, un Gerente desde otra terminal)
    /// decide con importes que leyó minutos antes. Si el saldo teórico, el total electrónico o la cantidad de ventas
    /// cambiaron desde entonces, el cierre se rechaza: toda venta, cobranza o movimiento de efectivo altera al menos
    /// uno de los tres valores.
    /// </summary>
    public void ExigirSinCambiosDesde(decimal saldoTeoricoLeido, decimal totalElectronicoLeido, int cantidadVentasLeida)
    {
        if (SaldoTeoricoEfectivo != saldoTeoricoLeido
            || TotalVentasElectronicas != totalElectronicoLeido
            || CantidadVentas != cantidadVentasLeida)
        {
            throw new TurnoModificadoException();
        }
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

        // Se incrementa siempre, incluso si la venta no mueve efectivo ni dinero electrónico: "materializa el
        // conflicto" para que el rowversion del turno se verifique (H18).
        CantidadVentas++;

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

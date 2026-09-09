using Retail.Domain.Common;
using Retail.Domain.Enums;

namespace Retail.Domain.Entities;

/// <summary>
/// Raíz de Agregado que custodia el ciclo de vida de una sesión de mostrador y balance de gaveta de efectivo.
/// </summary>
public class TurnoCaja : BaseEntity, IAggregateRoot
{
    public int IdUsuario { get; set; }

    public Usuario? Usuario { get; set; }

    public DateTime FechaApertura { get; set; } = DateTime.UtcNow;

    public decimal SaldoInicial { get; set; }

    public DateTime? FechaCierre { get; set; }

    public decimal TotalVentasEfectivo { get; set; }

    public decimal TotalIngresosEfectivo { get; set; }

    public decimal TotalEgresosEfectivo { get; set; }

    public decimal SaldoTeoricoEfectivo { get; set; }

    public decimal? SaldoDeclaradoEfectivo { get; set; }

    public decimal? DiferenciaEfectivo { get; set; }

    public decimal TotalVentasElectronicas { get; set; }

    public decimal MontoRetenidoEnCaja { get; set; }

    public EstadoTurnoEnum Estado { get; set; } = EstadoTurnoEnum.Abierto;

    public ICollection<MovimientoCaja> Movimientos { get; set; } = new List<MovimientoCaja>();

    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();

    public ICollection<CobranzaCliente> Cobranzas { get; set; } = new List<CobranzaCliente>();
}

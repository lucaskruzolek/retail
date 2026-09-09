using Retail.Domain.Common;
using Retail.Domain.Enums;

namespace Retail.Domain.Entities;

/// <summary>
/// Raíz de Agregado que representa a un cliente con soporte de cuenta corriente comercial.
/// </summary>
public class Cliente : BaseEntity, IAggregateRoot
{
    public string RazonSocialONombre { get; set; } = string.Empty;

    public TipoDocumentoEnum TipoDocumento { get; set; } = TipoDocumentoEnum.Dni;

    public string NumeroDocumento { get; set; } = string.Empty;

    public CondicionIvaEnum CondicionIva { get; set; } = CondicionIvaEnum.ConsumidorFinal;

    public string? DomicilioFiscal { get; set; }

    public string? Telefono { get; set; }

    public string? Email { get; set; }

    public bool TieneCuentaCorriente { get; set; }

    public decimal LimiteCredito { get; set; }

    public decimal SaldoCuentaCorriente { get; set; }

    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();

    public ICollection<Presupuesto> Presupuestos { get; set; } = new List<Presupuesto>();

    public ICollection<CobranzaCliente> Cobranzas { get; set; } = new List<CobranzaCliente>();
}

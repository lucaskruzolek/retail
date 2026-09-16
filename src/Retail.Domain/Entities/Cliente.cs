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

    public decimal CreditoDisponible => TieneCuentaCorriente ? Math.Max(0m, LimiteCredito - SaldoCuentaCorriente) : 0m;

    public void ActualizarDatos(
        string razonSocialONombre,
        TipoDocumentoEnum tipoDocumento,
        string numeroDocumento,
        CondicionIvaEnum condicionIva,
        string? domicilioFiscal,
        string? telefono,
        string? email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(razonSocialONombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(numeroDocumento);

        RazonSocialONombre = razonSocialONombre.Trim();
        TipoDocumento = tipoDocumento;
        NumeroDocumento = numeroDocumento.Trim();
        CondicionIva = condicionIva;
        DomicilioFiscal = string.IsNullOrWhiteSpace(domicilioFiscal) ? null : domicilioFiscal.Trim();
        Telefono = string.IsNullOrWhiteSpace(telefono) ? null : telefono.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
    }

    public void HabilitarCuentaCorriente(decimal limiteCredito)
    {
        if (limiteCredito < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(limiteCredito), "El límite de crédito no puede ser negativo.");
        }

        TieneCuentaCorriente = true;
        LimiteCredito = limiteCredito;
    }

    public void DeshabilitarCuentaCorriente()
    {
        if (SaldoCuentaCorriente > 0m)
        {
            throw new InvalidOperationException($"No se puede deshabilitar la cuenta corriente porque el cliente posee un saldo deudor pendiente de {SaldoCuentaCorriente:C}.");
        }

        TieneCuentaCorriente = false;
        LimiteCredito = 0m;
    }

    public void ModificarLimiteCredito(decimal nuevoLimite)
    {
        if (!TieneCuentaCorriente)
        {
            throw new Exceptions.CuentaCorrienteNoHabilitadaException(Id);
        }

        if (nuevoLimite < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(nuevoLimite), "El límite de crédito no puede ser negativo.");
        }

        if (nuevoLimite < SaldoCuentaCorriente)
        {
            throw new InvalidOperationException($"El nuevo límite ({nuevoLimite:C}) no puede ser inferior a la deuda actual del cliente ({SaldoCuentaCorriente:C}).");
        }

        LimiteCredito = nuevoLimite;
    }

    public void DebitarCuentaCorriente(decimal monto)
    {
        if (!TieneCuentaCorriente)
        {
            throw new Exceptions.CuentaCorrienteNoHabilitadaException(Id);
        }

        if (monto <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(monto), "El monto a debitar debe ser mayor a cero.");
        }

        if (SaldoCuentaCorriente + monto > LimiteCredito)
        {
            throw new Exceptions.LimiteCreditoExcedidoException(Id, SaldoCuentaCorriente, LimiteCredito, monto);
        }

        SaldoCuentaCorriente += monto;
    }

    public void AcreditarCobranza(decimal monto)
    {
        if (monto <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(monto), "El monto de la cobranza debe ser mayor a cero.");
        }

        if (monto > SaldoCuentaCorriente)
        {
            throw new Exceptions.CobranzaExcedeDeudaException(Id, SaldoCuentaCorriente, monto);
        }

        SaldoCuentaCorriente -= monto;
    }

    public CobranzaCliente RegistrarCobranza(
        int idTurno,
        int idUsuario,
        decimal monto,
        MedioPagoEnum medioPago,
        string? referencia = null)
    {
        if (!TieneCuentaCorriente && SaldoCuentaCorriente <= 0m)
        {
            throw new Exceptions.CuentaCorrienteNoHabilitadaException(Id);
        }

        AcreditarCobranza(monto);

        var cobranza = new CobranzaCliente
        {
            IdCliente = Id,
            IdTurno = idTurno,
            IdUsuario = idUsuario,
            Monto = monto,
            MedioPago = medioPago,
            Referencia = referencia,
            FechaHora = DateTime.UtcNow
        };

        Cobranzas.Add(cobranza);
        return cobranza;
    }

    public override void MarkAsDeleted()
    {
        if (SaldoCuentaCorriente > 0m)
        {
            throw new InvalidOperationException($"No se puede dar de baja al cliente porque mantiene un saldo deudor pendiente de {SaldoCuentaCorriente:C}.");
        }

        base.MarkAsDeleted();
    }
}

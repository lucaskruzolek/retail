using Retail.Domain.Common;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Entities;

/// <summary>
/// Raíz de Agregado que representa a un cliente con soporte de cuenta corriente comercial.
/// Los datos de identificación y contacto solo cambian a través de <see cref="Crear"/> y
/// <see cref="ActualizarDatos"/>, que los normalizan y validan con las reglas de <c>Retail.Domain.Common</c>.
/// </summary>
public class Cliente : BaseEntity, IAggregateRoot
{
    // Lo usa EF Core para materializar la entidad desde la base; el código de negocio usa Crear.
    private Cliente()
    {
    }

    /// <summary>
    /// "Apellido y Nombre" de una persona física o razón social de una empresa, en un único campo como lo pide el
    /// comprobante fiscal de ARCA. Cuál de las dos reglas se aplica lo decide el tipo de documento.
    /// </summary>
    public string RazonSocialONombre { get; private set; } = string.Empty;

    public TipoDocumentoEnum TipoDocumento { get; private set; } = TipoDocumentoEnum.Dni;

    /// <summary>Forma canónica: solo dígitos para DNI, CUIT y CUIL; mayúsculas sin separadores para pasaporte.</summary>
    public string NumeroDocumento { get; private set; } = string.Empty;

    public CondicionIvaEnum CondicionIva { get; private set; } = CondicionIvaEnum.ConsumidorFinal;

    public string? DomicilioFiscal { get; private set; }

    public string? Telefono { get; private set; }

    public string? Email { get; private set; }

    public bool TieneCuentaCorriente { get; set; }

    public decimal LimiteCredito { get; set; }

    public decimal SaldoCuentaCorriente { get; set; }

    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();

    public ICollection<Presupuesto> Presupuestos { get; set; } = new List<Presupuesto>();

    public ICollection<CobranzaCliente> Cobranzas { get; set; } = new List<CobranzaCliente>();

    public decimal CreditoDisponible => TieneCuentaCorriente ? Math.Max(0m, LimiteCredito - SaldoCuentaCorriente) : 0m;

    /// <summary>
    /// Método de creación del agregado: es la única forma de dar de alta un cliente. La cuenta corriente se
    /// habilita aparte con <see cref="HabilitarCuentaCorriente"/>, que custodia su propia invariante.
    /// </summary>
    public static Cliente Crear(
        string razonSocialONombre,
        TipoDocumentoEnum tipoDocumento,
        string numeroDocumento,
        CondicionIvaEnum condicionIva,
        string? domicilioFiscal = null,
        string? telefono = null,
        string? email = null)
    {
        var cliente = new Cliente();
        cliente.ActualizarDatos(
            razonSocialONombre,
            tipoDocumento,
            numeroDocumento,
            condicionIva,
            domicilioFiscal,
            telefono,
            email);
        return cliente;
    }

    /// <summary>
    /// Un DNI, un CUIL o un pasaporte identifican a una persona física, así que el nombre se valida como nombre de
    /// persona (sin dígitos ni símbolos). Un CUIT puede ser de una empresa, así que admite una razón social como
    /// "3M Argentina S.A.". Lo reutiliza el validador de Application para no duplicar la regla.
    /// </summary>
    public static bool EsNombreValido(TipoDocumentoEnum tipoDocumento, string? razonSocialONombre)
    {
        return ReglasDocumento.IdentificaPersonaFisica(tipoDocumento)
            ? ReglasTexto.EsNombreDePersonaValido(razonSocialONombre, ReglasTexto.LongitudMaximaRazonSocial)
            : ReglasTexto.EsRazonSocialValida(razonSocialONombre);
    }

    public void ActualizarDatos(
        string razonSocialONombre,
        TipoDocumentoEnum tipoDocumento,
        string numeroDocumento,
        CondicionIvaEnum condicionIva,
        string? domicilioFiscal,
        string? telefono,
        string? email)
    {
        if (!Enum.IsDefined(tipoDocumento))
        {
            throw new DomainException($"El tipo de documento '{tipoDocumento}' no es válido.");
        }

        if (!Enum.IsDefined(condicionIva))
        {
            throw new DomainException($"La condición de IVA '{condicionIva}' no es válida.");
        }

        // Se validan todos los valores antes de asignar alguno: el agregado nunca queda a medio actualizar.
        var nombreNormalizado = ReglasDocumento.IdentificaPersonaFisica(tipoDocumento)
            ? ReglasTexto.ExigirNombreDePersona(
                razonSocialONombre,
                "nombre del cliente",
                ReglasTexto.LongitudMaximaRazonSocial)
            : ReglasTexto.ExigirRazonSocial(razonSocialONombre, "razón social del cliente");
        var documentoNormalizado = ReglasDocumento.Exigir(tipoDocumento, numeroDocumento);
        var domicilioNormalizado = ReglasTexto.ExigirDomicilioOpcional(domicilioFiscal);
        var telefonoNormalizado = ReglasContacto.ExigirTelefonoOpcional(telefono);
        var emailNormalizado = ReglasContacto.ExigirEmailOpcional(email);

        RazonSocialONombre = nombreNormalizado;
        TipoDocumento = tipoDocumento;
        NumeroDocumento = documentoNormalizado;
        CondicionIva = condicionIva;
        DomicilioFiscal = domicilioNormalizado;
        Telefono = telefonoNormalizado;
        Email = emailNormalizado;
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

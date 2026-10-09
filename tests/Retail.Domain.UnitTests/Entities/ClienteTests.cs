using FluentAssertions;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Retail.Domain.UnitTests.TestData;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

public class ClienteTests
{
    [Fact]
    public void ActualizarDatos_ValoresValidos_ActualizaPropiedades()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear(razonSocialONombre: "Librería San Martín", tipoDocumento: TipoDocumentoEnum.Cuit, numeroDocumento: "30112233446", condicionIva: CondicionIvaEnum.ResponsableInscripto);

        // Act
        cliente.ActualizarDatos(
            "  Librería   San Martín S.A. ",
            TipoDocumentoEnum.Cuit,
            "30-99887766-7",
            CondicionIvaEnum.ResponsableInscripto,
            "Av.  Corrientes 1234",
            "(011) 4455-6677",
            " Contacto@LibreriaSanMartin.com ");

        // Assert: cada valor queda en su forma canónica.
        cliente.RazonSocialONombre.Should().Be("Librería San Martín S.A.");
        cliente.NumeroDocumento.Should().Be("30998877667");
        cliente.DomicilioFiscal.Should().Be("Av. Corrientes 1234");
        cliente.Telefono.Should().Be("01144556677");
        cliente.Email.Should().Be("contacto@libreriasanmartin.com");
    }

    [Theory]
    [InlineData("", "12345678")]
    [InlineData("   ", "12345678")]
    [InlineData("Cliente Valido", "")]
    [InlineData("Cliente Valido", "   ")]
    public void ActualizarDatos_CamposObligatoriosVacios_LanzaDomainException(string razonSocial, string numeroDocumento)
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear();

        // Act
        var act = () => cliente.ActualizarDatos(
            razonSocial,
            TipoDocumentoEnum.Dni,
            numeroDocumento,
            CondicionIvaEnum.ConsumidorFinal,
            null,
            null,
            null);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(TipoDocumentoEnum.Dni, "12345678")]
    [InlineData(TipoDocumentoEnum.Cuil, "20123456786")]
    [InlineData(TipoDocumentoEnum.Pasaporte, "AAA123456")]
    public void Crear_PersonaFisicaConDigitosEnElNombre_LanzaDomainException(TipoDocumentoEnum tipo, string documento)
    {
        // DNI, CUIL y pasaporte identifican a una persona: "Juan123" no es un nombre válido.
        var act = () => Cliente.Crear("Juan123 Pérez", tipo, documento, CondicionIvaEnum.ConsumidorFinal);

        act.Should().Throw<DomainException>().WithMessage("*nombre del cliente*");
    }

    [Fact]
    public void Crear_EmpresaConCuitYDigitosEnLaRazonSocial_LaAceptaSinAlterarMayusculas()
    {
        // Act: con CUIT puede ser una empresa, y "3M" o "S.A." se escriben así a propósito.
        var cliente = Cliente.Crear("3M Argentina S.A.", TipoDocumentoEnum.Cuit, "30-71234567-1", CondicionIvaEnum.ResponsableInscripto);

        // Assert
        cliente.RazonSocialONombre.Should().Be("3M Argentina S.A.");
        cliente.NumeroDocumento.Should().Be("30712345671");
    }

    [Fact]
    public void Crear_PersonaFisicaConDni_NormalizaNombreYDocumento()
    {
        // Act
        var cliente = Cliente.Crear("  PÉREZ   juan ", TipoDocumentoEnum.Dni, "12.345.678", CondicionIvaEnum.ConsumidorFinal);

        // Assert
        cliente.RazonSocialONombre.Should().Be("Pérez Juan");
        cliente.NumeroDocumento.Should().Be("12345678");
        cliente.TieneCuentaCorriente.Should().BeFalse();
    }

    [Theory]
    [InlineData("12345678", TipoDocumentoEnum.Cuit)]
    [InlineData("20123456780", TipoDocumentoEnum.Cuit)]
    [InlineData("30712345671", TipoDocumentoEnum.Cuil)]
    [InlineData("ABC1234", TipoDocumentoEnum.Dni)]
    public void Crear_DocumentoQueNoCorrespondeAlTipo_LanzaDomainException(string documento, TipoDocumentoEnum tipo)
    {
        var act = () => Cliente.Crear("Librería Central", tipo, documento, CondicionIvaEnum.ConsumidorFinal);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("a@b", null)]
    [InlineData(null, "llamar a la tarde")]
    public void Crear_ContactoInvalido_LanzaDomainException(string? email, string? telefono)
    {
        var act = () => Cliente.Crear(
            "Librería Central S.A.",
            TipoDocumentoEnum.Cuit,
            "30712345671",
            CondicionIvaEnum.ResponsableInscripto,
            telefono: telefono,
            email: email);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ActualizarDatos_EmailInvalido_NoModificaNingunDato()
    {
        // Arrange
        var cliente = Cliente.Crear("Librería Central S.A.", TipoDocumentoEnum.Cuit, "30712345671", CondicionIvaEnum.ResponsableInscripto);

        // Act: el nombre y el documento son válidos, pero el email no.
        var act = () => cliente.ActualizarDatos(
            "Otra Razón Social",
            TipoDocumentoEnum.Cuit,
            "20123456786",
            CondicionIvaEnum.Monotributo,
            null,
            null,
            "a@b");

        // Assert: el agregado valida todo antes de asignar, así que nada cambió.
        act.Should().Throw<DomainException>();
        cliente.RazonSocialONombre.Should().Be("Librería Central S.A.");
        cliente.NumeroDocumento.Should().Be("30712345671");
        cliente.CondicionIva.Should().Be(CondicionIvaEnum.ResponsableInscripto);
    }

    [Fact]
    public void HabilitarCuentaCorriente_LimiteValido_HabilitaCuentaYEstableceLimite()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear(tieneCuentaCorriente: false, limiteCredito: 0m);

        // Act
        cliente.HabilitarCuentaCorriente(50000m);

        // Assert
        cliente.TieneCuentaCorriente.Should().BeTrue();
        cliente.LimiteCredito.Should().Be(50000m);
    }

    [Fact]
    public void HabilitarCuentaCorriente_LimiteNegativo_LanzaArgumentOutOfRangeException()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear();

        // Act
        var act = () => cliente.HabilitarCuentaCorriente(-100m);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("limiteCredito");
    }

    [Fact]
    public void DeshabilitarCuentaCorriente_ConSaldoCero_DeshabilitaCuentaYReiniciaLimite()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear(tieneCuentaCorriente: true, limiteCredito: 50000m, saldoCuentaCorriente: 0m);

        // Act
        cliente.DeshabilitarCuentaCorriente();

        // Assert
        cliente.TieneCuentaCorriente.Should().BeFalse();
        cliente.LimiteCredito.Should().Be(0m);
    }

    [Fact]
    public void DeshabilitarCuentaCorriente_ConSaldoDeudor_LanzaInvalidOperationException()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear(tieneCuentaCorriente: true, limiteCredito: 50000m, saldoCuentaCorriente: 12500m);

        // Act
        var act = () => cliente.DeshabilitarCuentaCorriente();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*saldo deudor pendiente*");
    }

    [Fact]
    public void ModificarLimiteCredito_SinCuentaCorriente_LanzaCuentaCorrienteNoHabilitadaException()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear(id: 10, tieneCuentaCorriente: false);

        // Act
        var act = () => cliente.ModificarLimiteCredito(10000m);

        // Assert
        act.Should().Throw<CuentaCorrienteNoHabilitadaException>()
            .Where(e => e.ClienteId == 10);
    }

    [Fact]
    public void ModificarLimiteCredito_LimiteMenorADeudaActual_LanzaInvalidOperationException()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear(tieneCuentaCorriente: true, limiteCredito: 50000m, saldoCuentaCorriente: 30000m);

        // Act
        var act = () => cliente.ModificarLimiteCredito(20000m);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*inferior a la deuda actual*");
    }

    [Fact]
    public void ModificarLimiteCredito_ValoresValidos_ActualizaLimite()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear(tieneCuentaCorriente: true, limiteCredito: 50000m, saldoCuentaCorriente: 20000m);

        // Act
        cliente.ModificarLimiteCredito(40000m);

        // Assert
        cliente.LimiteCredito.Should().Be(40000m);
    }

    [Fact]
    public void DebitarCuentaCorriente_SinCuentaCorriente_LanzaCuentaCorrienteNoHabilitadaException()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear(id: 5, tieneCuentaCorriente: false);

        // Act
        var act = () => cliente.DebitarCuentaCorriente(1500m);

        // Assert
        act.Should().Throw<CuentaCorrienteNoHabilitadaException>()
            .Where(e => e.ClienteId == 5);
    }

    [Fact]
    public void DebitarCuentaCorriente_MontoInvalido_LanzaArgumentOutOfRangeException()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear(tieneCuentaCorriente: true, limiteCredito: 10000m);

        // Act
        var act = () => cliente.DebitarCuentaCorriente(0m);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("monto");
    }

    [Fact]
    public void DebitarCuentaCorriente_SuperaLimiteCredito_LanzaLimiteCreditoExcedidoException()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear(id: 8, tieneCuentaCorriente: true, limiteCredito: 10000m, saldoCuentaCorriente: 8000m);

        // Act
        var act = () => cliente.DebitarCuentaCorriente(3000m);

        // Assert
        act.Should().Throw<LimiteCreditoExcedidoException>()
            .Where(e => e.ClienteId == 8 && e.SaldoActual == 8000m && e.LimiteCredito == 10000m && e.MontoSolicitado == 3000m);
    }

    [Fact]
    public void DebitarCuentaCorriente_MontoValido_IncrementaSaldoDeudor()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear(tieneCuentaCorriente: true, limiteCredito: 20000m, saldoCuentaCorriente: 5000m);

        // Act
        cliente.DebitarCuentaCorriente(4500m);

        // Assert
        cliente.SaldoCuentaCorriente.Should().Be(9500m);
    }

    [Fact]
    public void AcreditarCobranza_MontoInvalido_LanzaArgumentOutOfRangeException()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear(saldoCuentaCorriente: 5000m);

        // Act
        var act = () => cliente.AcreditarCobranza(-10m);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("monto");
    }

    [Fact]
    public void AcreditarCobranza_MontoSuperiorASaldo_LanzaCobranzaExcedeDeudaException()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear(id: 12, saldoCuentaCorriente: 4000m);

        // Act
        var act = () => cliente.AcreditarCobranza(5000m);

        // Assert
        act.Should().Throw<CobranzaExcedeDeudaException>()
            .Where(e => e.ClienteId == 12 && e.SaldoDeudor == 4000m && e.MontoCobranza == 5000m);
    }

    [Fact]
    public void AcreditarCobranza_MontoValido_ReduceSaldoDeudor()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear(saldoCuentaCorriente: 7500m);

        // Act
        cliente.AcreditarCobranza(2500m);

        // Assert
        cliente.SaldoCuentaCorriente.Should().Be(5000m);
    }

    [Fact]
    public void CreditoDisponible_CalculoReactivo_CalculaCorrectamente()
    {
        // Arrange
        var clienteConCtaCte = ClientesDePrueba.Crear(tieneCuentaCorriente: true, limiteCredito: 25000m, saldoCuentaCorriente: 10000m);

        var clienteSinCtaCte = ClientesDePrueba.Crear(tieneCuentaCorriente: false, limiteCredito: 25000m, saldoCuentaCorriente: 0m);

        // Assert
        clienteConCtaCte.CreditoDisponible.Should().Be(15000m);
        clienteSinCtaCte.CreditoDisponible.Should().Be(0m);
    }

    [Fact]
    public void MarkAsDeleted_ConSaldoDeudor_LanzaInvalidOperationException()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear(saldoCuentaCorriente: 3500m);

        // Act
        var act = () => cliente.MarkAsDeleted();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*saldo deudor pendiente*");
        cliente.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void MarkAsDeleted_SinSaldoDeudor_MarcaComoEliminado()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear(saldoCuentaCorriente: 0m);

        // Act
        cliente.MarkAsDeleted();

        // Assert
        cliente.IsDeleted.Should().BeTrue();
        cliente.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public void RegistrarCobranza_ConDatosValidos_ReduceSaldoYAgregaEntidadCobranza()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear(id: 5, tieneCuentaCorriente: true, limiteCredito: 50000m, saldoCuentaCorriente: 15000m);

        // Act
        var cobranza = cliente.RegistrarCobranza(
            idTurno: 2,
            idUsuario: 3,
            monto: 6000m,
            medioPago: MedioPagoEnum.Efectivo,
            referencia: "Pago a cuenta en sucursal");

        // Assert
        cliente.SaldoCuentaCorriente.Should().Be(9000m);
        cliente.Cobranzas.Should().ContainSingle();
        cobranza.Should().NotBeNull();
        cobranza.IdCliente.Should().Be(5);
        cobranza.IdTurno.Should().Be(2);
        cobranza.IdUsuario.Should().Be(3);
        cobranza.Monto.Should().Be(6000m);
        cobranza.MedioPago.Should().Be(MedioPagoEnum.Efectivo);
        cobranza.Referencia.Should().Be("Pago a cuenta en sucursal");
        cobranza.FechaHora.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RegistrarCobranza_ConMontoMayorASaldo_LanzaCobranzaExcedeDeudaException()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear(id: 9, tieneCuentaCorriente: true, saldoCuentaCorriente: 5000m);

        // Act
        var act = () => cliente.RegistrarCobranza(1, 1, 6000m, MedioPagoEnum.TransferenciaQr);

        // Assert
        act.Should().Throw<CobranzaExcedeDeudaException>()
            .Where(e => e.ClienteId == 9 && e.SaldoDeudor == 5000m && e.MontoCobranza == 6000m);
        cliente.Cobranzas.Should().BeEmpty();
        cliente.SaldoCuentaCorriente.Should().Be(5000m);
    }

    [Fact]
    public void RegistrarCobranza_ConMontoCeroONegativo_LanzaArgumentOutOfRangeException()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear(tieneCuentaCorriente: true, saldoCuentaCorriente: 1000m);

        // Act
        var act = () => cliente.RegistrarCobranza(1, 1, 0m, MedioPagoEnum.Efectivo);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("monto");
        cliente.Cobranzas.Should().BeEmpty();
    }

    [Fact]
    public void RegistrarCobranza_ClienteSinCuentaNiDeuda_LanzaCuentaCorrienteNoHabilitadaException()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear(id: 15, tieneCuentaCorriente: false, saldoCuentaCorriente: 0m);

        // Act
        var act = () => cliente.RegistrarCobranza(1, 1, 500m, MedioPagoEnum.Efectivo);

        // Assert
        act.Should().Throw<CuentaCorrienteNoHabilitadaException>()
            .Where(e => e.ClienteId == 15);
    }
}

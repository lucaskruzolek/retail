using FluentAssertions;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

public class ClienteTests
{
    [Fact]
    public void ActualizarDatos_ValoresValidos_ActualizaPropiedades()
    {
        // Arrange
        var cliente = new Cliente
        {
            RazonSocialONombre = "Librería San Martín",
            TipoDocumento = TipoDocumentoEnum.Cuit,
            NumeroDocumento = "30-11223344-5",
            CondicionIva = CondicionIvaEnum.ResponsableInscripto
        };

        // Act
        cliente.ActualizarDatos(
            "Librería San Martín S.A.",
            TipoDocumentoEnum.Cuit,
            "30-99887766-5",
            CondicionIvaEnum.ResponsableInscripto,
            "Av. Corrientes 1234",
            "1144556677",
            "contacto@libreriasanmartin.com");

        // Assert
        cliente.RazonSocialONombre.Should().Be("Librería San Martín S.A.");
        cliente.NumeroDocumento.Should().Be("30-99887766-5");
        cliente.DomicilioFiscal.Should().Be("Av. Corrientes 1234");
        cliente.Telefono.Should().Be("1144556677");
        cliente.Email.Should().Be("contacto@libreriasanmartin.com");
    }

    [Theory]
    [InlineData("", "30-11223344-5")]
    [InlineData("   ", "30-11223344-5")]
    [InlineData("Cliente Valido", "")]
    [InlineData("Cliente Valido", "   ")]
    public void ActualizarDatos_CamposObligatoriosVacios_LanzaArgumentException(string razonSocial, string numeroDocumento)
    {
        // Arrange
        var cliente = new Cliente();

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
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void HabilitarCuentaCorriente_LimiteValido_HabilitaCuentaYEstableceLimite()
    {
        // Arrange
        var cliente = new Cliente { TieneCuentaCorriente = false, LimiteCredito = 0m };

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
        var cliente = new Cliente();

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
        var cliente = new Cliente
        {
            TieneCuentaCorriente = true,
            LimiteCredito = 50000m,
            SaldoCuentaCorriente = 0m
        };

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
        var cliente = new Cliente
        {
            TieneCuentaCorriente = true,
            LimiteCredito = 50000m,
            SaldoCuentaCorriente = 12500m
        };

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
        var cliente = new Cliente { Id = 10, TieneCuentaCorriente = false };

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
        var cliente = new Cliente
        {
            TieneCuentaCorriente = true,
            LimiteCredito = 50000m,
            SaldoCuentaCorriente = 30000m
        };

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
        var cliente = new Cliente
        {
            TieneCuentaCorriente = true,
            LimiteCredito = 50000m,
            SaldoCuentaCorriente = 20000m
        };

        // Act
        cliente.ModificarLimiteCredito(40000m);

        // Assert
        cliente.LimiteCredito.Should().Be(40000m);
    }

    [Fact]
    public void DebitarCuentaCorriente_SinCuentaCorriente_LanzaCuentaCorrienteNoHabilitadaException()
    {
        // Arrange
        var cliente = new Cliente { Id = 5, TieneCuentaCorriente = false };

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
        var cliente = new Cliente { TieneCuentaCorriente = true, LimiteCredito = 10000m };

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
        var cliente = new Cliente
        {
            Id = 8,
            TieneCuentaCorriente = true,
            LimiteCredito = 10000m,
            SaldoCuentaCorriente = 8000m
        };

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
        var cliente = new Cliente
        {
            TieneCuentaCorriente = true,
            LimiteCredito = 20000m,
            SaldoCuentaCorriente = 5000m
        };

        // Act
        cliente.DebitarCuentaCorriente(4500m);

        // Assert
        cliente.SaldoCuentaCorriente.Should().Be(9500m);
    }

    [Fact]
    public void AcreditarCobranza_MontoInvalido_LanzaArgumentOutOfRangeException()
    {
        // Arrange
        var cliente = new Cliente { SaldoCuentaCorriente = 5000m };

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
        var cliente = new Cliente { Id = 12, SaldoCuentaCorriente = 4000m };

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
        var cliente = new Cliente { SaldoCuentaCorriente = 7500m };

        // Act
        cliente.AcreditarCobranza(2500m);

        // Assert
        cliente.SaldoCuentaCorriente.Should().Be(5000m);
    }

    [Fact]
    public void CreditoDisponible_CalculoReactivo_CalculaCorrectamente()
    {
        // Arrange
        var clienteConCtaCte = new Cliente
        {
            TieneCuentaCorriente = true,
            LimiteCredito = 25000m,
            SaldoCuentaCorriente = 10000m
        };

        var clienteSinCtaCte = new Cliente
        {
            TieneCuentaCorriente = false,
            LimiteCredito = 25000m,
            SaldoCuentaCorriente = 0m
        };

        // Assert
        clienteConCtaCte.CreditoDisponible.Should().Be(15000m);
        clienteSinCtaCte.CreditoDisponible.Should().Be(0m);
    }

    [Fact]
    public void MarkAsDeleted_ConSaldoDeudor_LanzaInvalidOperationException()
    {
        // Arrange
        var cliente = new Cliente { SaldoCuentaCorriente = 3500m };

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
        var cliente = new Cliente { SaldoCuentaCorriente = 0m };

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
        var cliente = new Cliente
        {
            Id = 5,
            TieneCuentaCorriente = true,
            LimiteCredito = 50000m,
            SaldoCuentaCorriente = 15000m
        };

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
        var cliente = new Cliente
        {
            Id = 9,
            TieneCuentaCorriente = true,
            SaldoCuentaCorriente = 5000m
        };

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
        var cliente = new Cliente
        {
            TieneCuentaCorriente = true,
            SaldoCuentaCorriente = 1000m
        };

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
        var cliente = new Cliente
        {
            Id = 15,
            TieneCuentaCorriente = false,
            SaldoCuentaCorriente = 0m
        };

        // Act
        var act = () => cliente.RegistrarCobranza(1, 1, 500m, MedioPagoEnum.Efectivo);

        // Assert
        act.Should().Throw<CuentaCorrienteNoHabilitadaException>()
            .Where(e => e.ClienteId == 15);
    }
}

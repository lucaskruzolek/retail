using FluentAssertions;
using Retail.App.ViewModels.Clientes;
using Retail.Application.DTOs.Clientes;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class ClienteFormViewModelTests
{
    private readonly ClienteFormViewModel _sut = new();

    [Fact]
    public void ConfigurarAlta_InicializaEstadoPredeterminado()
    {
        // Act
        _sut.ConfigurarAlta();

        // Assert
        _sut.EsModoEdicion.Should().BeFalse();
        _sut.EsAlta.Should().BeTrue();
        _sut.IdCliente.Should().Be(0);
        _sut.RazonSocialONombre.Should().BeEmpty();
        _sut.TieneCuentaCorriente.Should().BeFalse();
        _sut.LimiteCredito.Should().Be(0m);
        _sut.TieneError.Should().BeFalse();
        _sut.TituloVentana.Should().Be("Nuevo Cliente");
    }

    [Fact]
    public void ConfigurarEdicion_CargaDatosDelClienteCorrectamente()
    {
        // Arrange
        var cliente = new ClienteDto
        {
            IdCliente = 42,
            RazonSocialONombre = "Librería Central S.A.",
            TipoDocumento = TipoDocumentoEnum.Cuit,
            NumeroDocumento = "30-55667788-9",
            CondicionIva = CondicionIvaEnum.ResponsableInscripto,
            DomicilioFiscal = "Calle Falsa 123",
            Telefono = "11-2233-4455",
            Email = "contacto@central.com",
            TieneCuentaCorriente = true,
            LimiteCredito = 75000m,
            SaldoCuentaCorriente = 15000m
        };

        // Act
        _sut.ConfigurarEdicion(cliente);

        // Assert
        _sut.EsModoEdicion.Should().BeTrue();
        _sut.EsAlta.Should().BeFalse();
        _sut.IdCliente.Should().Be(42);
        _sut.RazonSocialONombre.Should().Be("Librería Central S.A.");
        _sut.TipoDocumento.Should().Be(TipoDocumentoEnum.Cuit);
        _sut.NumeroDocumento.Should().Be("30-55667788-9");
        _sut.CondicionIva.Should().Be(CondicionIvaEnum.ResponsableInscripto);
        _sut.TieneCuentaCorriente.Should().BeTrue();
        _sut.LimiteCredito.Should().Be(75000m);
        _sut.TituloVentana.Should().Be("Modificar Cliente");
    }

    [Fact]
    public void TieneCuentaCorriente_AlDesmarcar_ReseteaLimiteCreditoACero()
    {
        // Arrange
        _sut.TieneCuentaCorriente = true;
        _sut.LimiteCredito = 50000m;

        // Act
        _sut.TieneCuentaCorriente = false;

        // Assert
        _sut.LimiteCredito.Should().Be(0m);
    }

    [Fact]
    public void TieneCuentaCorriente_AlMarcar_AsignaLimiteSugerido()
    {
        // Arrange
        _sut.TieneCuentaCorriente = false;
        _sut.LimiteCredito = 0m;

        // Act
        _sut.TieneCuentaCorriente = true;

        // Assert
        _sut.LimiteCredito.Should().Be(10000m);
    }

    [Fact]
    public void Validar_CamposObligatoriosCompletos_RetornaTrue()
    {
        // Arrange
        _sut.RazonSocialONombre = "Juan Pérez";
        _sut.NumeroDocumento = "12345678";
        _sut.TipoDocumento = TipoDocumentoEnum.Dni;
        _sut.CondicionIva = CondicionIvaEnum.ConsumidorFinal;

        // Act
        var result = _sut.Validar();

        // Assert
        result.Should().BeTrue();
        _sut.TieneError.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validar_RazonSocialVacia_RetornaFalseYFijaError(string razonSocialInvalida)
    {
        // Arrange
        _sut.RazonSocialONombre = razonSocialInvalida;
        _sut.NumeroDocumento = "12345678";

        // Act
        var result = _sut.Validar();

        // Assert
        result.Should().BeFalse();
        _sut.TieneError.Should().BeTrue();
        _sut.MensajeError.Should().Contain("razón social");
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    public void Validar_NumeroDocumentoInvalido_RetornaFalseYFijaError(string docInvalido)
    {
        // Arrange
        _sut.RazonSocialONombre = "Juan Pérez";
        _sut.NumeroDocumento = docInvalido;

        // Act
        var result = _sut.Validar();

        // Assert
        result.Should().BeFalse();
        _sut.TieneError.Should().BeTrue();
        _sut.MensajeError.Should().Contain("documento");
    }

    [Fact]
    public void GenerarCrearDto_ProduceDtoConValoresSanitizados()
    {
        // Arrange
        _sut.RazonSocialONombre = "  Papelería Belgrano  ";
        _sut.TipoDocumento = TipoDocumentoEnum.Cuit;
        _sut.NumeroDocumento = " 30-11223344-5 ";
        _sut.CondicionIva = CondicionIvaEnum.ResponsableInscripto;
        _sut.DomicilioFiscal = "  Av. Belgrano 1000  ";
        _sut.Telefono = " 11-44556677 ";
        _sut.Email = " info@belgrano.com ";
        _sut.TieneCuentaCorriente = true;
        _sut.LimiteCredito = 40000m;

        // Act
        var dto = _sut.GenerarCrearDto();

        // Assert
        dto.RazonSocialONombre.Should().Be("Papelería Belgrano");
        dto.NumeroDocumento.Should().Be("30-11223344-5");
        dto.DomicilioFiscal.Should().Be("Av. Belgrano 1000");
        dto.Telefono.Should().Be("11-44556677");
        dto.Email.Should().Be("info@belgrano.com");
        dto.TieneCuentaCorriente.Should().BeTrue();
        dto.LimiteCredito.Should().Be(40000m);
    }
}

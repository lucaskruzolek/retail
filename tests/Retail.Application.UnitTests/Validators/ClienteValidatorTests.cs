using FluentAssertions;
using Retail.Application.DTOs.Clientes;
using Retail.Application.Validators.Clientes;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.Application.UnitTests.Validators;

public class ClienteValidatorTests
{
    private readonly CrearClienteValidator _crearValidator = new();
    private readonly ActualizarClienteValidator _actualizarValidator = new();

    [Fact]
    public void CrearClienteValidator_DatosValidos_DebePasarValidacion()
    {
        // Arrange
        var dto = new CrearClienteDto
        {
            RazonSocialONombre = "Acme S.R.L.",
            TipoDocumento = TipoDocumentoEnum.Cuit,
            NumeroDocumento = "30-11223344-5",
            CondicionIva = CondicionIvaEnum.ResponsableInscripto,
            DomicilioFiscal = "Calle Falsa 123",
            Telefono = "11-2233-4455",
            Email = "contacto@acme.com",
            TieneCuentaCorriente = true,
            LimiteCredito = 50000m
        };

        // Act
        var result = _crearValidator.Validate(dto);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CrearClienteValidator_RazonSocialVacia_DebeFallar(string razonSocialInvalida)
    {
        // Arrange
        var dto = new CrearClienteDto
        {
            RazonSocialONombre = razonSocialInvalida,
            TipoDocumento = TipoDocumentoEnum.Dni,
            NumeroDocumento = "12345678",
            CondicionIva = CondicionIvaEnum.ConsumidorFinal,
            TieneCuentaCorriente = false,
            LimiteCredito = 0m
        };

        // Act
        var result = _crearValidator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CrearClienteDto.RazonSocialONombre));
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")] // Menos de 7 caracteres
    public void CrearClienteValidator_NumeroDocumentoInvalido_DebeFallar(string docInvalido)
    {
        // Arrange
        var dto = new CrearClienteDto
        {
            RazonSocialONombre = "Juan Pérez",
            TipoDocumento = TipoDocumentoEnum.Dni,
            NumeroDocumento = docInvalido,
            CondicionIva = CondicionIvaEnum.ConsumidorFinal,
            TieneCuentaCorriente = false,
            LimiteCredito = 0m
        };

        // Act
        var result = _crearValidator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CrearClienteDto.NumeroDocumento));
    }

    [Fact]
    public void CrearClienteValidator_EmailInvalido_DebeFallar()
    {
        // Arrange
        var dto = new CrearClienteDto
        {
            RazonSocialONombre = "Juan Pérez",
            TipoDocumento = TipoDocumentoEnum.Dni,
            NumeroDocumento = "12345678",
            CondicionIva = CondicionIvaEnum.ConsumidorFinal,
            Email = "correo-invalido-sin-arroba",
            TieneCuentaCorriente = false,
            LimiteCredito = 0m
        };

        // Act
        var result = _crearValidator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CrearClienteDto.Email));
    }

    [Fact]
    public void CrearClienteValidator_LimiteCreditoNegativo_DebeFallar()
    {
        // Arrange
        var dto = new CrearClienteDto
        {
            RazonSocialONombre = "Juan Pérez",
            TipoDocumento = TipoDocumentoEnum.Dni,
            NumeroDocumento = "12345678",
            CondicionIva = CondicionIvaEnum.ConsumidorFinal,
            TieneCuentaCorriente = true,
            LimiteCredito = -500m
        };

        // Act
        var result = _crearValidator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CrearClienteDto.LimiteCredito));
    }

    [Fact]
    public void ActualizarClienteValidator_DatosValidos_DebePasarValidacion()
    {
        // Arrange
        var dto = new ActualizarClienteDto
        {
            IdCliente = 1,
            RazonSocialONombre = "Acme Corp",
            TipoDocumento = TipoDocumentoEnum.Cuit,
            NumeroDocumento = "30-11223344-5",
            CondicionIva = CondicionIvaEnum.ResponsableInscripto,
            TieneCuentaCorriente = true,
            LimiteCredito = 100000m
        };

        // Act
        var result = _actualizarValidator.Validate(dto);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ActualizarClienteValidator_IdInvalido_DebeFallar(int idInvalido)
    {
        // Arrange
        var dto = new ActualizarClienteDto
        {
            IdCliente = idInvalido,
            RazonSocialONombre = "Acme Corp",
            TipoDocumento = TipoDocumentoEnum.Cuit,
            NumeroDocumento = "30-11223344-5",
            CondicionIva = CondicionIvaEnum.ResponsableInscripto,
            TieneCuentaCorriente = false,
            LimiteCredito = 0m
        };

        // Act
        var result = _actualizarValidator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ActualizarClienteDto.IdCliente));
    }
}

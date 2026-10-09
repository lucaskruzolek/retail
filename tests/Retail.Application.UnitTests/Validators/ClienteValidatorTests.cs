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
            NumeroDocumento = "30-11223344-6",
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
            NumeroDocumento = "30-11223344-6",
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
            NumeroDocumento = "30-11223344-6",
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

    private static readonly CrearClienteDto ConsumidorFinalValido = new()
    {
        RazonSocialONombre = "Pérez Juan",
        TipoDocumento = TipoDocumentoEnum.Dni,
        NumeroDocumento = "12.345.678",
        CondicionIva = CondicionIvaEnum.ConsumidorFinal
    };

    [Theory]
    [InlineData(TipoDocumentoEnum.Dni, "12345678")]
    [InlineData(TipoDocumentoEnum.Cuil, "20-12345678-6")]
    [InlineData(TipoDocumentoEnum.Pasaporte, "AAA123456")]
    public void CrearClienteValidator_PersonaFisicaConDigitosEnElNombre_DebeFallar(TipoDocumentoEnum tipo, string documento)
    {
        // Act: la crítica de la cátedra, "pueden insertarse números en los nombres".
        var result = _crearValidator.Validate(ConsumidorFinalValido with
        {
            RazonSocialONombre = "Juan123",
            TipoDocumento = tipo,
            NumeroDocumento = documento
        });

        // Assert
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CrearClienteDto.RazonSocialONombre))
            .Which.ErrorMessage.Should().Contain("persona física");
    }

    [Fact]
    public void CrearClienteValidator_EmpresaConCuitYDigitosEnLaRazonSocial_DebePasarValidacion()
    {
        var result = _crearValidator.Validate(ConsumidorFinalValido with
        {
            RazonSocialONombre = "3M Argentina S.A.",
            TipoDocumento = TipoDocumentoEnum.Cuit,
            NumeroDocumento = "30-71234567-1",
            CondicionIva = CondicionIvaEnum.ResponsableInscripto
        });

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("@@@")]
    public void CrearClienteValidator_RazonSocialSinLetras_DebeFallar(string razonSocial)
    {
        var result = _crearValidator.Validate(ConsumidorFinalValido with
        {
            RazonSocialONombre = razonSocial,
            TipoDocumento = TipoDocumentoEnum.Cuit,
            NumeroDocumento = "30712345671"
        });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CrearClienteDto.RazonSocialONombre));
    }

    [Theory]
    [InlineData(TipoDocumentoEnum.Dni, "ABCDEFG")]
    [InlineData(TipoDocumentoEnum.Dni, "123456789")]
    [InlineData(TipoDocumentoEnum.Cuit, "20-12345678-0")]
    [InlineData(TipoDocumentoEnum.Cuil, "30-71234567-1")]
    public void CrearClienteValidator_DocumentoQueNoCorrespondeAlTipo_DebeFallarConElFormatoEsperado(TipoDocumentoEnum tipo, string documento)
    {
        // Act
        var result = _crearValidator.Validate(ConsumidorFinalValido with { TipoDocumento = tipo, NumeroDocumento = documento });

        // Assert
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CrearClienteDto.NumeroDocumento))
            .Which.ErrorMessage.Should().Contain(tipo.ToString().ToUpperInvariant());
    }

    [Theory]
    [InlineData("a@b")]
    [InlineData("Nombre <a@b.com>")]
    public void CrearClienteValidator_EmailSinDominioCompletoOConNombre_DebeFallar(string email)
    {
        var result = _crearValidator.Validate(ConsumidorFinalValido with { Email = email });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CrearClienteDto.Email));
    }

    [Fact]
    public void CrearClienteValidator_TelefonoConLetras_DebeFallar()
    {
        var result = _crearValidator.Validate(ConsumidorFinalValido with { Telefono = "llamar a la tarde" });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CrearClienteDto.Telefono));
    }

    [Fact]
    public void CrearClienteValidator_ContactosOpcionalesVacios_DebePasarValidacion()
    {
        var result = _crearValidator.Validate(ConsumidorFinalValido with { Email = "  ", Telefono = "", DomicilioFiscal = null });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ActualizarClienteValidator_PersonaFisicaConDigitosEnElNombre_DebeFallarIgualQueElAlta()
    {
        // Crear y Actualizar comparten las reglas: no pueden divergir.
        var dto = new ActualizarClienteDto
        {
            IdCliente = 1,
            RazonSocialONombre = "Juan123",
            TipoDocumento = TipoDocumentoEnum.Dni,
            NumeroDocumento = "12345678",
            CondicionIva = CondicionIvaEnum.ConsumidorFinal,
            TieneCuentaCorriente = false,
            LimiteCredito = 0m
        };

        _actualizarValidator.Validate(dto).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(ActualizarClienteDto.RazonSocialONombre));
    }
}

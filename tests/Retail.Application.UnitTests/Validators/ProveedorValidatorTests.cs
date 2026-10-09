using FluentAssertions;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Validators.Proveedores;
using Xunit;

namespace Retail.Application.UnitTests.Validators;

public class ProveedorValidatorTests
{
    private readonly CrearProveedorValidator _crearValidator = new();
    private readonly ActualizarProveedorValidator _actualizarValidator = new();

    private static readonly CrearProveedorDto AltaValida = new()
    {
        RazonSocial = "López & Hnos. S.R.L.",
        Cuit = "30-71234567-1",
        Telefono = "(011) 4555-1234",
        Email = "ventas@lopez.com.ar"
    };

    [Fact]
    public void CrearProveedorValidator_DatosValidos_DebePasarValidacion()
    {
        _crearValidator.Validate(AltaValida).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("@@@")]
    [InlineData("😀 S.A.")]
    public void CrearProveedorValidator_RazonSocialSinLetrasOConSimbolos_DebeFallar(string razonSocial)
    {
        var result = _crearValidator.Validate(AltaValida with { RazonSocial = razonSocial });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CrearProveedorDto.RazonSocial));
    }

    [Theory]
    [InlineData("30-71234567-9")]
    [InlineData("11111111111")]
    [InlineData("3071234567")]
    [InlineData("30-ABCDEFGH-1")]
    public void CrearProveedorValidator_CuitInvalido_DebeFallarConElFormatoEsperado(string cuit)
    {
        // Antes solo se exigían 11 dígitos: "30-71234567-9" pasaba aunque su dígito verificador es 1 (P-2).
        var result = _crearValidator.Validate(AltaValida with { Cuit = cuit });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CrearProveedorDto.Cuit))
            .Which.ErrorMessage.Should().Contain("dígito verificador");
    }

    [Theory]
    [InlineData("a@b")]
    [InlineData("ventas@")]
    public void CrearProveedorValidator_EmailInvalido_DebeFallar(string email)
    {
        var result = _crearValidator.Validate(AltaValida with { Email = email });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CrearProveedorDto.Email));
    }

    [Fact]
    public void CrearProveedorValidator_TelefonoConLetras_DebeFallar()
    {
        var result = _crearValidator.Validate(AltaValida with { Telefono = "consultar" });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CrearProveedorDto.Telefono));
    }

    [Fact]
    public void ActualizarProveedorValidator_MismasReglasQueElAlta_RechazaCuitInvalido()
    {
        // Arrange
        var dto = new ActualizarProveedorDto
        {
            IdProveedor = 1,
            RazonSocial = "López & Hnos. S.R.L.",
            Cuit = "30-71234567-9"
        };

        // Act & Assert
        _actualizarValidator.Validate(dto).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(ActualizarProveedorDto.Cuit));
    }

    [Fact]
    public void ActualizarProveedorValidator_IdInvalido_DebeFallar()
    {
        var dto = new ActualizarProveedorDto { IdProveedor = 0, RazonSocial = "López & Hnos. S.R.L.", Cuit = "30712345671" };

        _actualizarValidator.Validate(dto).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(ActualizarProveedorDto.IdProveedor));
    }
}

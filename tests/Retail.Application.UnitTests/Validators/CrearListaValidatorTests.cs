using FluentAssertions;
using Retail.Application.DTOs.Listas;
using Retail.Application.Validators.Listas;
using Retail.Domain.Entities;
using Xunit;

namespace Retail.Application.UnitTests.Validators;

public class CrearListaValidatorTests
{
    private readonly CrearListaValidator _validator = new();

    [Fact]
    public void Validate_ListaValida_PasaLaValidacion()
    {
        // Act
        var result = _validator.Validate(CrearDtoValido());

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_NombreVacio_Falla(string nombre)
    {
        // Arrange
        var dto = CrearDtoValido() with { Nombre = nombre };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("nombre"));
    }

    [Fact]
    public void Validate_NombreDeCienCaracteresNormalizados_EsValido()
    {
        // Arrange: los espacios repetidos no cuentan, igual que en el agregado ("aaa…a b" normalizado mide 100).
        var nombre = "  " + new string('a', ListaArticulos.LongitudMaximaNombre - 2) + "   b  ";
        var dto = CrearDtoValido() with { Nombre = nombre };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NombreDeCientoUnCaracteres_Falla()
    {
        // Arrange
        var dto = CrearDtoValido() with { Nombre = new string('a', ListaArticulos.LongitudMaximaNombre + 1) };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("100"));
    }

    [Fact]
    public void Validate_SinRenglones_Falla()
    {
        // Arrange
        var dto = CrearDtoValido() with { Renglones = [] };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("al menos un artículo"));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(10, 0)]
    public void Validate_RenglonInvalido_Falla(int idArticulo, int cantidad)
    {
        // Arrange
        var dto = CrearDtoValido() with { Renglones = [new RenglonListaEntradaDto { IdArticulo = idArticulo, Cantidad = cantidad }] };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_UsuarioInvalido_Falla()
    {
        // Arrange
        var dto = CrearDtoValido() with { IdUsuario = 0 };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    private static CrearListaDto CrearDtoValido()
    {
        return new CrearListaDto
        {
            IdUsuario = 2,
            Nombre = "Útiles 1° grado",
            Renglones = [new RenglonListaEntradaDto { IdArticulo = 10, Cantidad = 2 }]
        };
    }
}

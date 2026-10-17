using FluentAssertions;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Validators.Articulos;
using Retail.Domain.Common;
using Xunit;

namespace Retail.Application.UnitTests.Validators;

public class ClasificacionValidatorTests
{
    private static readonly string NombreLargo = new('a', ReglasTexto.LongitudMaximaNombreClasificacion + 1);
    private static readonly string NombreEnElLimite = new('a', ReglasTexto.LongitudMaximaNombreClasificacion);

    [Theory]
    [InlineData("Escolar")]
    [InlineData("3M")]
    [InlineData("  Útiles   y   papelería  ")]
    public void CrearRubroValidator_NombreValido_DebePasarValidacion(string nombre)
    {
        new CrearRubroValidator().Validate(new CrearRubroDto { Nombre = nombre }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void CrearRubroValidator_NombreEnElLimite_DebePasarValidacion()
    {
        new CrearRubroValidator().Validate(new CrearRubroDto { Nombre = NombreEnElLimite }).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CrearRubroValidator_NombreVacio_DebeFallar(string nombre)
    {
        var result = new CrearRubroValidator().Validate(new CrearRubroDto { Nombre = nombre });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CrearRubroDto.Nombre));
    }

    [Fact]
    public void CrearRubroValidator_NombreMasLargoQueElMaximo_DebeFallarCitandoElLimite()
    {
        var result = new CrearRubroValidator().Validate(new CrearRubroDto { Nombre = NombreLargo });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CrearRubroDto.Nombre))
            .Which.ErrorMessage.Should().Contain("100");
    }

    [Fact]
    public void RenombrarRubroValidator_DatosValidos_DebePasarValidacion()
    {
        new RenombrarRubroValidator().Validate(new RenombrarRubroDto { IdRubro = 1, Nombre = "Escolar" }).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RenombrarRubroValidator_IdNoPositivo_DebeFallar(int id)
    {
        var result = new RenombrarRubroValidator().Validate(new RenombrarRubroDto { IdRubro = id, Nombre = "Escolar" });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(RenombrarRubroDto.IdRubro));
    }

    [Fact]
    public void RenombrarRubroValidator_NombreVacio_DebeFallar()
    {
        var result = new RenombrarRubroValidator().Validate(new RenombrarRubroDto { IdRubro = 1, Nombre = "" });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(RenombrarRubroDto.Nombre));
    }

    [Fact]
    public void RenombrarRubroValidator_NombreMasLargoQueElMaximo_DebeFallar()
    {
        var result = new RenombrarRubroValidator().Validate(new RenombrarRubroDto { IdRubro = 1, Nombre = NombreLargo });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(RenombrarRubroDto.Nombre));
    }

    [Theory]
    [InlineData("Escolar")]
    [InlineData("3M")]
    [InlineData("  Útiles   y   papelería  ")]
    public void CrearCategoriaValidator_NombreValido_DebePasarValidacion(string nombre)
    {
        new CrearCategoriaValidator().Validate(new CrearCategoriaDto { Nombre = nombre }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void CrearCategoriaValidator_NombreEnElLimite_DebePasarValidacion()
    {
        new CrearCategoriaValidator().Validate(new CrearCategoriaDto { Nombre = NombreEnElLimite }).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CrearCategoriaValidator_NombreVacio_DebeFallar(string nombre)
    {
        var result = new CrearCategoriaValidator().Validate(new CrearCategoriaDto { Nombre = nombre });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CrearCategoriaDto.Nombre));
    }

    [Fact]
    public void CrearCategoriaValidator_NombreMasLargoQueElMaximo_DebeFallarCitandoElLimite()
    {
        var result = new CrearCategoriaValidator().Validate(new CrearCategoriaDto { Nombre = NombreLargo });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CrearCategoriaDto.Nombre))
            .Which.ErrorMessage.Should().Contain("100");
    }

    [Fact]
    public void RenombrarCategoriaValidator_DatosValidos_DebePasarValidacion()
    {
        new RenombrarCategoriaValidator().Validate(new RenombrarCategoriaDto { IdCategoria = 1, Nombre = "Escolar" }).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RenombrarCategoriaValidator_IdNoPositivo_DebeFallar(int id)
    {
        var result = new RenombrarCategoriaValidator().Validate(new RenombrarCategoriaDto { IdCategoria = id, Nombre = "Escolar" });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(RenombrarCategoriaDto.IdCategoria));
    }

    [Fact]
    public void RenombrarCategoriaValidator_NombreVacio_DebeFallar()
    {
        var result = new RenombrarCategoriaValidator().Validate(new RenombrarCategoriaDto { IdCategoria = 1, Nombre = "" });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(RenombrarCategoriaDto.Nombre));
    }

    [Fact]
    public void RenombrarCategoriaValidator_NombreMasLargoQueElMaximo_DebeFallar()
    {
        var result = new RenombrarCategoriaValidator().Validate(new RenombrarCategoriaDto { IdCategoria = 1, Nombre = NombreLargo });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(RenombrarCategoriaDto.Nombre));
    }

    [Theory]
    [InlineData("Escolar")]
    [InlineData("3M")]
    [InlineData("  Útiles   y   papelería  ")]
    public void CrearMarcaValidator_NombreValido_DebePasarValidacion(string nombre)
    {
        new CrearMarcaValidator().Validate(new CrearMarcaDto { Nombre = nombre }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void CrearMarcaValidator_NombreEnElLimite_DebePasarValidacion()
    {
        new CrearMarcaValidator().Validate(new CrearMarcaDto { Nombre = NombreEnElLimite }).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CrearMarcaValidator_NombreVacio_DebeFallar(string nombre)
    {
        var result = new CrearMarcaValidator().Validate(new CrearMarcaDto { Nombre = nombre });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CrearMarcaDto.Nombre));
    }

    [Fact]
    public void CrearMarcaValidator_NombreMasLargoQueElMaximo_DebeFallarCitandoElLimite()
    {
        var result = new CrearMarcaValidator().Validate(new CrearMarcaDto { Nombre = NombreLargo });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CrearMarcaDto.Nombre))
            .Which.ErrorMessage.Should().Contain("100");
    }

    [Fact]
    public void RenombrarMarcaValidator_DatosValidos_DebePasarValidacion()
    {
        new RenombrarMarcaValidator().Validate(new RenombrarMarcaDto { IdMarca = 1, Nombre = "Escolar" }).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RenombrarMarcaValidator_IdNoPositivo_DebeFallar(int id)
    {
        var result = new RenombrarMarcaValidator().Validate(new RenombrarMarcaDto { IdMarca = id, Nombre = "Escolar" });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(RenombrarMarcaDto.IdMarca));
    }

    [Fact]
    public void RenombrarMarcaValidator_NombreVacio_DebeFallar()
    {
        var result = new RenombrarMarcaValidator().Validate(new RenombrarMarcaDto { IdMarca = 1, Nombre = "" });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(RenombrarMarcaDto.Nombre));
    }

    [Fact]
    public void RenombrarMarcaValidator_NombreMasLargoQueElMaximo_DebeFallar()
    {
        var result = new RenombrarMarcaValidator().Validate(new RenombrarMarcaDto { IdMarca = 1, Nombre = NombreLargo });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(RenombrarMarcaDto.Nombre));
    }
}

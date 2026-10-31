using FluentAssertions;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Validators.Proveedores;
using Xunit;

namespace Retail.Application.UnitTests.Validators;

public class ConsultaCatalogoProveedorValidatorTests
{
    private readonly ConsultaCatalogoProveedorValidator _validator = new();

    [Fact]
    public void Validate_SinFiltros_DebePasarValidacion()
    {
        _validator.Validate(new ConsultaCatalogoProveedorDto()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_CategoriaYMarcaValidas_DebePasarValidacion()
    {
        var consulta = new ConsultaCatalogoProveedorDto { IdProveedor = 1, IdCategoria = 2, IdMarca = 3 };

        _validator.Validate(consulta).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_SoloSinCategoriaConMarca_DebePasarValidacion()
    {
        var consulta = new ConsultaCatalogoProveedorDto { SoloSinCategoria = true, IdMarca = 3 };

        _validator.Validate(consulta).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_IdProveedorNoPositivo_DebeFallar(int idProveedor)
    {
        var result = _validator.Validate(new ConsultaCatalogoProveedorDto { IdProveedor = idProveedor });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(ConsultaCatalogoProveedorDto.IdProveedor));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_IdCategoriaNoPositivo_DebeFallar(int idCategoria)
    {
        var result = _validator.Validate(new ConsultaCatalogoProveedorDto { IdCategoria = idCategoria });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(ConsultaCatalogoProveedorDto.IdCategoria));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_IdMarcaNoPositivo_DebeFallar(int idMarca)
    {
        var result = _validator.Validate(new ConsultaCatalogoProveedorDto { IdMarca = idMarca });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(ConsultaCatalogoProveedorDto.IdMarca));
    }

    [Fact]
    public void Validate_SoloSinCategoriaConCategoria_DebeFallar()
    {
        var result = _validator.Validate(new ConsultaCatalogoProveedorDto { SoloSinCategoria = true, IdCategoria = 2 });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(ConsultaCatalogoProveedorDto.SoloSinCategoria));
    }
}

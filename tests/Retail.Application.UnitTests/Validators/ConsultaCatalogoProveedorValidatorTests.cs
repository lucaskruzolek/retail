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
    public void Validate_CategoriasYMarcasValidas_DebePasarValidacion()
    {
        var consulta = new ConsultaCatalogoProveedorDto { IdProveedor = 1, IdsCategoria = [2, 5], IdsMarca = [3] };

        _validator.Validate(consulta).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_CategoriaYCentinelaSinCategoriaJuntos_DebePasarValidacion()
    {
        // M-78: con listas, "Útiles + Sin categoría" es una unión válida.
        var consulta = new ConsultaCatalogoProveedorDto
        {
            IdsCategoria = [2, ConsultaCatalogoProveedorDto.IdSinCategoria],
            IdsMarca = [ConsultaCatalogoProveedorDto.IdSinMarca]
        };

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
    [InlineData(-2)]
    public void Validate_IdDeCategoriaInvalido_DebeFallar(int idCategoria)
    {
        var result = _validator.Validate(new ConsultaCatalogoProveedorDto { IdsCategoria = [2, idCategoria] });

        result.Errors.Should().ContainSingle().Which.PropertyName.Should().StartWith(nameof(ConsultaCatalogoProveedorDto.IdsCategoria));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void Validate_IdDeMarcaInvalido_DebeFallar(int idMarca)
    {
        var result = _validator.Validate(new ConsultaCatalogoProveedorDto { IdsMarca = [idMarca] });

        result.Errors.Should().ContainSingle().Which.PropertyName.Should().StartWith(nameof(ConsultaCatalogoProveedorDto.IdsMarca));
    }
}

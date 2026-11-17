using FluentAssertions;
using Retail.Application.DTOs.Presupuestos;
using Retail.Application.Validators.Presupuestos;
using Retail.Domain.Entities;
using Xunit;

namespace Retail.Application.UnitTests.Validators;

public class CrearPresupuestoValidatorTests
{
    private readonly CrearPresupuestoValidator _validator = new();

    [Fact]
    public void Validate_DtoCompleto_EsValido()
    {
        var resultado = _validator.Validate(CrearDtoValido() with
        {
            IdCliente = 4,
            Titulo = "Lista escolar",
            Descuento = 100m,
            VigenciaDias = 30
        });

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DtoMinimo_EsValidoConVigenciaPorDefecto()
    {
        var dto = CrearDtoValido();

        _validator.Validate(dto).IsValid.Should().BeTrue();
        dto.VigenciaDias.Should().Be(Presupuesto.VigenciaPorDefectoDias);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_UsuarioNoPositivo_Falla(int idUsuario)
    {
        _validator.Validate(CrearDtoValido() with { IdUsuario = idUsuario }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_ClienteNoPositivo_Falla(int idCliente)
    {
        _validator.Validate(CrearDtoValido() with { IdCliente = idCliente }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_TituloDeLongitudMaxima_EsValido()
    {
        var titulo = new string('a', Presupuesto.LongitudMaximaTitulo);

        _validator.Validate(CrearDtoValido() with { Titulo = titulo }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_TituloQueSuperaElMaximo_Falla()
    {
        var titulo = new string('a', Presupuesto.LongitudMaximaTitulo + 1);

        _validator.Validate(CrearDtoValido() with { Titulo = titulo }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_SinItems_Falla()
    {
        _validator.Validate(CrearDtoValido() with { Items = [] }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ItemConArticuloInvalido_Falla()
    {
        var dto = CrearDtoValido() with { Items = [Item() with { IdArticulo = 0 }] };

        _validator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ItemConCantidadCero_Falla()
    {
        var dto = CrearDtoValido() with { Items = [Item() with { Cantidad = 0 }] };

        _validator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ItemConPrecioNegativo_Falla()
    {
        var dto = CrearDtoValido() with { Items = [Item() with { PrecioUnitarioPactado = -1m }] };

        _validator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ItemConPrecioCero_EsValido()
    {
        var dto = CrearDtoValido() with { Items = [Item() with { PrecioUnitarioPactado = 0m }] };

        _validator.Validate(dto).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DescuentoNegativo_Falla()
    {
        _validator.Validate(CrearDtoValido() with { Descuento = -1m }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(Presupuesto.VigenciaMinimaDias)]
    [InlineData(Presupuesto.VigenciaMaximaDias)]
    public void Validate_VigenciaEnLosLimites_EsValido(int dias)
    {
        _validator.Validate(CrearDtoValido() with { VigenciaDias = dias }).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(Presupuesto.VigenciaMinimaDias - 1)]
    [InlineData(Presupuesto.VigenciaMaximaDias + 1)]
    public void Validate_VigenciaFueraDeRango_Falla(int dias)
    {
        _validator.Validate(CrearDtoValido() with { VigenciaDias = dias }).IsValid.Should().BeFalse();
    }

    private static DetallePresupuestoDto Item()
    {
        return new DetallePresupuestoDto
        {
            IdArticulo = 10,
            Descripcion = "Cuaderno",
            Cantidad = 2,
            PrecioUnitarioPactado = 1500m
        };
    }

    private static CrearPresupuestoDto CrearDtoValido()
    {
        return new CrearPresupuestoDto
        {
            IdUsuario = 2,
            Items = [Item()]
        };
    }
}

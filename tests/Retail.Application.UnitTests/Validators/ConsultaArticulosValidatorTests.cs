using FluentAssertions;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Validators.Articulos;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.Application.UnitTests.Validators;

public class ConsultaArticulosValidatorTests
{
    private readonly ConsultaArticulosValidator _validator = new();

    [Fact]
    public void Validate_SinFiltros_EsValido()
    {
        var resultado = _validator.Validate(new ConsultaArticulosDto());

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ConVariosValoresEnCadaFiltro_EsValido()
    {
        var resultado = _validator.Validate(new ConsultaArticulosDto
        {
            IdsCategoria = [3, 7],
            IdsMarca = [2],
            IdsProveedor = [ConsultaArticulosDto.IdProveedorManual, 12],
            EstadosStock = [FiltroStockArticuloEnum.SinStock, FiltroStockArticuloEnum.StockBajo]
        });

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_CategoriaNoPositiva_Falla()
    {
        var resultado = _validator.Validate(new ConsultaArticulosDto { IdsCategoria = [3, 0] });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage == "La categoría no es válida.");
    }

    [Fact]
    public void Validate_MarcaNegativa_Falla()
    {
        var resultado = _validator.Validate(new ConsultaArticulosDto { IdsMarca = [-1] });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage == "La marca no es válida.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void Validate_DistribuidorNiPositivoNiManual_Falla(int idProveedor)
    {
        var resultado = _validator.Validate(new ConsultaArticulosDto { IdsProveedor = [idProveedor] });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage == "El distribuidor no es válido.");
    }

    [Fact]
    public void Validate_EstadoDeStockDesconocido_Falla()
    {
        var resultado = _validator.Validate(new ConsultaArticulosDto { EstadosStock = [(FiltroStockArticuloEnum)99] });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage == "El estado de stock no es válido.");
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, ConsultaArticulosValidator.TamanoPaginaMaximo + 1)]
    public void Validate_PaginacionFueraDeRango_Falla(int pagina, int tamanoPagina)
    {
        var resultado = _validator.Validate(new ConsultaArticulosDto { Pagina = pagina, TamanoPagina = tamanoPagina });

        resultado.IsValid.Should().BeFalse();
    }
}

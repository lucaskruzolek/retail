using FluentAssertions;
using Retail.Application.DTOs.Reportes;
using Retail.Application.UnitTests.TestData;
using Retail.Application.Validators.Reportes;
using Xunit;

namespace Retail.Application.UnitTests.Validators.Reportes;

public class FiltrosReporteValidatorTests
{
    // Hoy local: 15/10/2026 (el reloj marca las 12:00 en Argentina).
    private static readonly DateOnly Hoy = new(2026, 10, 15);
    private readonly RelojDePrueba _reloj = RelojDePrueba.EnQuinceDeOctubre();

    [Fact]
    public void FiltroPeriodo_ConDesdePosteriorAHasta_Falla()
    {
        var resultado = new FiltroPeriodoValidator(_reloj).Validate(
            new FiltroPeriodoDto { Desde = Hoy, Hasta = Hoy.AddDays(-1) });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage == "La fecha desde no puede ser posterior a la fecha hasta.");
    }

    [Fact]
    public void FiltroPeriodo_ConHastaMañana_Falla()
    {
        var resultado = new FiltroPeriodoValidator(_reloj).Validate(
            new FiltroPeriodoDto { Desde = Hoy, Hasta = Hoy.AddDays(1) });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage == "La fecha hasta no puede ser posterior a hoy.");
    }

    [Fact]
    public void FiltroPeriodo_ConHastaHoy_EsValido()
    {
        var resultado = new FiltroPeriodoValidator(_reloj).Validate(
            new FiltroPeriodoDto { Desde = Hoy.AddDays(-30), Hasta = Hoy });

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void FiltroPeriodo_CercaDeLaMedianocheUtcPeroAunHoyEnLocal_UsaElDiaLocal()
    {
        // 02:00 UTC del 16/10 son las 23:00 del 15/10 en Argentina: hoy local sigue siendo el 15.
        var reloj = new RelojDePrueba(new DateTimeOffset(2026, 10, 16, 2, 0, 0, TimeSpan.Zero));

        var resultado = new FiltroPeriodoValidator(reloj).Validate(new FiltroPeriodoDto { Desde = Hoy, Hasta = Hoy });

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void FiltroVentasPorPeriodo_ConAgrupacionInvalida_Falla()
    {
        var resultado = new FiltroVentasPorPeriodoValidator(_reloj).Validate(
            new FiltroVentasPorPeriodoDto { Desde = Hoy, Hasta = Hoy, Agrupacion = (AgrupacionTemporalEnum)99 });

        resultado.IsValid.Should().BeFalse();
    }

    [Fact]
    public void FiltroVentasPorPeriodo_ConCajeroCero_Falla()
    {
        var resultado = new FiltroVentasPorPeriodoValidator(_reloj).Validate(
            new FiltroVentasPorPeriodoDto { Desde = Hoy, Hasta = Hoy, IdUsuario = 0 });

        resultado.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void FiltroRanking_ConCantidadFueraDeRango_Falla(int cantidad)
    {
        var resultado = new FiltroRankingValidator(_reloj).Validate(
            new FiltroRankingDto { Desde = Hoy, Hasta = Hoy, Cantidad = cantidad });

        resultado.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void FiltroRanking_ConCantidadEnLosLimites_EsValido(int cantidad)
    {
        var resultado = new FiltroRankingValidator(_reloj).Validate(
            new FiltroRankingDto { Desde = Hoy, Hasta = Hoy, Cantidad = cantidad });

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void FiltroRanking_ConCriterioInvalido_Falla()
    {
        var resultado = new FiltroRankingValidator(_reloj).Validate(
            new FiltroRankingDto { Desde = Hoy, Hasta = Hoy, Criterio = (CriterioRankingEnum)99 });

        resultado.IsValid.Should().BeFalse();
    }

    [Fact]
    public void FiltroRanking_ConCategoriaCero_Falla()
    {
        var resultado = new FiltroRankingValidator(_reloj).Validate(
            new FiltroRankingDto { Desde = Hoy, Hasta = Hoy, IdCategoria = 0 });

        resultado.IsValid.Should().BeFalse();
    }

    [Fact]
    public void FiltroRentabilidad_ConAgrupacionInvalida_Falla()
    {
        var resultado = new FiltroRentabilidadValidator(_reloj).Validate(
            new FiltroRentabilidadDto { Desde = Hoy, Hasta = Hoy, AgruparPor = (AgrupacionRentabilidadEnum)99 });

        resultado.IsValid.Should().BeFalse();
    }

    [Fact]
    public void FiltroRentabilidad_ConMarcaCero_Falla()
    {
        var resultado = new FiltroRentabilidadValidator(_reloj).Validate(
            new FiltroRentabilidadDto { Desde = Hoy, Hasta = Hoy, IdMarca = 0 });

        resultado.IsValid.Should().BeFalse();
    }

    [Fact]
    public void FiltroInventario_ConIdsCero_Falla()
    {
        var resultado = new FiltroInventarioValidator().Validate(new FiltroInventarioDto { IdCategoria = 0, IdMarca = 0 });

        resultado.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void FiltroInventario_SinFiltros_EsValido()
    {
        new FiltroInventarioValidator().Validate(new FiltroInventarioDto()).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3651)]
    public void FiltroSinMovimiento_ConDiasFueraDeRango_Falla(int dias)
    {
        var resultado = new FiltroSinMovimientoValidator().Validate(new FiltroSinMovimientoDto { Dias = dias });

        resultado.IsValid.Should().BeFalse();
    }

    [Fact]
    public void FiltroSinMovimiento_ConCategoriaCero_Falla()
    {
        var resultado = new FiltroSinMovimientoValidator().Validate(new FiltroSinMovimientoDto { IdCategoria = 0 });

        resultado.IsValid.Should().BeFalse();
    }
}

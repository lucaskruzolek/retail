using FluentAssertions;
using Retail.Application.DTOs.Presupuestos;
using Retail.Application.Validators.Presupuestos;
using Xunit;

namespace Retail.Application.UnitTests.Validators;

public class ConsultaPresupuestosValidatorTests
{
    private readonly ConsultaPresupuestosValidator _validator = new();

    [Fact]
    public void Validate_SinFiltros_EsValido()
    {
        _validator.Validate(new ConsultaPresupuestosDto()).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(FiltroEstadoPresupuestoEnum.Todos)]
    [InlineData(FiltroEstadoPresupuestoEnum.Vigentes)]
    [InlineData(FiltroEstadoPresupuestoEnum.Vencidos)]
    [InlineData(FiltroEstadoPresupuestoEnum.Convertidos)]
    [InlineData(FiltroEstadoPresupuestoEnum.Anulados)]
    public void Validate_CadaFiltroDefinido_EsValido(FiltroEstadoPresupuestoEnum filtro)
    {
        _validator.Validate(new ConsultaPresupuestosDto { Filtro = filtro }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_FiltroInexistente_Falla()
    {
        _validator.Validate(new ConsultaPresupuestosDto { Filtro = (FiltroEstadoPresupuestoEnum)99 }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_PaginaCero_Falla()
    {
        _validator.Validate(new ConsultaPresupuestosDto { Pagina = 0 }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ConsultaPresupuestosValidator.TamanoPaginaMaximo + 1)]
    public void Validate_TamanoDePaginaFueraDeRango_Falla(int tamano)
    {
        _validator.Validate(new ConsultaPresupuestosDto { TamanoPagina = tamano }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_TamanoDePaginaMaximo_EsValido()
    {
        var consulta = new ConsultaPresupuestosDto { TamanoPagina = ConsultaPresupuestosValidator.TamanoPaginaMaximo };

        _validator.Validate(consulta).IsValid.Should().BeTrue();
    }
}

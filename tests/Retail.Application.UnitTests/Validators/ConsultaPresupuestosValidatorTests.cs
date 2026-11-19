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
    [InlineData(FiltroEstadoPresupuestoEnum.Vigentes)]
    [InlineData(FiltroEstadoPresupuestoEnum.Vencidos)]
    [InlineData(FiltroEstadoPresupuestoEnum.Convertidos)]
    [InlineData(FiltroEstadoPresupuestoEnum.Anulados)]
    public void Validate_CadaFiltroDefinido_EsValido(FiltroEstadoPresupuestoEnum filtro)
    {
        _validator.Validate(new ConsultaPresupuestosDto { Estados = [filtro] }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_VariosEstadosDefinidos_EsValido()
    {
        var consulta = new ConsultaPresupuestosDto { Estados = [FiltroEstadoPresupuestoEnum.Vigentes, FiltroEstadoPresupuestoEnum.Anulados] };

        _validator.Validate(consulta).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EstadoInexistente_Falla()
    {
        _validator.Validate(new ConsultaPresupuestosDto { Estados = [(FiltroEstadoPresupuestoEnum)99] }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Validate_IdDeClienteNoPositivo_Falla(int idCliente)
    {
        _validator.Validate(new ConsultaPresupuestosDto { IdsCliente = [idCliente] }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ClientesYConsumidorFinal_EsValido()
    {
        _validator.Validate(new ConsultaPresupuestosDto { IdsCliente = [1, 2], IncluirConsumidorFinal = true }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RangoDeEmisionAlReves_Falla()
    {
        var consulta = new ConsultaPresupuestosDto
        {
            EmisionDesdeUtc = new DateTime(2026, 11, 10, 3, 0, 0, DateTimeKind.Utc),
            EmisionHastaUtc = new DateTime(2026, 11, 9, 3, 0, 0, DateTimeKind.Utc)
        };

        _validator.Validate(consulta).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_RangoDeEmisionConUnSoloExtremo_EsValido()
    {
        var consulta = new ConsultaPresupuestosDto { EmisionDesdeUtc = new DateTime(2026, 11, 10, 3, 0, 0, DateTimeKind.Utc) };

        _validator.Validate(consulta).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RangoDeVencimientoAlReves_Falla()
    {
        var consulta = new ConsultaPresupuestosDto { VenceDesde = new DateOnly(2026, 11, 10), VenceHasta = new DateOnly(2026, 11, 9) };

        _validator.Validate(consulta).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_RangoDeVencimientoDeUnSoloDia_EsValido()
    {
        var consulta = new ConsultaPresupuestosDto { VenceDesde = new DateOnly(2026, 11, 10), VenceHasta = new DateOnly(2026, 11, 10) };

        _validator.Validate(consulta).IsValid.Should().BeTrue();
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

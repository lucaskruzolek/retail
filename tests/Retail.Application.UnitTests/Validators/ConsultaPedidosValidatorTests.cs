using FluentAssertions;
using Retail.Application.DTOs.Pedidos;
using Retail.Application.Validators.Pedidos;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.Application.UnitTests.Validators;

public class ConsultaPedidosValidatorTests
{
    private readonly ConsultaPedidosValidator _validator = new();

    [Fact]
    public void Validate_SinFiltros_EsValido()
    {
        var resultado = _validator.Validate(new ConsultaPedidosDto());

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ConVariosValoresEnCadaFiltro_EsValido()
    {
        var resultado = _validator.Validate(new ConsultaPedidosDto
        {
            Estados = [EstadoPedidoEnum.Borrador, EstadoPedidoEnum.Cancelado],
            IdsProveedor = [1, 5]
        });

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EstadoInexistente_Falla()
    {
        var resultado = _validator.Validate(new ConsultaPedidosDto { Estados = [(EstadoPedidoEnum)99] });

        resultado.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ProveedorNoPositivo_Falla()
    {
        var resultado = _validator.Validate(new ConsultaPedidosDto { IdsProveedor = [2, 0] });

        resultado.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, ConsultaPedidosValidator.TamanoPaginaMaximo + 1)]
    public void Validate_PaginacionFueraDeRango_Falla(int pagina, int tamano)
    {
        var resultado = _validator.Validate(new ConsultaPedidosDto { Pagina = pagina, TamanoPagina = tamano });

        resultado.IsValid.Should().BeFalse();
    }
}

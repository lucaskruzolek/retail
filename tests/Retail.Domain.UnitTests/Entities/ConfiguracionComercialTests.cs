using FluentAssertions;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

public class ConfiguracionComercialTests
{
    [Fact]
    public void Constructor_SinCambios_RedondeaAlEnteroMasCercano()
    {
        var configuracion = new ConfiguracionComercial();

        configuracion.RedondeoPrecio.Should().Be(RedondeoPrecioEnum.EnteroMasCercano);
    }

    [Fact]
    public void CambiarRedondeoPrecio_PoliticaValida_LaAsigna()
    {
        var configuracion = new ConfiguracionComercial();

        configuracion.CambiarRedondeoPrecio(RedondeoPrecioEnum.MultiploDe10Superior);

        configuracion.RedondeoPrecio.Should().Be(RedondeoPrecioEnum.MultiploDe10Superior);
    }

    [Fact]
    public void CambiarRedondeoPrecio_PoliticaInexistente_LanzaDomainExceptionSinCambiar()
    {
        var configuracion = new ConfiguracionComercial();

        var accion = () => configuracion.CambiarRedondeoPrecio((RedondeoPrecioEnum)99);

        accion.Should().Throw<DomainException>();
        configuracion.RedondeoPrecio.Should().Be(RedondeoPrecioEnum.EnteroMasCercano);
    }
}

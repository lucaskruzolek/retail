using NSubstitute;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;

namespace Retail.Infrastructure.IntegrationTests.TestData;

/// <summary>
/// Política de redondeo fija para las pruebas que no verifican la configuración comercial (D-38): evita leer la
/// tabla de configuración y mantiene el costo de las consultas que miden algunos tests de rendimiento.
/// </summary>
internal static class ConfiguracionComercialDePrueba
{
    public static IConfiguracionComercialService ConRedondeoEntero()
    {
        var configuracion = Substitute.For<IConfiguracionComercialService>();
        configuracion.ObtenerRedondeoPrecioAsync(Arg.Any<CancellationToken>()).Returns(RedondeoPrecioEnum.EnteroMasCercano);
        return configuracion;
    }
}

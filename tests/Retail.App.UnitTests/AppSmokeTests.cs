using FluentAssertions;
using Xunit;

namespace Retail.App.UnitTests;

public class AppSmokeTests
{
    [Fact]
    public void AppProject_DebeEstarConfiguradoCorrectamente()
    {
        // Smoke test inicial para confirmar que el arquetipo de test WPF compila y ejecuta
        const bool proyectoConfigurado = true;
        proyectoConfigurado.Should().BeTrue();
    }
}

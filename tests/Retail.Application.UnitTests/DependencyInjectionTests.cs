using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Retail.Application;
using Xunit;

namespace Retail.Application.UnitTests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplicationServices_DebeRetornarServiceCollectionValido()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddApplicationServices();

        // Assert
        result.Should().BeSameAs(services);
        services.Should().Contain(d => d.ServiceType == typeof(Retail.Application.Interfaces.Services.IUsuarioService));
        services.Should().Contain(d => d.ServiceType == typeof(FluentValidation.IValidator<Retail.Application.DTOs.Usuarios.CrearUsuarioDto>));
    }

    [Fact]
    public void AddApplicationServices_NoRegistraLasOperacionesPrivilegiadasDeAdministracion()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApplicationServices();

        // Assert: la aplicación WPF no puede resolver las operaciones sin actor (migrar, primer Gerente).
        services.Should().Contain(d => d.ServiceType == typeof(Retail.Application.Interfaces.Services.IEstadoSistemaService));
        services.Should().NotContain(d => d.ServiceType == typeof(Retail.Application.Interfaces.Services.IAdministracionSistemaService));
    }

    [Fact]
    public void AddAdministracionSistema_RegistraLasOperacionesPrivilegiadas()
    {
        var services = new ServiceCollection();

        services.AddAdministracionSistema();

        services.Should().Contain(d => d.ServiceType == typeof(Retail.Application.Interfaces.Services.IAdministracionSistemaService));
    }
}

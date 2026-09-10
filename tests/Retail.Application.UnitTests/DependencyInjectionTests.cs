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
}


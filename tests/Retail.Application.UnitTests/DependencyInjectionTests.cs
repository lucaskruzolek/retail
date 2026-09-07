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
    }
}

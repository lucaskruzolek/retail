using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Retail.Infrastructure;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructureServices_DebeRetornarServiceCollectionValido()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = NSubstitute.Substitute.For<IConfiguration>();

        // Act
        var result = services.AddInfrastructureServices(configuration);

        // Assert
        result.Should().BeSameAs(services);
    }
}

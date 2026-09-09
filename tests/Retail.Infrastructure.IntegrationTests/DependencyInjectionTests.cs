using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Infrastructure.ExternalServices.ArcaSdk;
using Retail.Infrastructure.Hardware;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructureServices_DebeRetornarServiceCollectionValido()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var result = services.AddInfrastructureServices(configuration);

        // Assert
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddInfrastructureServices_ConUseMockArcaTrue_RegistraMockArcaClientYPrinterService()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["ArcaSettings:UseMockArca"] = "true",
            ["ArcaSettings:BaseUrl"] = "http://localhost:8080",
            ["ArcaSettings:TimeoutSeconds"] = "10",
            ["TicketPrinterSettings:OutputDirectory"] = "test-tickets"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddInfrastructureServices(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        var printerService = provider.GetService<ITicketPrinterService>();
        printerService.Should().NotBeNull();
        printerService.Should().BeOfType<FileDebugTicketPrinterService>();

        var arcaClient = provider.GetService<IArcaClient>();
        arcaClient.Should().NotBeNull();
        arcaClient.Should().BeOfType<MockArcaClient>();
    }

    [Fact]
    public void AddInfrastructureServices_ConUseMockArcaFalse_RegistraArcaClientReal()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["ArcaSettings:UseMockArca"] = "false",
            ["ArcaSettings:BaseUrl"] = "http://localhost:8080",
            ["ArcaSettings:TimeoutSeconds"] = "10"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddInfrastructureServices(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        var arcaClient = provider.GetService<IArcaClient>();
        arcaClient.Should().NotBeNull();
        arcaClient.Should().BeOfType<ArcaClient>();
    }
}

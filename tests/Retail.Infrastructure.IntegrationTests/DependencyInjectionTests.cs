using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Retail.Application;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Infrastructure.ExternalServices.ArcaSdk;
using Retail.Infrastructure.Hardware;
using Retail.Infrastructure.Persistence.Services;
using Retail.Infrastructure.Presupuestos;
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

    [Fact]
    public void AddInfrastructureServices_RegistraPersistenciaYSeguridadCorrectamente()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = ServidorDePruebas.CadenaDeConexion("RetailDb_DiTest"),
            ["ArcaSettings:UseMockArca"] = "true"
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
        provider.GetService<Retail.Application.Interfaces.Infrastructure.IPasswordHasher>().Should().NotBeNull();
        provider.GetService<Retail.Infrastructure.Persistence.Context.RetailDbContext>().Should().NotBeNull();
        provider.GetService<Retail.Application.Interfaces.Persistence.IRetailDbContext>().Should().NotBeNull();
        provider.GetService<Retail.Application.Interfaces.Persistence.IUnitOfWork>().Should().NotBeNull();
        provider.GetService<Retail.Application.Interfaces.Persistence.IRepository<Retail.Domain.Entities.Articulo>>().Should().NotBeNull();
    }

    [Fact]
    public void AddInfrastructureServices_ConAplicacion_ResuelveVentaYPresupuestoConElContenedorReal()
    {
        // Arrange: el contenedor real con Aplicación e Infraestructura; ValidateOnBuild detecta dependencias sin registrar
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ServidorDePruebas.CadenaDeConexion("RetailDb_DiTest"),
                ["ArcaSettings:UseMockArca"] = "true"
            })
            .Build();

        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddApplicationServices();
        servicios.AddInfrastructureServices(configuracion);
        using var proveedor = servicios.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var alcance = proveedor.CreateScope();

        // Act
        var presupuestos = alcance.ServiceProvider.GetService<IPresupuestoService>();
        var ventas = alcance.ServiceProvider.GetService<IVentaService>();

        // Assert
        presupuestos.Should().NotBeNull();
        ventas.Should().NotBeNull();
        alcance.ServiceProvider.GetService<IPresupuestoQueryService>().Should().BeOfType<PresupuestoQueryService>();
        proveedor.GetService<IExportadorPresupuestoPdf>().Should().BeOfType<ExportadorPresupuestoPdf>();
    }
}

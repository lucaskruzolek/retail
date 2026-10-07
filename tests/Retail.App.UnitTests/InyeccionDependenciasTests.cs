using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Retail.App.Services;
using Retail.Infrastructure.Persistence.Context;
using Xunit;

namespace Retail.App.UnitTests;

/// <summary>
/// Valida la configuración real de inyección de dependencias de la aplicación (App.RegistrarServicios) con
/// las mismas opciones que usa el Host: ValidateScopes y ValidateOnBuild (H-19).
/// </summary>
public class InyeccionDependenciasTests
{
    [Fact]
    public void RegistrarServicios_ConValidacionDeCiclosDeVida_ConstruyeElProveedorSinErrores()
    {
        // Act
        var construir = () => ConstruirProveedor();

        // Assert: ValidateOnBuild falla si un Singleton depende de un servicio Scoped o falta un registro
        construir.Should().NotThrow();
    }

    [Fact]
    public void RegistrarServicios_DosScopes_ResuelvenDbContextDistintos()
    {
        // Arrange
        using var provider = ConstruirProveedor();
        using var scopePantallaA = provider.CreateScope();
        using var scopePantallaB = provider.CreateScope();

        // Act
        var contextoA = scopePantallaA.ServiceProvider.GetRequiredService<RetailDbContext>();
        var contextoB = scopePantallaB.ServiceProvider.GetRequiredService<RetailDbContext>();

        // Assert
        contextoB.Should().NotBeSameAs(contextoA, "dos pantallas no deben compartir el DbContext");
    }

    [Fact]
    public void RegistrarServicios_ServicioDeDialogo_EsUnicoDentroDelScopeDeSuPantalla()
    {
        // Arrange
        using var provider = ConstruirProveedor();
        using var scopePantalla = provider.CreateScope();
        using var scopeOtraPantalla = provider.CreateScope();

        // Act
        var dialogo = scopePantalla.ServiceProvider.GetRequiredService<IProveedorDialogService>();
        var mismoDialogo = scopePantalla.ServiceProvider.GetRequiredService<IProveedorDialogService>();
        var dialogoDeOtraPantalla = scopeOtraPantalla.ServiceProvider.GetRequiredService<IProveedorDialogService>();

        // Assert: el modal usa el scope (y el DbContext) de la pantalla que lo abre
        mismoDialogo.Should().BeSameAs(dialogo);
        dialogoDeOtraPantalla.Should().NotBeSameAs(dialogo);
    }

    [Fact]
    public void RegistrarServicios_ResolverDbContextDesdeLaRaiz_LanzaExcepcion()
    {
        // Arrange
        using var provider = ConstruirProveedor();

        // Act
        var resolverDesdeRaiz = () => provider.GetRequiredService<RetailDbContext>();

        // Assert: la red de seguridad impide volver a compartir un único contexto en toda la app
        resolverDesdeRaiz.Should().Throw<InvalidOperationException>();
    }

    private static ServiceProvider ConstruirProveedor()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = @"Server=(localdb)\mssqllocaldb;Database=RetailDb_DiApp;Trusted_Connection=True;MultipleActiveResultSets=true",
                ["ArcaSettings:UseMockArca"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuracion);
        App.RegistrarServicios(services, configuracion);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }
}

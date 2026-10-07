using System.Windows;
using System.Windows.Controls;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Retail.App.Services;
using Retail.App.UnitTests.Helpers;
using Xunit;

namespace Retail.App.UnitTests.Services;

/// <summary>
/// Verifica que cada pantalla se resuelva en su propio scope de inyección de dependencias (H-19):
/// dos pantallas nunca comparten los servicios Scoped (entre ellos, el DbContext), y al navegar se
/// descarta el scope de la pantalla anterior.
/// </summary>
public class NavigationServiceTests
{
    [Fact]
    public void NavigateTo_DosNavegaciones_ResuelveCadaPantallaEnUnScopeDistinto()
    {
        // Arrange
        VistaDePrueba? primera = null;
        VistaDePrueba? segunda = null;

        // Act
        WpfTestHelper.Run(() =>
        {
            var (navegacion, _) = CrearNavegacion();
            navegacion.NavigateTo<VistaDePrueba>(v => primera = v);
            navegacion.NavigateTo<VistaDePrueba>(v => segunda = v);
        });

        // Assert
        primera.Should().NotBeNull();
        segunda.Should().NotBeNull();
        segunda!.Recurso.Should().NotBeSameAs(primera!.Recurso, "cada pantalla debe tener sus propios servicios Scoped");
    }

    [Fact]
    public void NavigateTo_AlNavegarAOtraPantalla_DescartaElScopeDeLaAnterior()
    {
        // Arrange
        VistaDePrueba? primera = null;
        VistaDePrueba? segunda = null;

        // Act
        WpfTestHelper.Run(() =>
        {
            var (navegacion, _) = CrearNavegacion();
            navegacion.NavigateTo<VistaDePrueba>(v => primera = v);
            navegacion.NavigateTo<VistaDePrueba>(v => segunda = v);
        });

        // Assert
        primera!.Recurso.Descartado.Should().BeTrue("al salir de la pantalla se liberan sus servicios Scoped");
        segunda!.Recurso.Descartado.Should().BeFalse("la pantalla visible conserva su scope");
    }

    [Fact]
    public void NavigateTo_SiLaConfiguracionDeLaVistaFalla_DescartaElScopeNuevoYConservaLaPantallaActual()
    {
        // Arrange
        VistaDePrueba? actual = null;
        VistaDePrueba? fallida = null;
        Exception? error = null;

        // Act
        WpfTestHelper.Run(() =>
        {
            var (navegacion, _) = CrearNavegacion();
            navegacion.NavigateTo<VistaDePrueba>(v => actual = v);
            try
            {
                navegacion.NavigateTo<VistaDePrueba>(v =>
                {
                    fallida = v;
                    throw new InvalidOperationException("Configuración inválida");
                });
            }
            catch (InvalidOperationException ex)
            {
                error = ex;
            }
        });

        // Assert
        error.Should().NotBeNull();
        fallida!.Recurso.Descartado.Should().BeTrue("el scope de una navegación fallida no debe quedar abierto");
        actual!.Recurso.Descartado.Should().BeFalse("la pantalla actual sigue en uso");
    }

    private static (NavigationService Navegacion, Frame Frame) CrearNavegacion()
    {
        var services = new ServiceCollection();
        services.AddScoped<RecursoDePrueba>();
        services.AddTransient<VistaDePrueba>();
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        var frame = new Frame();
        var navegacion = new NavigationService(provider.GetRequiredService<IServiceScopeFactory>());
        navegacion.Initialize(frame);
        return (navegacion, frame);
    }

    /// <summary>Servicio Scoped de prueba que registra si su scope se descartó (como el DbContext).</summary>
    public sealed class RecursoDePrueba : IDisposable
    {
        public bool Descartado { get; private set; }

        public void Dispose()
        {
            Descartado = true;
        }
    }

    public sealed class VistaDePrueba : FrameworkElement
    {
        public VistaDePrueba(RecursoDePrueba recurso)
        {
            Recurso = recurso;
        }

        public RecursoDePrueba Recurso { get; }
    }
}

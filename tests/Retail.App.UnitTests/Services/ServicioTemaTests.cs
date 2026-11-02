using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using FluentAssertions;
using NSubstitute;
using Retail.App.Services;
using Retail.App.UnitTests.Helpers;
using Xunit;

namespace Retail.App.UnitTests.Services;

/// <summary>
/// La Application de WPF se comparte en toda la suite: cada prueba restaura el tema claro al terminar.
/// </summary>
public class ServicioTemaTests
{
    private const string RutaEstilos = "pack://application:,,,/Retail.App;component/Styles/";

    private static ServicioTema CrearServicio(IPreferenciasUsuario? preferencias = null)
    {
        return new ServicioTema(preferencias ?? Substitute.For<IPreferenciasUsuario>());
    }

    private static Color ColorDePaleta(string archivo, string clave)
    {
        var paleta = new ResourceDictionary { Source = new Uri(RutaEstilos + archivo, UriKind.Absolute) };
        return ((SolidColorBrush)paleta[clave]).Color;
    }

    private static Color ColorVigente(string clave)
    {
        return ((SolidColorBrush)System.Windows.Application.Current.FindResource(clave)).Color;
    }

    [Fact]
    public void Aplicar_Oscuro_CambiaElPincelDeTextoALaPaletaOscura()
    {
        WpfTestHelper.Run(() =>
        {
            var servicio = CrearServicio();
            try
            {
                // Act
                servicio.Aplicar(TemaVisual.Oscuro);

                // Assert
                ColorVigente("TextPrimaryBrush").Should().Be(ColorDePaleta("Colors.Dark.xaml", "TextPrimaryBrush"));
            }
            finally
            {
                servicio.Aplicar(TemaVisual.Claro);
            }
        });
    }

    [Fact]
    public void Aplicar_Oscuro_RepintaConsumidoresConDynamicResource()
    {
        WpfTestHelper.Run(() =>
        {
            System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var servicio = CrearServicio();
            Window? ventana = null;
            try
            {
                // Arrange: un Border que toma el pincel con DynamicResource, como toda la interfaz del sistema. Hay que
                // alojarlo en un elemento con recursos: sin árbol, un DynamicResource no se resuelve.
                var borde = (Border)XamlReader.Parse(
                    "<Border xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
                    "Background=\"{DynamicResource SurfaceCardBrush}\" />");
                ventana = new Window { Content = borde, Left = -10000, Top = -10000, ShowActivated = false };
                ventana.Show();

                // Act
                servicio.Aplicar(TemaVisual.Oscuro);

                // Assert
                ((SolidColorBrush)borde.Background).Color.Should().Be(ColorDePaleta("Colors.Dark.xaml", "SurfaceCardBrush"));
            }
            finally
            {
                servicio.Aplicar(TemaVisual.Claro);
                ventana?.Close();
            }
        });
    }

    [Fact]
    public void Aplicar_Oscuro_RepintaElIconoBoxIconsConForegroundDinamico()
    {
        WpfTestHelper.Run(() =>
        {
            System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var servicio = CrearServicio();
            Window? ventana = null;
            try
            {
                // Arrange: el ícono de búsqueda de los campos de texto, que antes tomaba el pincel con StaticResource
                var icono = new Retail.App.Views.Components.IconoBoxIcons
                {
                    Kind = MahApps.Metro.IconPacks.PackIconBoxIconsKind.RegularSearch,
                    Width = 16,
                    Height = 16
                };
                icono.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "TextMutedBrush");
                ventana = new Window { Content = icono, Left = -10000, Top = -10000, ShowActivated = false };
                ventana.Show();

                // Act
                servicio.Aplicar(TemaVisual.Oscuro);

                // Assert
                ((SolidColorBrush)icono.Foreground).Color.Should().Be(ColorDePaleta("Colors.Dark.xaml", "TextMutedBrush"));
            }
            finally
            {
                servicio.Aplicar(TemaVisual.Claro);
                ventana?.Close();
            }
        });
    }

    [Fact]
    public void Aplicar_OscuroYLuegoClaro_RestauraLaPaletaClara()
    {
        WpfTestHelper.Run(() =>
        {
            var servicio = CrearServicio();
            try
            {
                // Act
                servicio.Aplicar(TemaVisual.Oscuro);
                servicio.Aplicar(TemaVisual.Claro);

                // Assert
                ColorVigente("TextPrimaryBrush").Should().Be(ColorDePaleta("Colors.xaml", "TextPrimaryBrush"));
                ColorVigente("SurfaceCardBrush").Should().Be(ColorDePaleta("Colors.xaml", "SurfaceCardBrush"));
            }
            finally
            {
                servicio.Aplicar(TemaVisual.Claro);
            }
        });
    }

    [Fact]
    public void Aplicar_MismoTemaDosVeces_EsIdempotente()
    {
        WpfTestHelper.Run(() =>
        {
            var servicio = CrearServicio();
            try
            {
                // Act
                servicio.Aplicar(TemaVisual.Oscuro);
                servicio.Aplicar(TemaVisual.Oscuro);

                // Assert
                ColorVigente("PrimaryBrush").Should().Be(ColorDePaleta("Colors.Dark.xaml", "PrimaryBrush"));
            }
            finally
            {
                servicio.Aplicar(TemaVisual.Claro);
            }
        });
    }

    [Fact]
    public void AplicarGuardado_PreferenciaOscura_AplicaElTemaOscuro()
    {
        WpfTestHelper.Run(() =>
        {
            var preferencias = Substitute.For<IPreferenciasUsuario>();
            preferencias.Tema = TemaVisual.Oscuro;
            var servicio = CrearServicio(preferencias);
            try
            {
                // Act
                servicio.AplicarGuardado();

                // Assert
                preferencias.Received(1).Cargar();
                ColorVigente("TextPrimaryBrush").Should().Be(ColorDePaleta("Colors.Dark.xaml", "TextPrimaryBrush"));
            }
            finally
            {
                servicio.Aplicar(TemaVisual.Claro);
            }
        });
    }
}

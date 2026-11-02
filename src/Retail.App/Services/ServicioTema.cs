using System.Collections;
using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Retail.App.Services;

/// <summary>
/// Cambia en caliente el tema claro/oscuro: intercambia los valores de la paleta de Retail, el tema de WPF-UI y el
/// acento de marca.
/// </summary>
/// <remarks>
/// Estrategia de la paleta: se copian los colores de la paleta destino sobre los pinceles de los recursos. Los pinceles
/// de un diccionario cargado desde BAML llegan congelados (<see cref="Freezable.IsFrozen"/>, comprobado con una prueba):
/// no admiten cambios, así que se reemplaza la entrada del diccionario por un pincel nuevo. Eso repinta a quienes toman
/// el color con <c>{DynamicResource}</c>, que es la regla del sistema de diseño para todo color; un
/// <c>{StaticResource}</c> de color conservaría el pincel viejo y no cambiaría de tema (<c>audit-xaml.ps1</c> lo
/// rechaza, y los íconos de WPF-UI que lo necesitaban usan <c>IconoBoxIcons</c>).
/// </remarks>
public sealed class ServicioTema : IServicioTema
{
    private const string RutaEstilos = "pack://application:,,,/Retail.App;component/Styles/";

    private static readonly Color s_acentoClaro = Color.FromRgb(0x9D, 0x0F, 0x33);
    private static readonly Color s_acentoOscuro = Color.FromRgb(0xD6, 0x3A, 0x62);

    private readonly IPreferenciasUsuario _preferencias;

    public ServicioTema(IPreferenciasUsuario preferencias)
    {
        _preferencias = preferencias ?? throw new ArgumentNullException(nameof(preferencias));
    }

    public void AplicarGuardado()
    {
        _preferencias.Cargar();
        Aplicar(_preferencias.Tema);
    }

    public void Aplicar(TemaVisual tema)
    {
        var app = System.Windows.Application.Current;
        if (app is null)
        {
            return;
        }

        var esOscuro = tema == TemaVisual.Oscuro;
        var temaWpfUi = esOscuro ? ApplicationTheme.Dark : ApplicationTheme.Light;
        var archivoPaleta = esOscuro ? "Colors.Dark.xaml" : "Colors.xaml";

        var paleta = new ResourceDictionary { Source = new Uri(RutaEstilos + archivoPaleta, UriKind.Absolute) };
        CopiarPaleta(paleta, BuscarPaletaActiva(app.Resources));

        ApplicationThemeManager.Apply(temaWpfUi, WindowBackdropType.None, updateAccent: false);
        ApplicationAccentColorManager.Apply(esOscuro ? s_acentoOscuro : s_acentoClaro, temaWpfUi);
    }

    /// <summary>
    /// Ubica el diccionario de paleta que App.xaml fusionó (Colors.xaml o Colors.Dark.xaml). Se identifica por el
    /// nombre del archivo porque su posición puede variar.
    /// </summary>
    private static ResourceDictionary BuscarPaletaActiva(ResourceDictionary recursos)
    {
        foreach (var fusionado in recursos.MergedDictionaries)
        {
            var origen = fusionado.Source?.OriginalString;
            if (origen is not null
                && (origen.EndsWith("Styles/Colors.xaml", StringComparison.OrdinalIgnoreCase)
                    || origen.EndsWith("Styles/Colors.Dark.xaml", StringComparison.OrdinalIgnoreCase)))
            {
                return fusionado;
            }
        }

        throw new InvalidOperationException("No se encontró el diccionario de paleta (Colors.xaml) entre los recursos de la aplicación.");
    }

    private static void CopiarPaleta(ResourceDictionary origen, ResourceDictionary destino)
    {
        foreach (DictionaryEntry entrada in origen)
        {
            var actual = destino[entrada.Key];

            if (entrada.Value is SolidColorBrush nuevo)
            {
                if (actual is SolidColorBrush existente && !existente.IsFrozen)
                {
                    existente.Color = nuevo.Color;
                    existente.Opacity = nuevo.Opacity;
                }
                else
                {
                    destino[entrada.Key] = new SolidColorBrush(nuevo.Color) { Opacity = nuevo.Opacity };
                }
            }
            else if (!Equals(actual, entrada.Value))
            {
                destino[entrada.Key] = entrada.Value;
            }
        }
    }
}

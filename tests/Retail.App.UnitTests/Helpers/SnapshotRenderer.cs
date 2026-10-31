using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Retail.App.UnitTests.Helpers;

/// <summary>
/// Renderiza vistas WPF a archivos PNG para poder "ver" la interfaz sin iniciar la aplicación (que exige base de datos
/// y login). Las capturas se guardan en <c>artifacts/snapshots</c>, carpeta ignorada por Git.
/// </summary>
public static class SnapshotRenderer
{
    /// <summary>
    /// Resolución de la captura. 144 DPI equivale a una escala de 1,5: el texto sale nítido y legible en la imagen.
    /// </summary>
    private const double Dpi = 144d;

    private const double Escala = Dpi / 96d;

    /// <summary>
    /// Muestra el elemento en una ventana fuera de pantalla y guarda su captura como PNG. Debe invocarse en el hilo
    /// STA del dispatcher de pruebas (dentro de <see cref="WpfTestHelper.Run(Action)"/>).
    /// </summary>
    /// <param name="elemento">Vista o ventana a capturar.</param>
    /// <param name="ancho">Ancho lógico de la ventana en unidades independientes del dispositivo.</param>
    /// <param name="alto">Alto lógico de la ventana.</param>
    /// <param name="nombre">Ruta relativa sin extensión (puede incluir subcarpetas, por ejemplo "Light/ArticulosView").</param>
    /// <param name="condicionListo">
    /// Condición opcional que indica que la carga asíncrona de la vista terminó (por ejemplo, que la grilla ya tiene
    /// filas). Se espera hasta 5 segundos vaciando la cola del dispatcher.
    /// </param>
    /// <returns>Ruta completa del PNG generado.</returns>
    /// <remarks>
    /// Los popups (menús, desplegables) son ventanas propias que se mostrarían en pantalla aunque la vista esté fuera de
    /// ella: por eso las capturas no abren ninguno.
    /// </remarks>
    public static string Guardar(FrameworkElement elemento, double ancho, double alto, string nombre, Func<bool>? condicionListo = null)
    {
        ArgumentNullException.ThrowIfNull(elemento);

        var ventana = PrepararVentana(elemento, ancho, alto);

        try
        {
            ventana.Show();
            VaciarDispatcher(ventana, condicionListo);

            var rutaArchivo = ResolverRutaArchivo(nombre);
            File.WriteAllBytes(rutaArchivo, RenderizarAPng(ventana));
            return rutaArchivo;
        }
        finally
        {
            ventana.Close();
        }
    }

    /// <summary>
    /// Captura una página larga con scroll en varias imágenes: aloja la vista en una ventana de <paramref name="altoParte"/>
    /// de alto, busca su primer <see cref="ScrollViewer"/> y, desde arriba, guarda una captura por cada pantallazo
    /// (<c>nombre_01.png</c>, <c>nombre_02.png</c>…). La última parte queda alineada al final de la página, así que puede
    /// repetir un poco de la anterior. Es más legible que una sola imagen de varios miles de píxeles de alto.
    /// </summary>
    /// <returns>Rutas completas de los PNG generados, en orden de arriba hacia abajo.</returns>
    public static IReadOnlyList<string> GuardarPorPartes(FrameworkElement elemento, double ancho, double altoParte, string nombre)
    {
        ArgumentNullException.ThrowIfNull(elemento);

        var ventana = PrepararVentana(elemento, ancho, altoParte);
        var rutas = new List<string>();

        try
        {
            ventana.Show();
            VaciarDispatcher(ventana, null);

            var scroll = BuscarScrollViewer(ventana)
                ?? throw new InvalidOperationException("La vista no contiene un ScrollViewer para dividir en partes.");

            var partes = Math.Max(1, (int)Math.Ceiling(scroll.ExtentHeight / scroll.ViewportHeight));
            for (var i = 0; i < partes; i++)
            {
                scroll.ScrollToVerticalOffset(i * scroll.ViewportHeight);
                VaciarDispatcher(ventana, null);

                var rutaArchivo = ResolverRutaArchivo($"{nombre}_{i + 1:00}");
                File.WriteAllBytes(rutaArchivo, RenderizarAPng(ventana));
                rutas.Add(rutaArchivo);
            }

            return rutas;
        }
        finally
        {
            ventana.Close();
        }
    }

    private static ScrollViewer? BuscarScrollViewer(DependencyObject raiz)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
        {
            var hijo = VisualTreeHelper.GetChild(raiz, i);
            if (hijo is ScrollViewer scroll)
            {
                return scroll;
            }

            var interno = BuscarScrollViewer(hijo);
            if (interno is not null)
            {
                return interno;
            }
        }

        return null;
    }

    private static Window PrepararVentana(FrameworkElement elemento, double ancho, double alto)
    {
        // Con el modo por defecto, cerrar la última ventana apaga la Application compartida de toda la suite y las
        // pruebas siguientes fallan con "El objeto Application se va a cerrar".
        System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Un UserControl solo dispara Loaded (y por lo tanto la carga de datos) dentro de una ventana real mostrada;
        // por eso se lo aloja en una ventana propia en lugar de medirlo suelto.
        var ventana = elemento as Window ?? CrearVentanaAnfitriona(elemento);
        ventana.Left = -10000;
        ventana.Top = -10000;
        ventana.Width = ancho;

        // Un diálogo con SizeToContent="Height" se captura con el alto que le da su contenido, como en la aplicación:
        // forzarle un alto fijo dejaría una franja vacía (o recortaría el pie) que no existe en la realidad.
        if (ventana.SizeToContent != SizeToContent.Height)
        {
            ventana.SizeToContent = SizeToContent.Manual;
            ventana.Height = alto;
        }
        ventana.ShowActivated = false;
        ventana.ShowInTaskbar = false;
        ventana.WindowStartupLocation = WindowStartupLocation.Manual;
        return ventana;
    }

    private static Window CrearVentanaAnfitriona(FrameworkElement elemento)
    {
        return new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            Background = ObtenerFondo(),
            Content = elemento
        };
    }

    private static Brush ObtenerFondo()
    {
        return System.Windows.Application.Current?.Resources["SurfaceBackgroundBrush"] as Brush ?? Brushes.White;
    }

    /// <summary>
    /// Deja que se ejecuten los manejadores <c>Loaded</c> asíncronos: se vacía el dispatcher hasta el nivel
    /// <see cref="DispatcherPriority.ApplicationIdle"/> dos veces, porque la primera pasada dispara las cargas y la
    /// segunda procesa las continuaciones que se encolaron al terminar (y el re-layout resultante).
    /// </summary>
    private static void VaciarDispatcher(Window ventana, Func<bool>? condicionListo)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var limite = DateTime.UtcNow.AddSeconds(5);

        do
        {
            ventana.UpdateLayout();
            dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            ventana.UpdateLayout();
        }
        while (condicionListo is not null && !condicionListo() && DateTime.UtcNow < limite);
    }

    /// <summary>
    /// Compone la captura sobre un fondo sólido: las ventanas con Mica pueden quedar con alfa en el render y el PNG
    /// saldría transparente (o negro en algunos visores).
    /// </summary>
    private static byte[] RenderizarAPng(Window ventana)
    {
        var anchoReal = ventana.ActualWidth;
        var altoReal = ventana.ActualHeight;
        var visual = new DrawingVisual();
        using (var contexto = visual.RenderOpen())
        {
            var area = new Rect(0, 0, anchoReal, altoReal);
            contexto.DrawRectangle(ObtenerFondo(), null, area);
            contexto.DrawRectangle(new VisualBrush(ventana) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, null, area);
        }

        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(anchoReal * Escala),
            (int)Math.Ceiling(altoReal * Escala),
            Dpi,
            Dpi,
            PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var memoria = new MemoryStream();
        encoder.Save(memoria);
        return memoria.ToArray();
    }

    private static string ResolverRutaArchivo(string nombre)
    {
        var raiz = new DirectoryInfo(AppContext.BaseDirectory);
        while (raiz is not null && !File.Exists(Path.Combine(raiz.FullName, "Retail.sln")))
        {
            raiz = raiz.Parent;
        }

        if (raiz is null)
        {
            throw new DirectoryNotFoundException("No se encontró Retail.sln subiendo desde " + AppContext.BaseDirectory);
        }

        var ruta = Path.Combine(raiz.FullName, "artifacts", "snapshots", nombre.Replace('/', Path.DirectorySeparatorChar) + ".png");
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        return ruta;
    }
}

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Retail.App.ViewModels.Comun;
using Retail.App.Views.Pages;
using Retail.Domain.Enums;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dev;

/// <summary>
/// Fila de muestra de la tabla de la galería de estilos.
/// </summary>
public record ArticuloMuestra(
    string Descripcion,
    string Categoria,
    string Marca,
    string? Distribuidor,
    decimal PrecioVenta,
    int Stock,
    bool StockBajo,
    bool EsServicio);

/// <summary>
/// Galería de estilos: referencia viva del sistema de diseño (fundamentos, componentes y patrones de pantalla) armada con
/// los estilos y componentes reales. Los componentes con estado (filtros de columna, paginación) reciben datos de
/// muestra de esta clase, así funcionan igual que en una pantalla. Al final quedan las herramientas de desarrollo.
/// </summary>
public partial class StyleGalleryView : UserControl
{
    public ObservableCollection<ArticuloMuestra> ArticulosEjemplo { get; } =
    [
        new("Cuaderno Rivadavia ABC 50 hojas rayado", "Cuadernos", "Rivadavia", "Distribuidora Sur", 2960m, 42, false, false),
        new("Lápiz negro Faber-Castell HB", "Escritura", "Faber-Castell", "Distribuidora Sur", 576m, 3, true, false),
        new("Resma A4 Autor 75 g x 500 hojas", "Papel para impresión", "Autor", "Papelera del Plata", 9315m, 0, true, false),
        new("Fotocopia simple faz", "Servicios", string.Empty, null, 100m, 0, false, true)
    ];

    /// <summary>Filtro de la columna Categoría, con las categorías agrupadas por rubro.</summary>
    public FiltroColumnaViewModel FiltroCategoria { get; } = new("Categoría");

    /// <summary>Filtro de la columna Stock, con un estado aplicado para mostrar el embudo resaltado.</summary>
    public FiltroColumnaViewModel FiltroStock { get; } = new("Stock");

    public PaginacionMuestra Paginacion { get; } = new();

    public StyleGalleryView()
    {
        InitializeComponent();
        CargarFiltrosDeMuestra();
        DataContext = this;
    }

    private void CargarFiltrosDeMuestra()
    {
        FiltroCategoria.CargarOpciones(
        [
            new OpcionFiltroColumna(1, "Carpetas", "Oficina"),
            new OpcionFiltroColumna(2, "Papel para impresión", "Oficina"),
            new OpcionFiltroColumna(3, "Cuadernos", "Papelería"),
            new OpcionFiltroColumna(4, "Escritura", "Papelería"),
            new OpcionFiltroColumna(5, "Servicios", "Servicios"),
            new OpcionFiltroColumna(6, "Mochilas", "Escolar"),
            new OpcionFiltroColumna(7, "Marcadores", "Papelería"),
            new OpcionFiltroColumna(8, "Calculadoras", "Oficina")
        ]);

        FiltroStock.CargarOpciones(
        [
            new OpcionFiltroColumna((int)FiltroStockArticuloEnum.SinStock, "Sin stock"),
            new OpcionFiltroColumna((int)FiltroStockArticuloEnum.StockBajo, "Stock bajo"),
            new OpcionFiltroColumna((int)FiltroStockArticuloEnum.ConStock, "Con stock")
        ]);
        FiltroStock.EstaAbierto = true;
        FiltroStock.Grupos[0].Opciones[1].Seleccionada = true;
        FiltroStock.AplicarCommand.Execute(null);
    }

    private void BtnAbrirModalMuestra_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new DialogoMuestraDialog { Owner = Window.GetWindow(this) };
        dialogo.ShowDialog();
    }

    private void BtnTestDispatcherException_Click(object sender, RoutedEventArgs e)
    {
        throw new InvalidOperationException("Excepción deliberada en el Dispatcher de UI (Prueba de Resiliencia - Etapa 0.8).");
    }

    private void BtnTestTaskException_Click(object sender, RoutedEventArgs e)
    {
        // Provoca una tarea que falla sin ser esperada (Unobserved Task Exception)
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            throw new InvalidOperationException("Excepción asíncrona no observada (Prueba de Resiliencia - Etapa 0.8).");
        });

        // Forzar recolección de basura para que el finalizador detecte la tarea no observada
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    private void BtnTestAppDomainException_Click(object sender, RoutedEventArgs e)
    {
        // Provoca una excepción en un hilo de fondo del AppDomain
        var thread = new System.Threading.Thread(() =>
        {
            throw new InvalidOperationException("Excepción deliberada en Hilo de Fondo / AppDomain (Prueba de Resiliencia - Etapa 0.8).");
        })
        {
            IsBackground = true
        };
        thread.Start();
    }

    private void BtnAbrirUsuariosDev_Click(object sender, RoutedEventArgs e)
    {
        AbrirEnVentana(App.Services.GetRequiredService<UsuariosView>(), "Retail POS - Sandbox: Administración de Usuarios");
    }

    private void BtnAbrirArticulosDev_Click(object sender, RoutedEventArgs e)
    {
        AbrirEnVentana(App.Services.GetRequiredService<ArticulosView>(), "Retail POS - Sandbox: Catálogo de Artículos");
    }

    private void BtnAbrirClientesDev_Click(object sender, RoutedEventArgs e)
    {
        AbrirEnVentana(App.Services.GetRequiredService<ClientesView>(), "Retail POS - Sandbox: Padrón de Clientes");
    }

    // Una vista de módulo en su propia ventana, para probarla sin pasar por la navegación del shell
    private static void AbrirEnVentana(UserControl vista, string titulo)
    {
        var ventana = new FluentWindow
        {
            Title = titulo,
            Width = 1180,
            Height = 720,
            MinWidth = 950,
            MinHeight = 580,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = vista
        };
        ventana.Show();
    }
}

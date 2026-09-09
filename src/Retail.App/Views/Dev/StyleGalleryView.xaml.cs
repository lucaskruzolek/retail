using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace Retail.App.Views.Dev;

/// <summary>
/// Modelo de datos sintético para la demostración de la grilla de la Galería de Controles.
/// </summary>
public record ArticuloMuestra(
    string CodigoBarras,
    string Descripcion,
    string Categoria,
    int Stock,
    bool StockBajo,
    string PrecioVentaFormateado
);

/// <summary>
/// Lógica de interacción para StyleGalleryView.xaml (Galería de Controles interactiva de la Etapa 0.6).
/// </summary>
public partial class StyleGalleryView : UserControl
{
    public ObservableCollection<ArticuloMuestra> ArticulosEjemplo { get; } =
    [
        new("9789500762885", "El Principito - Antoine de Saint-Exupéry", "Literatura", 3, true, "$ 12.500,00"),
        new("9789878000107", "Rayuela - Julio Cortázar", "Ficción", 18, false, "$ 24.800,00"),
        new("9789501298451", "Cuaderno Universitario A4 80 Hojas Rayado", "Escolar", 45, false, "$ 4.250,50"),
        new("(Artesanal)", "Señalador de Madera Grabado a Láser", "Regalería", 12, false, "$ 1.800,00")
    ];

    public StyleGalleryView()
    {
        InitializeComponent();
        DataContext = this;
    }
}

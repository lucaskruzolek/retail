using System.Windows.Controls;

namespace Retail.App.Views.Components;

/// <summary>
/// Tabla de valores del proveedor sin equivalencia: cada fila se asocia a una categoría o marca de la tienda, o se
/// ignora (Track 6.6, M-13, M-32). Su DataContext es un <c>TraducirValoresPasoViewModel</c>. La alojan el paso 1 del modal
/// de incorporación y el diálogo "Asociar categorías y marcas del catálogo" (M-80).
/// </summary>
public partial class AsociarValoresPanel : UserControl
{
    public AsociarValoresPanel()
    {
        InitializeComponent();
    }
}

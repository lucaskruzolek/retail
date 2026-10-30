using System.Windows;
using System.Windows.Controls;

namespace Retail.App.Views.Components;

public partial class PaginationBar : UserControl
{
    public static readonly DependencyProperty MostrarResumenProperty =
        DependencyProperty.Register(nameof(MostrarResumen), typeof(bool), typeof(PaginationBar), new PropertyMetadata(true));

    public PaginationBar()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Muestra el "Mostrando X a Y de Z" a la izquierda. Una página que ya informa la cantidad de resultados en otro
    /// lugar lo oculta y aprovecha ese espacio (Artículos, para sus acciones masivas).
    /// </summary>
    public bool MostrarResumen
    {
        get => (bool)GetValue(MostrarResumenProperty);
        set => SetValue(MostrarResumenProperty, value);
    }
}

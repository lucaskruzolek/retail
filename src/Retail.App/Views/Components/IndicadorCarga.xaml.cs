using System.Windows;
using System.Windows.Controls;

namespace Retail.App.Views.Components;

/// <summary>
/// Círculo de carga sin leyenda (regla R12 del sistema de diseño): mientras <see cref="EstaCargando"/> es verdadero gira
/// dentro de un cuadrado de 20 × 20 que está <b>siempre reservado</b>. Como se oculta sin colapsarse, ni su aparición ni su
/// desaparición corren los demás controles de la línea (un "Cargando…" dentro de una barra de filtros mandaba lo que seguía
/// a una línea nueva). Vive en el encabezado de la página (<see cref="PageHeader"/>), junto al ícono de información.
/// </summary>
public partial class IndicadorCarga : UserControl
{
    public static readonly DependencyProperty EstaCargandoProperty =
        DependencyProperty.Register(nameof(EstaCargando), typeof(bool), typeof(IndicadorCarga), new PropertyMetadata(false));

    public IndicadorCarga()
    {
        InitializeComponent();
    }

    public bool EstaCargando
    {
        get => (bool)GetValue(EstaCargandoProperty);
        set => SetValue(EstaCargandoProperty, value);
    }
}

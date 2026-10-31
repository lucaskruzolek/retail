using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Retail.App.Views.Components;

/// <summary>
/// Pie de una tabla paginada: a la izquierda "Página [n] de N" (editable: Enter va a esa página) y las filas por página;
/// a la derecha primera / anterior, los números de página con "…" y siguiente / última.
/// <para>
/// Se enlaza al ViewModel de la página por su DataContext (<c>PaginaActual</c>, <c>TotalPaginas</c>, <c>TamanoPagina</c>
/// y los comandos de navegación, incluido <c>IrAPaginaCommand</c>). Los números visibles se calculan acá con
/// <see cref="CalcularElementos"/>: siempre siete lugares, para que la barra no cambie de ancho al navegar.
/// </para>
/// </summary>
public partial class PaginationBar : UserControl
{
    // Siempre siete lugares: con más páginas, los tres primeros y los tres últimos o la actual con sus vecinas
    private const int LugaresVisibles = 7;

    public static readonly DependencyProperty MostrarResumenProperty =
        DependencyProperty.Register(nameof(MostrarResumen), typeof(bool), typeof(PaginationBar), new PropertyMetadata(true));

    public static readonly DependencyProperty PaginaActualProperty =
        DependencyProperty.Register(nameof(PaginaActual), typeof(int), typeof(PaginationBar), new PropertyMetadata(1, AlCambiarPaginas));

    public static readonly DependencyProperty TotalPaginasProperty =
        DependencyProperty.Register(nameof(TotalPaginas), typeof(int), typeof(PaginationBar), new PropertyMetadata(1, AlCambiarPaginas));

    public static readonly DependencyProperty ComandoIrAPaginaProperty =
        DependencyProperty.Register(nameof(ComandoIrAPagina), typeof(ICommand), typeof(PaginationBar), new PropertyMetadata(null));

    public PaginationBar()
    {
        InitializeComponent();

        // Se enlazan acá y no en la raíz del XAML: ahí solo se pueden fijar propiedades de UserControl, no las propias
        SetBinding(PaginaActualProperty, new Binding("PaginaActual"));
        SetBinding(TotalPaginasProperty, new Binding("TotalPaginas"));
        SetBinding(ComandoIrAPaginaProperty, new Binding("IrAPaginaCommand"));
        ActualizarElementos();

        CuadroPagina.KeyDown += AlPresionarTeclaEnCuadro;
        CuadroPagina.LostKeyboardFocus += (_, _) => IrALaPaginaEscrita();
    }

    /// <summary>
    /// Muestra el "Mostrando X a Y de Z" a la izquierda. Una página que ya informa la cantidad de resultados en otro
    /// lugar lo oculta.
    /// </summary>
    public bool MostrarResumen
    {
        get => (bool)GetValue(MostrarResumenProperty);
        set => SetValue(MostrarResumenProperty, value);
    }

    public int PaginaActual
    {
        get => (int)GetValue(PaginaActualProperty);
        set => SetValue(PaginaActualProperty, value);
    }

    public int TotalPaginas
    {
        get => (int)GetValue(TotalPaginasProperty);
        set => SetValue(TotalPaginasProperty, value);
    }

    public ICommand? ComandoIrAPagina
    {
        get => (ICommand?)GetValue(ComandoIrAPaginaProperty);
        set => SetValue(ComandoIrAPaginaProperty, value);
    }

    public ObservableCollection<ElementoPaginacion> Elementos { get; } = new();

    /// <summary>
    /// Los lugares de la lista de páginas. Hasta siete páginas se muestran todas; con más, siete lugares: cerca de un
    /// extremo, "1 2 3 … N-2 N-1 N"; en el medio, "1 … a-1 a a+1 … N".
    /// </summary>
    public static IReadOnlyList<ElementoPaginacion> CalcularElementos(int actual, int total)
    {
        total = Math.Max(1, total);
        actual = Math.Clamp(actual, 1, total);

        IEnumerable<int?> numeros;
        if (total <= LugaresVisibles)
        {
            numeros = Enumerable.Range(1, total).Select(n => (int?)n);
        }
        else if (actual <= 3 || actual >= total - 2)
        {
            numeros = [1, 2, 3, null, total - 2, total - 1, total];
        }
        else
        {
            numeros = [1, null, actual - 1, actual, actual + 1, null, total];
        }

        return numeros.Select(n => new ElementoPaginacion(n, n == actual)).ToList();
    }

    private static void AlCambiarPaginas(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((PaginationBar)d).ActualizarElementos();
    }

    private void ActualizarElementos()
    {
        Elementos.Clear();
        foreach (var elemento in CalcularElementos(PaginaActual, TotalPaginas))
        {
            Elementos.Add(elemento);
        }

        // Los bindings de la raíz pueden avisar antes de que InitializeComponent cree el cuadro
        if (CuadroPagina is not null)
        {
            CuadroPagina.Text = PaginaActual.ToString(CultureInfo.InvariantCulture);
        }
    }

    private void AlPresionarTeclaEnCuadro(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            IrALaPaginaEscrita();
            e.Handled = true;
        }
    }

    // Un número válido va a esa página; cualquier otra cosa vuelve a mostrar la actual
    private void IrALaPaginaEscrita()
    {
        if (int.TryParse(CuadroPagina.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int pagina) &&
            pagina >= 1 && pagina <= TotalPaginas && pagina != PaginaActual &&
            ComandoIrAPagina?.CanExecute(pagina) == true)
        {
            ComandoIrAPagina.Execute(pagina);
            return;
        }

        CuadroPagina.Text = PaginaActual.ToString(CultureInfo.InvariantCulture);
    }
}

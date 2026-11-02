using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Retail.App.ViewModels.Comun;

/// <summary>
/// Opciones del filtro que comparten encabezado (las categorías de un rubro). Su casilla es de tres estados: marcada si
/// todas las opciones visibles lo están, vacía si ninguna, e indeterminada (<c>null</c>) si hay de las dos; marcarla
/// marca o desmarca todas las visibles a la vez. Un grupo sin <see cref="Nombre"/> se muestra sin encabezado.
/// </summary>
public sealed class GrupoFiltroColumna : ObservableObject
{
    private bool _propagando;

    public GrupoFiltroColumna(string? nombre, IReadOnlyList<OpcionFiltroColumna> opciones)
    {
        Nombre = nombre;
        Opciones = opciones ?? throw new ArgumentNullException(nameof(opciones));

        foreach (var opcion in Opciones)
        {
            opcion.PropertyChanged += AlCambiarOpcion;
        }
    }

    public string? Nombre { get; }

    public bool TieneEncabezado => !string.IsNullOrEmpty(Nombre);

    public IReadOnlyList<OpcionFiltroColumna> Opciones { get; }

    /// <summary>Hay al menos una opción que coincide con la búsqueda.</summary>
    public bool Visible => Opciones.Any(o => o.Visible);

    public bool? TodasSeleccionadas
    {
        get
        {
            var visibles = Opciones.Where(o => o.Visible).ToList();
            if (visibles.Count > 0 && visibles.All(o => o.Seleccionada))
            {
                return true;
            }

            return visibles.Any(o => o.Seleccionada) ? null : false;
        }
        set
        {
            // La casilla de WPF pasa de indeterminada a desmarcada: un clic siempre deja un estado definido.
            bool marcar = value == true;
            _propagando = true;
            try
            {
                foreach (var opcion in Opciones.Where(o => o.Visible))
                {
                    opcion.Seleccionada = marcar;
                }
            }
            finally
            {
                _propagando = false;
            }

            OnPropertyChanged();
        }
    }

    private void AlCambiarOpcion(object? sender, PropertyChangedEventArgs e)
    {
        if (_propagando)
        {
            return;
        }

        if (e.PropertyName is nameof(OpcionFiltroColumna.Seleccionada) or nameof(OpcionFiltroColumna.Visible))
        {
            OnPropertyChanged(nameof(TodasSeleccionadas));
        }

        if (e.PropertyName == nameof(OpcionFiltroColumna.Visible))
        {
            OnPropertyChanged(nameof(Visible));
        }
    }
}

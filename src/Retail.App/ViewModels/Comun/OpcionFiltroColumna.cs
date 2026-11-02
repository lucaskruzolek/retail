using CommunityToolkit.Mvvm.ComponentModel;

namespace Retail.App.ViewModels.Comun;

/// <summary>
/// Una casilla del filtro de una columna: el valor que representa (<see cref="Id"/>), su texto y el grupo bajo el que se
/// muestra (por ejemplo, el rubro de una categoría). <see cref="Seleccionada"/> es el borrador mientras el filtro está
/// abierto; lo aplicado vive en <see cref="FiltroColumnaViewModel.IdsAplicados"/>.
/// </summary>
public partial class OpcionFiltroColumna : ObservableObject
{
    [ObservableProperty]
    private bool _seleccionada;

    /// <summary>Falso cuando el texto buscado dentro del filtro no aparece en el nombre.</summary>
    [ObservableProperty]
    private bool _visible = true;

    public OpcionFiltroColumna(int id, string nombre, string? grupo = null)
    {
        Id = id;
        Nombre = nombre ?? throw new ArgumentNullException(nameof(nombre));
        Grupo = grupo;
    }

    public int Id { get; }

    public string Nombre { get; }

    public string? Grupo { get; }
}

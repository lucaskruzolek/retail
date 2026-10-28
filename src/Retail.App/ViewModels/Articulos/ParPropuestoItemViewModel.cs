using CommunityToolkit.Mvvm.ComponentModel;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;

namespace Retail.App.ViewModels.Articulos;

/// <summary>
/// Fila de la revisión de reglas propuestas (Track 6.6, paso 6.8): un par que los artículos confirmados respaldan,
/// con la categoría propuesta editable antes de aceptarlo.
/// </summary>
public partial class ParPropuestoItemViewModel : ObservableObject
{
    public ParPropuestoItemViewModel(ParPropuestoDto propuesto, CategoriaDto categoriaPropuesta)
    {
        Propuesto = propuesto ?? throw new ArgumentNullException(nameof(propuesto));
        _categoriaElegida = categoriaPropuesta ?? throw new ArgumentNullException(nameof(categoriaPropuesta));
    }

    public ParPropuestoDto Propuesto { get; }

    public string Termino => Propuesto.Termino;

    public string SegundaPalabra => Propuesto.SegundaPalabra;

    /// <summary>La evidencia, por ejemplo "8 de 8": artículos con esa categoría sobre el total con el par.</summary>
    public string Evidencia => $"{Propuesto.Coincidencias} de {Propuesto.Total}";

    public string Ejemplos => string.Join(" | ", Propuesto.Ejemplos);

    /// <summary>Categoría con la que se acepta el par; arranca en la propuesta y el usuario puede cambiarla.</summary>
    [ObservableProperty]
    private CategoriaDto _categoriaElegida;

    /// <summary>Marca de selección para aceptar o descartar varios pares a la vez.</summary>
    [ObservableProperty]
    private bool _seleccionado;
}

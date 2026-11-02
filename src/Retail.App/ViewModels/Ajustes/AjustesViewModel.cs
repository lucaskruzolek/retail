using CommunityToolkit.Mvvm.ComponentModel;
using Retail.App.Services;

namespace Retail.App.ViewModels.Ajustes;

/// <summary>
/// Pantalla de Ajustes. Hoy solo permite elegir el tema claro/oscuro: al cambiarlo se aplica en caliente y se guarda
/// en las preferencias del usuario, de modo que la próxima vez la aplicación arranca con ese tema.
/// </summary>
public partial class AjustesViewModel : ObservableObject
{
    private readonly IServicioTema _servicioTema;
    private readonly IPreferenciasUsuario _preferencias;

    public AjustesViewModel(IServicioTema servicioTema, IPreferenciasUsuario preferencias)
    {
        _servicioTema = servicioTema ?? throw new ArgumentNullException(nameof(servicioTema));
        _preferencias = preferencias ?? throw new ArgumentNullException(nameof(preferencias));

        // Se asigna el campo (no la propiedad) para no volver a aplicar ni guardar el tema que ya está vigente.
        _temaOscuro = _preferencias.Tema == TemaVisual.Oscuro;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemaClaro))]
    private bool _temaOscuro;

    /// <summary>
    /// Espejo de <see cref="TemaOscuro"/> para la opción "Claro" del selector: las dos opciones son un par excluyente,
    /// cada una enlazada a su propia propiedad.
    /// </summary>
    public bool TemaClaro
    {
        get => !TemaOscuro;
        set => TemaOscuro = !value;
    }

    partial void OnTemaOscuroChanged(bool value)
    {
        var tema = value ? TemaVisual.Oscuro : TemaVisual.Claro;

        _preferencias.Tema = tema;
        _servicioTema.Aplicar(tema);
        _preferencias.Guardar();
    }
}

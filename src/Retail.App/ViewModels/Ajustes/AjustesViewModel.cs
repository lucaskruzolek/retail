using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Retail.App.Services;

namespace Retail.App.ViewModels.Ajustes;

/// <summary>
/// Pantalla de Ajustes. Permite elegir el tema claro/oscuro y consultar la documentación y manual de usuario.
/// </summary>
public partial class AjustesViewModel : ObservableObject
{
    private readonly IServicioTema _servicioTema;
    private readonly IPreferenciasUsuario _preferencias;
    private readonly IAyudaDialogService? _ayudaDialogService;

    public AjustesViewModel(
        IServicioTema servicioTema,
        IPreferenciasUsuario preferencias,
        IAyudaDialogService? ayudaDialogService = null)
    {
        _servicioTema = servicioTema ?? throw new ArgumentNullException(nameof(servicioTema));
        _preferencias = preferencias ?? throw new ArgumentNullException(nameof(preferencias));
        _ayudaDialogService = ayudaDialogService;

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

    [RelayCommand]
    private void AbrirManualPdf()
    {
        _ayudaDialogService?.AbrirManualPdf();
    }

    [RelayCommand]
    private void AbrirCentroAyuda()
    {
        _ayudaDialogService?.MostrarCentroAyuda();
    }
}

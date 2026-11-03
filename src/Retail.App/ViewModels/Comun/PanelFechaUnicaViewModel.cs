using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Retail.App.ViewModels.Comun;

/// <summary>
/// Estado mínimo del panel de <c>SelectorFechaUnica</c>: solo si está abierto. Existe para reutilizar
/// <c>PanelFiltroEmergente</c> (clic afuera, animación, Esc y Enter), que gobierna cualquier <see cref="IFiltroEmergente"/>.
/// Elegir un día en el calendario cierra el panel; no hay borrador que aplicar ni descartar, por eso "Aplicar" y "Cancelar"
/// solo lo cierran.
/// </summary>
public partial class PanelFechaUnicaViewModel : ObservableObject, IFiltroEmergente
{
    [ObservableProperty]
    private bool _estaAbierto;

    [RelayCommand]
    private void Aplicar()
    {
        EstaAbierto = false;
    }

    [RelayCommand]
    private void Cancelar()
    {
        EstaAbierto = false;
    }
}

using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Retail.App.ViewModels.Comun;

/// <summary>
/// Lo que necesita un panel emergente de filtro de columna (selección múltiple, fechas) para abrirse y cerrarse: si está
/// abierto y qué hacer al aplicar o cancelar. Lo implementan <see cref="FiltroColumnaViewModel"/> y
/// <see cref="FiltroFechaColumnaViewModel"/>; la mecánica del popup (clic afuera, animación, teclado) es una sola.
/// </summary>
public interface IFiltroEmergente : INotifyPropertyChanged
{
    /// <summary>El panel está abierto (enlazado en ambos sentidos con el botón de la cabecera).</summary>
    bool EstaAbierto { get; set; }

    IRelayCommand AplicarCommand { get; }

    IRelayCommand CancelarCommand { get; }
}

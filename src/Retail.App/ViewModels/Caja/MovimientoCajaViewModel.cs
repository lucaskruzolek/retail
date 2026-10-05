using CommunityToolkit.Mvvm.ComponentModel;
using Retail.Domain.Enums;

namespace Retail.App.ViewModels.Caja;

/// <summary>
/// ViewModel desacoplado para el diálogo de registro de movimiento extraordinario de caja (RF-14).
/// </summary>
public partial class MovimientoCajaViewModel : ObservableObject
{
    [ObservableProperty]
    private int _idTurno;

    [ObservableProperty]
    private TipoMovimientoCajaEnum _tipoMovimiento = TipoMovimientoCajaEnum.IngresoExtraordinario;

    [ObservableProperty]
    private decimal _monto;

    [ObservableProperty]
    private string _concepto = string.Empty;

    public MovimientoCajaViewModel(int idTurno)
    {
        IdTurno = idTurno;
    }
}

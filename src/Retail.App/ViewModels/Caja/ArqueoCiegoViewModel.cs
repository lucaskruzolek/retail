using CommunityToolkit.Mvvm.ComponentModel;

namespace Retail.App.ViewModels.Caja;

public partial class ArqueoCiegoViewModel : ObservableObject
{
    [ObservableProperty]
    private int _idTurno;

    [ObservableProperty]
    private decimal _saldoDeclaradoEfectivo;

    [ObservableProperty]
    private decimal _montoRetenidoEnCaja;

    public ArqueoCiegoViewModel(int idTurno)
    {
        IdTurno = idTurno;
    }
}

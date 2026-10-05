using CommunityToolkit.Mvvm.ComponentModel;

namespace Retail.App.ViewModels.Caja;

public partial class AperturaTurnoViewModel : ObservableObject
{
    [ObservableProperty]
    private decimal _saldoInicial;
}
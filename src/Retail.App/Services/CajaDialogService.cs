using System.Windows;
using Retail.App.ViewModels.Caja;
using Retail.App.Views.Dialogs;
using Retail.Application.DTOs.Caja;

namespace Retail.App.Services;

public class CajaDialogService : ICajaDialogService
{
    public bool? MostrarDialogoApertura(out decimal saldoInicial)
    {
        saldoInicial = 0;
        var dialog = new AperturaTurnoDialog();
        var result = dialog.ShowDialog();

        if (result == true && dialog.DataContext is AperturaTurnoViewModel vm)
        {
            saldoInicial = vm.SaldoInicial;
        }

        return result;
    }

    public MovimientoCajaDto? MostrarDialogoMovimiento(int idTurno)
    {
        var dialog = new MovimientoCajaDialog(idTurno);
        var result = dialog.ShowDialog();

        if (result == true && dialog.DataContext is MovimientoCajaViewModel vm)
        {
            return new MovimientoCajaDto
            {
                IdTurno = idTurno,
                TipoMovimiento = vm.TipoMovimiento,
                Monto = vm.Monto,
                Concepto = vm.Concepto.Trim()
            };
        }

        return null;
    }

    public bool? MostrarDialogoArqueo(int idTurno, out decimal saldoDeclarado, out decimal montoRetenido)
    {
        saldoDeclarado = 0;
        montoRetenido = 0;

        var dialog = new ArqueoCiegoDialog(idTurno);
        var result = dialog.ShowDialog();

        if (result == true && dialog.DataContext is ArqueoCiegoViewModel vm)
        {
            saldoDeclarado = vm.SaldoDeclaradoEfectivo;
            montoRetenido = vm.MontoRetenidoEnCaja;
        }

        return result;
    }

    public void MostrarInformacion(string titulo, string mensaje)
    {
        System.Windows.MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void MostrarError(string titulo, string mensaje)
    {
        System.Windows.MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public void MostrarAdvertencia(string titulo, string mensaje)
    {
        System.Windows.MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
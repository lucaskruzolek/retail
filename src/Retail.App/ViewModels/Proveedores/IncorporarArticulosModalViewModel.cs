using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace Retail.App.ViewModels.Proveedores;

/// <summary>
/// ViewModel para el diálogo modal de parámetros de incorporación de artículos a la tienda (Etapa 2.2).
/// Permite definir el porcentaje de ganancia sugerido y la categoría de destino.
/// </summary>
public partial class IncorporarArticulosModalViewModel : ObservableObject
{
    [ObservableProperty]
    private decimal _porcentajeGanancia = 40m;

    [ObservableProperty]
    private int? _idCategoriaDestino;

    [ObservableProperty]
    private int? _idMarcaDestino;

    public bool DialogResult { get; private set; }

    [RelayCommand]
    private void Aceptar(Window window)
    {
        if (IdCategoriaDestino is null || IdCategoriaDestino <= 0)
        {
            MessageBox.Show("Debe ingresar un ID de categoría de destino válido.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        window.DialogResult = true;
        window.Close();
    }

    [RelayCommand]
    private void Cancelar(Window window)
    {
        DialogResult = false;
        window.DialogResult = false;
        window.Close();
    }
}
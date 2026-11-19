using System.Windows;
using Retail.App.Services;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// Pregunta de tres salidas al cargar una lista con el ticket no vacío (L-14): Reemplazar, Agregar o Cancelar. Con un
/// presupuesto cargado no ofrece Agregar (L-15). Un <c>MessageBox</c> no sirve: no permite rotular los botones.
/// Cerrarlo con la X o con Esc equivale a Cancelar.
/// </summary>
public partial class PreguntaCargaListaDialog : FluentWindow
{
    public PreguntaCargaListaDialog(string nombreLista, bool permitirAgregar)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombreLista);

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;

        InitializeComponent();

        TxtPregunta.Text = $"El ticket ya tiene artículos. ¿Qué hacer con la lista «{nombreLista}»?";
        TxtAyuda.Text = permitirAgregar
            ? "Reemplazar vacía el ticket y carga la lista. Agregar suma las cantidades de la lista a los artículos que ya están."
            : "Hay un presupuesto cargado: la lista reemplaza el ticket y lo desvincula del presupuesto.";
        BtnAgregar.Visibility = permitirAgregar ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Lo que eligió el usuario; <see cref="RespuestaCargaLista.Cancelar"/> si cerró la ventana sin elegir.</summary>
    public RespuestaCargaLista Respuesta { get; private set; } = RespuestaCargaLista.Cancelar;

    private void OnReemplazarClick(object sender, RoutedEventArgs e)
    {
        Respuesta = RespuestaCargaLista.Reemplazar;
        Close();
    }

    private void OnAgregarClick(object sender, RoutedEventArgs e)
    {
        Respuesta = RespuestaCargaLista.Agregar;
        Close();
    }

    private void OnCancelarClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

using System.Windows;
using Retail.App.Helpers;
using Retail.App.ViewModels.Sistema;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// Diálogo modal que explica por qué la base no está lista y ofrece los comandos de <c>Retail.Cli</c> listos para
/// copiar y pegar en PowerShell (RF-23). Se muestra antes del login: no depende de DI ni de <c>MainWindow</c>.
/// La aplicación nunca ejecuta los comandos (D-10/D-11); por eso no hay botón "Ejecutar".
/// </summary>
public partial class DiagnosticoArranqueDialog : FluentWindow
{
    private static readonly TimeSpan DuracionConfirmacion = TimeSpan.FromSeconds(2);

    private readonly List<ComandoCopiableViewModel> _comandos;

    public DiagnosticoArranqueDialog(DiagnosticoArranque diagnostico)
    {
        ArgumentNullException.ThrowIfNull(diagnostico);
        InitializeComponent();

        // Salvaguarda de pantalla para monitores de baja resolución o DPI escalado (SISTEMA_DE_DISENO.md)
        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;

        var formateados = diagnostico.Argumentos.Select(argumentos => ComandoCli.Formatear(argumentos)).ToList();
        _comandos = formateados.Select(f => new ComandoCopiableViewModel(f.Linea)).ToList();

        TxtMensaje.Text = diagnostico.Mensaje;
        ListaComandos.ItemsSource = _comandos;
        PanelComandos.Visibility = _comandos.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        TxtNotaDesarrollo.Visibility = formateados.Any(f => f.RequiereRaizDelRepositorio) ? Visibility.Visible : Visibility.Collapsed;
        BtnCopiarTodos.Visibility = _comandos.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    public string MensajeMostrado => TxtMensaje.Text;
    public IReadOnlyList<string> LineasMostradas => _comandos.Select(c => c.Linea).ToList();
    public Visibility CopiarTodosVisibility => BtnCopiarTodos.Visibility;

    private async void BtnCopiarComando_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { DataContext: ComandoCopiableViewModel comando })
        {
            return;
        }

        comando.TextoBoton = PortapapelesSeguro.TryCopiar(comando.Linea) ? "Copiado" : "No se pudo copiar";
        await Task.Delay(DuracionConfirmacion);
        comando.TextoBoton = ComandoCopiableViewModel.TextoCopiar;
    }

    private async void BtnCopiarTodos_Click(object sender, RoutedEventArgs e)
    {
        var todos = string.Join(Environment.NewLine, _comandos.Select(c => c.Linea));

        TxtBtnCopiarTodos.Text = PortapapelesSeguro.TryCopiar(todos) ? "Copiado" : "No se pudo copiar";
        await Task.Delay(DuracionConfirmacion);
        TxtBtnCopiarTodos.Text = "Copiar todos";
    }

    private void BtnCerrar_Click(object sender, RoutedEventArgs e)
    {
        // Cerrar el diálogo equivale a salir: App.OnStartup llama a Shutdown() al volver de ShowDialog().
        Close();
    }
}

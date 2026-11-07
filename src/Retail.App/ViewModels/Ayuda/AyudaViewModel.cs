using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Retail.App.Services;

namespace Retail.App.ViewModels.Ayuda;

/// <summary>
/// Representa una entrada de atajo rápido de teclado para la guía de mostrador.
/// </summary>
/// <param name="Tecla">Nombre o combinación de teclas (ej. F1, Ctrl+B).</param>
/// <param name="Accion">Acción principal que ejecuta.</param>
/// <param name="Detalle">Descripción breve o módulo asociado.</param>
public record AtajoItemViewModel(string Tecla, string Accion, string Detalle);

/// <summary>
/// ViewModel que gobierna el diálogo modal de Ayuda, Soporte y atajos rápidos de mostrador.
/// </summary>
public partial class AyudaViewModel : ObservableObject
{
    private readonly IAyudaDialogService? _ayudaDialogService;

    public event Action? SolicitarCerrar;

    public string Titulo { get; } = "Ayuda y Soporte";

    public string VersionInfo { get; }

    public IReadOnlyList<AtajoItemViewModel> Atajos { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    public AyudaViewModel(IAyudaDialogService? ayudaDialogService = null)
    {
        _ayudaDialogService = ayudaDialogService;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionStr = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.0.0";
        VersionInfo = $"Retail POS {versionStr} • Cátedra Universitaria";

        Atajos =
        [
            new("F1", "Punto de Venta", "Venta de mostrador y facturación"),
            new("F2", "Artículos", "Consulta de catálogo, precios y stock"),
            new("F4", "Clientes", "Cuentas corrientes y datos de contacto"),
            new("F5", "Caja y Turnos", "Apertura, movimientos y arqueo ciego"),
            new("F6", "Presupuestos", "Emisión y conversión a venta"),
            new("F10", "Usuarios", "Gestión de cuentas y asignación de roles"),
            new("F11", "Copias y Restauraciones", "Seguridad y respaldos de base de datos"),
            new("F12", "Cobrar Venta", "Confirmación de cobro y medios de pago en POS"),
            new("Ctrl+B", "Menú Lateral", "Expandir o contraer la barra de módulos"),
            new("Ctrl+H", "Ayuda", "Abrir este centro de soporte y atajos"),
            new("Esc", "Cerrar / Volver", "Cierra modales o cancela la operación actual")
        ];
    }

    [RelayCommand]
    private void AbrirManualPdf()
    {
        MensajeError = null;

        if (_ayudaDialogService == null)
        {
            MensajeError = "El servicio de ayuda no se encuentra disponible.";
            return;
        }

        var exito = _ayudaDialogService.AbrirManualPdf();
        if (!exito)
        {
            MensajeError = "No se pudo abrir el manual. Verifique que exista el archivo en la instalación o asocie un lector de PDF en Windows.";
        }
    }

    [RelayCommand]
    private void Cerrar()
    {
        SolicitarCerrar?.Invoke();
    }
}


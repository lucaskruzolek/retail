using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Retail.App.Navegacion;
using Retail.App.Services;
using Retail.Domain.Enums;

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

    /// <summary>
    /// Atajos del Shell que no abren una página y por eso no están en <see cref="RegistroNavegacion"/>.
    /// </summary>
    private static readonly IReadOnlyList<AtajoItemViewModel> AtajosDelShell =
    [
        new("Ctrl+B", "Menú lateral", "Expandir o contraer la barra de módulos"),
        new("Ctrl+H", "Ayuda", "Abrir este centro de soporte y atajos"),
        new("Esc", "Cerrar o volver", "Cierra modales o cancela la operación actual")
    ];

    /// <param name="rol">Rol del operador: solo se listan los atajos de las páginas que puede abrir.</param>
    /// <param name="ayudaDialogService">Servicio para abrir el manual, o <c>null</c> si no está disponible.</param>
    public AyudaViewModel(RolUsuarioEnum? rol, IAyudaDialogService? ayudaDialogService = null)
    {
        _ayudaDialogService = ayudaDialogService;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionStr = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.0.0";
        VersionInfo = $"Retail POS {versionStr} • Cátedra Universitaria";

        // Los atajos de páginas salen del registro de navegación, la misma fuente que usa el Shell para atenderlos:
        // así el diálogo no puede mostrar una tecla que no existe o que el rol no tiene permitida.
        var atajosDePaginas = RegistroNavegacion.AtajosVisibles(rol)
            .Select(x => new AtajoItemViewModel(
                x.Pagina.Atajo!,
                x.Pagina.Titulo,
                x.Seccion.Titulo == x.Pagina.Titulo ? string.Empty : x.Seccion.Titulo));

        Atajos = [.. atajosDePaginas, .. AtajosDelShell];
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


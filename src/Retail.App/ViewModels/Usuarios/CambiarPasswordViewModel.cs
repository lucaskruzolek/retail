using CommunityToolkit.Mvvm.ComponentModel;
using Retail.Application.DTOs.Usuarios;

namespace Retail.App.ViewModels.Usuarios;

/// <summary>
/// ViewModel para el diálogo modal de reseteo o cambio de contraseña de un operador.
/// </summary>
public partial class CambiarPasswordViewModel : ObservableObject
{
    [ObservableProperty]
    private int _idUsuario;

    [ObservableProperty]
    private string _nombreUsuario = string.Empty;

    [ObservableProperty]
    private string _nombreCompleto = string.Empty;

    [ObservableProperty]
    private string _nuevaPassword = string.Empty;

    [ObservableProperty]
    private string _confirmarPassword = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    public Func<Task>? OnGuardarAsync { get; set; }

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    public void Configurar(UsuarioDto usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        IdUsuario = usuario.IdUsuario;
        NombreUsuario = usuario.NombreUsuario;
        NombreCompleto = usuario.NombreCompleto;
        NuevaPassword = string.Empty;
        ConfirmarPassword = string.Empty;
        MensajeError = null;
        IsBusy = false;
        OnGuardarAsync = null;
    }

    public bool Validar()
    {
        MensajeError = null;

        if (string.IsNullOrWhiteSpace(NuevaPassword) || NuevaPassword.Length < 6)
        {
            MensajeError = "La contraseña debe tener una longitud mínima de 6 caracteres.";
            return false;
        }

        if (NuevaPassword != ConfirmarPassword)
        {
            MensajeError = "Las contraseñas ingresadas no coinciden.";
            return false;
        }

        return true;
    }

    public CambiarPasswordDto ObtenerDto() => new()
    {
        IdUsuario = IdUsuario,
        NuevaPassword = NuevaPassword
    };
}

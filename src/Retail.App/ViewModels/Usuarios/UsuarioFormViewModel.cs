using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using Retail.Application.DTOs.Usuarios;
using Retail.Domain.Enums;

namespace Retail.App.ViewModels.Usuarios;

/// <summary>
/// ViewModel para el diálogo modal de creación o edición de operadores del sistema.
/// </summary>
public partial class UsuarioFormViewModel : ObservableObject
{
    [GeneratedRegex(@"^[a-zA-Z0-9_\.]+$")]
    private static partial Regex UsernameRegex();

    [ObservableProperty]
    private int _idUsuario;

    [ObservableProperty]
    private string _nombreUsuario = string.Empty;

    [ObservableProperty]
    private string _nombreCompleto = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private RolUsuarioEnum _rol = RolUsuarioEnum.Cajero;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsAlta))]
    [NotifyPropertyChangedFor(nameof(TituloVentana))]
    private bool _esModoEdicion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    public bool EsAlta => !EsModoEdicion;

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    public string TituloVentana => EsModoEdicion ? "Modificar Operador" : "Alta de Operador";

    public Func<Task>? OnGuardarAsync { get; set; }

    public IReadOnlyList<RolUsuarioEnum> RolesDisponibles { get; } = Enum.GetValues<RolUsuarioEnum>();

    partial void OnNombreUsuarioChanged(string value)
    {
        if (value.Contains(' ', StringComparison.Ordinal))
        {
            NombreUsuario = value.Replace(" ", string.Empty, StringComparison.Ordinal);
        }
    }

    public void ConfigurarAlta()
    {
        EsModoEdicion = false;
        IdUsuario = 0;
        NombreUsuario = string.Empty;
        NombreCompleto = string.Empty;
        Password = string.Empty;
        Rol = RolUsuarioEnum.Cajero;
        MensajeError = null;
        IsBusy = false;
        OnGuardarAsync = null;
    }

    public void ConfigurarEdicion(UsuarioDto usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        EsModoEdicion = true;
        IdUsuario = usuario.IdUsuario;
        NombreUsuario = usuario.NombreUsuario;
        NombreCompleto = usuario.NombreCompleto;
        Password = string.Empty;
        Rol = usuario.Rol;
        MensajeError = null;
        IsBusy = false;
        OnGuardarAsync = null;
    }

    public bool Validar()
    {
        MensajeError = null;

        var username = NombreUsuario.Trim();
        if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
        {
            MensajeError = "El nombre de usuario debe contener al menos 3 caracteres.";
            return false;
        }

        if (username.Length > 50)
        {
            MensajeError = "El nombre de usuario no puede exceder 50 caracteres.";
            return false;
        }

        if (!UsernameRegex().IsMatch(username))
        {
            MensajeError = "El nombre de usuario solo puede contener letras, números, puntos o guiones bajos (sin espacios).";
            return false;
        }

        var nombreCompleto = NombreCompleto.Trim();
        if (string.IsNullOrWhiteSpace(nombreCompleto) || nombreCompleto.Length < 3)
        {
            MensajeError = "El nombre completo debe contener al menos 3 caracteres.";
            return false;
        }

        if (nombreCompleto.Length > 100)
        {
            MensajeError = "El nombre completo no puede exceder 100 caracteres.";
            return false;
        }

        if (!EsModoEdicion && (string.IsNullOrWhiteSpace(Password) || Password.Length < 6))
        {
            MensajeError = "La contraseña debe contener al menos 6 caracteres.";
            return false;
        }

        return true;
    }

    public CrearUsuarioDto ObtenerCrearDto() => new()
    {
        NombreUsuario = NombreUsuario.Trim(),
        NombreCompleto = NombreCompleto.Trim(),
        Password = Password,
        Rol = Rol
    };

    public ModificarUsuarioDto ObtenerModificarDto() => new()
    {
        IdUsuario = IdUsuario,
        NombreCompleto = NombreCompleto.Trim(),
        Rol = Rol
    };
}

using CommunityToolkit.Mvvm.ComponentModel;
using FluentValidation;
using Retail.Application.DTOs.Usuarios;
using Retail.Domain.Enums;

namespace Retail.App.ViewModels.Usuarios;

/// <summary>
/// ViewModel para el diálogo modal de creación o edición de operadores del sistema.
/// No replica las reglas de negocio: las valida <c>UsuarioService</c> con los validadores de Application, que a
/// su vez reutilizan las reglas del Dominio. Acá solo se controla lo que el servicio no puede ver.
/// </summary>
public partial class UsuarioFormViewModel : ObservableObject
{
    [ObservableProperty]
    private int _idUsuario;

    [ObservableProperty]
    private string _nombreUsuario = string.Empty;

    [ObservableProperty]
    private string _nombre = string.Empty;

    [ObservableProperty]
    private string _apellido = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmarPassword = string.Empty;

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
        Nombre = string.Empty;
        Apellido = string.Empty;
        Password = string.Empty;
        ConfirmarPassword = string.Empty;
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
        Nombre = usuario.Nombre;
        Apellido = usuario.Apellido;
        Password = string.Empty;
        ConfirmarPassword = string.Empty;
        Rol = usuario.Rol;
        MensajeError = null;
        IsBusy = false;
        OnGuardarAsync = null;
    }

    /// <summary>
    /// Control previo al envío. La confirmación de la contraseña es una ayuda de la interfaz contra errores de
    /// tipeo: no viaja en el DTO, así que es lo único que el servicio no puede validar.
    /// </summary>
    public bool Validar()
    {
        MensajeError = null;

        if (EsAlta && Password != ConfirmarPassword)
        {
            MensajeError = "Las contraseñas ingresadas no coinciden.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Muestra el error devuelto por el servicio. Una <see cref="ValidationException"/> trae todos los campos
    /// inválidos a la vez, y se listan uno por línea en lugar del texto técnico de su propiedad Message.
    /// </summary>
    public void InformarError(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        MensajeError = ex is ValidationException validacion && validacion.Errors.Any()
            ? string.Join("\n", validacion.Errors.Select(e => e.ErrorMessage).Distinct())
            : ex.Message;
    }

    public CrearUsuarioDto ObtenerCrearDto() => new()
    {
        NombreUsuario = NombreUsuario,
        Nombre = Nombre,
        Apellido = Apellido,
        Password = Password,
        Rol = Rol
    };

    public ModificarUsuarioDto ObtenerModificarDto() => new()
    {
        IdUsuario = IdUsuario,
        Nombre = Nombre,
        Apellido = Apellido,
        Rol = Rol
    };
}

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
    [NotifyPropertyChangedFor(nameof(EtiquetaEmail))]
    private RolUsuarioEnum _rol = RolUsuarioEnum.Cajero;

    /// <summary>
    /// Roles que el operador de la sesión puede asignar, calculados por <c>UsuarioService</c> con la matriz del
    /// Dominio. El formulario no decide permisos: solo evita ofrecer opciones que el servicio rechazaría.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<RolUsuarioEnum> _rolesDisponibles = [];

    [ObservableProperty]
    private string _emailRecuperacion = string.Empty;

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

    /// <summary>
    /// Anticipa la regla "un Gerente tiene email" en la etiqueta del campo; la valida el servicio.
    /// </summary>
    public string EtiquetaEmail => Rol == RolUsuarioEnum.Gerente
        ? "Email de Recuperación (obligatorio para Gerente)"
        : "Email de Recuperación (opcional)";

    partial void OnNombreUsuarioChanged(string value)
    {
        if (value.Contains(' ', StringComparison.Ordinal))
        {
            NombreUsuario = value.Replace(" ", string.Empty, StringComparison.Ordinal);
        }
    }

    public void ConfigurarAlta(IReadOnlyList<RolUsuarioEnum> rolesAsignables)
    {
        ArgumentNullException.ThrowIfNull(rolesAsignables);
        RolesDisponibles = rolesAsignables;
        EsModoEdicion = false;
        IdUsuario = 0;
        NombreUsuario = string.Empty;
        Nombre = string.Empty;
        Apellido = string.Empty;
        Password = string.Empty;
        ConfirmarPassword = string.Empty;
        // El rol con menos privilegios que el operador puede asignar: dar más poder debe ser una elección explícita.
        Rol = rolesAsignables.Count > 0 ? rolesAsignables.Min() : RolUsuarioEnum.Cajero;
        EmailRecuperacion = string.Empty;
        MensajeError = null;
        IsBusy = false;
        OnGuardarAsync = null;
    }

    public void ConfigurarEdicion(UsuarioDto usuario, IReadOnlyList<RolUsuarioEnum> rolesAsignables)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        ArgumentNullException.ThrowIfNull(rolesAsignables);
        RolesDisponibles = rolesAsignables;
        EsModoEdicion = true;
        IdUsuario = usuario.IdUsuario;
        NombreUsuario = usuario.NombreUsuario;
        Nombre = usuario.Nombre;
        Apellido = usuario.Apellido;
        Password = string.Empty;
        ConfirmarPassword = string.Empty;
        Rol = usuario.Rol;
        EmailRecuperacion = usuario.EmailRecuperacion ?? string.Empty;
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
        Rol = Rol,
        EmailRecuperacion = EmailRecuperacion
    };

    public ModificarUsuarioDto ObtenerModificarDto() => new()
    {
        IdUsuario = IdUsuario,
        Nombre = Nombre,
        Apellido = Apellido,
        Rol = Rol,
        EmailRecuperacion = EmailRecuperacion
    };
}

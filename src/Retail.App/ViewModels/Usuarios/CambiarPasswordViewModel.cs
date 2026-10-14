using CommunityToolkit.Mvvm.ComponentModel;
using FluentValidation;
using Retail.Application.DTOs.Usuarios;

namespace Retail.App.ViewModels.Usuarios;

/// <summary>
/// ViewModel para el diálogo modal de cambio de contraseña (RF-03, D-6). Tiene tres usos:
/// el blanqueo que un Gerente o un Administrador hace sobre otra cuenta, el cambio obligatorio que el titular hace
/// al iniciar sesión cuando su contraseña la eligió otra persona, y el cambio voluntario de la propia contraseña,
/// que exige la contraseña actual.
/// </summary>
public partial class CambiarPasswordViewModel : ObservableObject
{
    private enum Modo
    {
        Blanqueo,
        CambioObligatorio,
        CambioPropio
    }

    [ObservableProperty]
    private int _idUsuario;

    [ObservableProperty]
    private string _nombreUsuario = string.Empty;

    [ObservableProperty]
    private string _nombreCompleto = string.Empty;

    [ObservableProperty]
    private string _passwordActual = string.Empty;

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

    private Modo _modoActual = Modo.Blanqueo;

    // El modo es un detalle interno: la vista se enlaza a las propiedades derivadas, que se notifican al cambiarlo.
    private Modo ModoActual
    {
        get => _modoActual;
        set
        {
            if (SetProperty(ref _modoActual, value))
            {
                OnPropertyChanged(nameof(EsCambioObligatorio));
                OnPropertyChanged(nameof(RequierePasswordActual));
                OnPropertyChanged(nameof(TituloVentana));
                OnPropertyChanged(nameof(EtiquetaOperador));
                OnPropertyChanged(nameof(TextoBotonGuardar));
            }
        }
    }

    public bool EsCambioObligatorio => ModoActual == Modo.CambioObligatorio;

    /// <summary>
    /// Solo el cambio voluntario pide la contraseña actual en pantalla. En el cambio obligatorio el titular acaba
    /// de escribirla en el login, y quien llama la completa en el DTO.
    /// </summary>
    public bool RequierePasswordActual => ModoActual == Modo.CambioPropio;

    public string TituloVentana => ModoActual switch
    {
        Modo.CambioObligatorio => "Cambio de Contraseña Obligatorio",
        Modo.CambioPropio => "Cambiar Mi Contraseña",
        _ => "Restablecer Contraseña"
    };

    public string EtiquetaOperador => ModoActual == Modo.Blanqueo ? "Operador Seleccionado:" : "Su cuenta:";

    public string TextoBotonGuardar => ModoActual switch
    {
        Modo.CambioObligatorio => "Cambiar e Ingresar",
        Modo.CambioPropio => "Cambiar",
        _ => "Restablecer"
    };

    public bool IsNotBusy => !IsBusy;

    public Func<Task>? OnGuardarAsync { get; set; }

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    /// <summary>
    /// Blanqueo: un Gerente o un Administrador asigna una contraseña temporal a otra cuenta.
    /// </summary>
    public void Configurar(UsuarioDto usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        Inicializar(usuario.IdUsuario, usuario.NombreUsuario, usuario.NombreCompleto, Modo.Blanqueo);
    }

    /// <summary>
    /// Cambio voluntario de la propia contraseña, desde una sesión ya iniciada.
    /// </summary>
    public void ConfigurarCambioPropio(UsuarioDto usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        Inicializar(usuario.IdUsuario, usuario.NombreUsuario, usuario.NombreCompleto, Modo.CambioPropio);
    }

    /// <summary>
    /// Modo de inicio de sesión: el titular elige su propia contraseña antes de entrar al sistema.
    /// </summary>
    public void ConfigurarCambioObligatorio(int idUsuario, string nombreUsuario, string nombreCompleto)
    {
        Inicializar(idUsuario, nombreUsuario, nombreCompleto, Modo.CambioObligatorio);
    }

    private void Inicializar(int idUsuario, string nombreUsuario, string nombreCompleto, Modo modo)
    {
        IdUsuario = idUsuario;
        NombreUsuario = nombreUsuario;
        NombreCompleto = nombreCompleto;
        ModoActual = modo;
        PasswordActual = string.Empty;
        NuevaPassword = string.Empty;
        ConfirmarPassword = string.Empty;
        MensajeError = null;
        IsBusy = false;
        OnGuardarAsync = null;
    }

    public bool Validar()
    {
        MensajeError = null;

        if (RequierePasswordActual && string.IsNullOrEmpty(PasswordActual))
        {
            MensajeError = "Ingrese su contraseña actual.";
            return false;
        }

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

    /// <summary>
    /// Muestra el error devuelto por el servicio: los mensajes de una <see cref="ValidationException"/> se listan
    /// uno por línea en lugar del texto técnico de su propiedad Message.
    /// </summary>
    public void InformarError(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        MensajeError = ex is ValidationException validacion && validacion.Errors.Any()
            ? string.Join("\n", validacion.Errors.Select(e => e.ErrorMessage).Distinct())
            : ex.Message;
    }

    public CambiarPasswordDto ObtenerDto() => new()
    {
        IdUsuario = IdUsuario,
        NuevaPassword = NuevaPassword,
        PasswordActual = RequierePasswordActual ? PasswordActual : null
    };
}

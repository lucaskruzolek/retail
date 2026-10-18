using CommunityToolkit.Mvvm.ComponentModel;
using Retail.Application.DTOs.Backups;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Backups;

/// <summary>
/// ViewModel del diálogo que pide una credencial antes de una operación sensible sobre la base (D-8). Sirve a tres
/// casos: la copia manual (solo la contraseña propia), la restauración (aviso destacado + palabra de confirmación) y la
/// restauración que un Administrador ejecuta con la autorización de un Gerente (usuario y contraseña de ese Gerente, D-30).
/// </summary>
/// <remarks>
/// El diálogo ejecuta la operación en lugar de solo devolver la contraseña porque quien la valida es el servicio de
/// Aplicación al reautenticar: si fuera incorrecta, el Gerente ve el error en el mismo diálogo y reintenta sin
/// reabrirlo. También es el lugar natural de la barra de progreso.
/// </remarks>
public partial class ConfirmarCredencialViewModel : ObservableObject
{
    /// <summary>
    /// Palabra que el Gerente debe escribir para confirmar una restauración (un gesto deliberado, no un clic de más).
    /// </summary>
    public const string FraseRestauracion = "RESTAURAR";

    private readonly Func<CredencialAutorizanteDto, IProgress<int>, Task> _operacion;

    public ConfirmarCredencialViewModel(
        Func<string, IProgress<int>, Task> operacion,
        string titulo,
        string mensaje,
        string textoBoton,
        string? aviso = null,
        bool exigeFrase = false)
        : this(SoloPassword(operacion), titulo, mensaje, textoBoton, aviso, exigeFrase, pideUsuarioAutorizante: false)
    {
    }

    private ConfirmarCredencialViewModel(
        Func<CredencialAutorizanteDto, IProgress<int>, Task> operacion,
        string titulo,
        string mensaje,
        string textoBoton,
        string? aviso,
        bool exigeFrase,
        bool pideUsuarioAutorizante)
    {
        _operacion = operacion ?? throw new ArgumentNullException(nameof(operacion));
        Titulo = titulo;
        Mensaje = mensaje;
        TextoBoton = textoBoton;
        Aviso = aviso;
        ExigeFrase = exigeFrase;
        PideUsuarioAutorizante = pideUsuarioAutorizante;
    }

    public string Titulo { get; }

    public string Mensaje { get; }

    public string TextoBoton { get; }

    /// <summary>
    /// Advertencia que se muestra destacada (en la restauración, la conciliación fiscal con ARCA).
    /// </summary>
    public string? Aviso { get; }

    public bool TieneAviso => !string.IsNullOrWhiteSpace(Aviso);

    /// <summary>
    /// Exigir la palabra <see cref="FraseRestauracion"/> marca además la operación como destructiva (botón de peligro).
    /// </summary>
    public bool ExigeFrase { get; }

    public bool EsDestructiva => ExigeFrase;

    public bool NoEsDestructiva => !ExigeFrase;

    /// <summary>
    /// El diálogo pide además el usuario del Gerente que autoriza (restauración ejecutada por un Administrador, D-30).
    /// </summary>
    public bool PideUsuarioAutorizante { get; }

    /// <summary>
    /// Etiqueta del campo de contraseña: en el modo autorizado es la del Gerente; en los otros, la de quien opera.
    /// </summary>
    public string EtiquetaPassword => PideUsuarioAutorizante ? "Contraseña del Gerente" : "Contraseña";

    public string PlaceholderPassword => PideUsuarioAutorizante ? "Contraseña del Gerente" : "Su contraseña";

    [ObservableProperty]
    private string _nombreUsuarioAutorizante = string.Empty;

    [ObservableProperty]
    private string _frase = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    private int _progreso;

    public bool IsNotBusy => !IsBusy;

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    /// <summary>
    /// La operación terminó con éxito.
    /// </summary>
    public bool Completada { get; private set; }

    /// <summary>
    /// Falla de la operación que no es una contraseña incorrecta: cierra el diálogo y la informa quien lo abrió.
    /// </summary>
    public Exception? FalloOperacion { get; private set; }

    /// <summary>
    /// Valida lo que se escribió. La contraseña correcta la decide el servicio, no la pantalla.
    /// </summary>
    public bool Validar()
    {
        MensajeError = null;

        if (ExigeFrase && !string.Equals(Frase.Trim(), FraseRestauracion, StringComparison.Ordinal))
        {
            MensajeError = $"Escriba la palabra {FraseRestauracion} para confirmar.";
            return false;
        }

        if (PideUsuarioAutorizante && string.IsNullOrWhiteSpace(NombreUsuarioAutorizante))
        {
            MensajeError = "Ingrese el usuario del Gerente que autoriza.";
            return false;
        }

        if (string.IsNullOrEmpty(Password))
        {
            MensajeError = PideUsuarioAutorizante ? "Ingrese la contraseña del Gerente." : "Ingrese su contraseña.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Ejecuta la operación con la credencial ingresada. Devuelve <c>true</c> si el diálogo debe cerrarse (la operación
    /// terminó, bien o mal) y <c>false</c> si debe quedar abierto para corregir algo: un dato faltante o una contraseña
    /// incorrecta.
    /// </summary>
    public async Task<bool> EjecutarAsync()
    {
        if (!Validar())
        {
            return false;
        }

        IsBusy = true;
        Progreso = 0;

        try
        {
            await _operacion(new CredencialAutorizanteDto(NombreUsuarioAutorizante.Trim(), Password), new Progress<int>(valor => Progreso = Math.Clamp(valor, 0, 100)));
            Completada = true;
            return true;
        }
        catch (CredencialesInvalidasException ex)
        {
            // La contraseña se corrige en el mismo diálogo (el usuario se conserva); la operación no empezó.
            MensajeError = ex.Message;
            Password = string.Empty;
            return false;
        }
        catch (Exception ex)
        {
            FalloOperacion = ex;
            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Diálogo de la copia manual: solo se pide la contraseña (D-8).
    /// </summary>
    public static ConfirmarCredencialViewModel ParaBackupManual(Func<string, IProgress<int>, Task> operacion)
    {
        return new ConfirmarCredencialViewModel(
            operacion,
            titulo: "Crear copia de seguridad",
            mensaje: "Se generará una copia completa de la base de datos. Por seguridad, confirme su contraseña.",
            textoBoton: "Crear copia");
    }

    /// <summary>
    /// Diálogo de la restauración: explica el alcance, muestra el aviso fiscal y exige escribir la palabra de confirmación.
    /// </summary>
    public static ConfirmarCredencialViewModel ParaRestauracion(
        Func<string, IProgress<int>, Task> operacion,
        string fechaCopia,
        string avisoConciliacionArca)
    {
        return new ConfirmarCredencialViewModel(
            operacion,
            titulo: "Restaurar copia de seguridad",
            mensaje: $"Se reemplazarán TODOS los datos actuales por los de la copia del {fechaCopia}. Todo lo registrado " +
                "después de esa fecha (ventas, compras, movimientos de caja, cambios de stock y de usuarios) dejará de " +
                "estar en el sistema. Antes de reemplazar se toma una copia de la base actual y la aplicación se reiniciará.",
            textoBoton: "Restaurar",
            aviso: avisoConciliacionArca,
            exigeFrase: true);
    }

    /// <summary>
    /// Diálogo de la restauración que ejecuta un Administrador: igual a <see cref="ParaRestauracion"/>, pero además pide
    /// el usuario y la contraseña de un Gerente que la autorice (control dual, D-30).
    /// </summary>
    public static ConfirmarCredencialViewModel ParaRestauracionAutorizada(
        Func<CredencialAutorizanteDto, IProgress<int>, Task> operacion,
        string fechaCopia,
        string avisoConciliacionArca)
    {
        return new ConfirmarCredencialViewModel(
            operacion,
            titulo: "Restaurar copia de seguridad",
            mensaje: $"Se reemplazarán TODOS los datos actuales por los de la copia del {fechaCopia}. Todo lo registrado " +
                "después de esa fecha (ventas, compras, movimientos de caja, cambios de stock y de usuarios) dejará de " +
                "estar en el sistema. Antes de reemplazar se toma una copia de la base actual y la aplicación se reiniciará.\n\n" +
                "Un Gerente debe ingresar SU usuario y SU contraseña para autorizar esta restauración.",
            textoBoton: "Restaurar",
            aviso: avisoConciliacionArca,
            exigeFrase: true,
            pideUsuarioAutorizante: true);
    }

    private static Func<CredencialAutorizanteDto, IProgress<int>, Task> SoloPassword(Func<string, IProgress<int>, Task> operacion)
    {
        ArgumentNullException.ThrowIfNull(operacion);
        return (credencial, progreso) => operacion(credencial.Password, progreso);
    }
}

namespace Retail.App.Services;

/// <summary>
/// Cuadros de aviso de la sesión única y del latido (MT-3, MT-6, MT-12). Se abstraen para poder probar el arranque y el
/// latido sin abrir ventanas: la implementación WPF usa <see cref="System.Windows.MessageBox"/>.
/// </summary>
public interface IAvisosSesion
{
    /// <summary>Rechazo al iniciar: tras cerrarlo, la aplicación termina.</summary>
    void RechazarInicio(string mensaje);

    /// <summary>Aviso informativo: la aplicación sigue abierta.</summary>
    void Informar(string mensaje);

    /// <summary>
    /// La sesión se perdió (otra terminal la tomó o un Gerente la cerró): muestra el aviso modal y cierra la aplicación.
    /// Se ejecuta en el hilo de la UI; la tarea termina cuando el usuario aceptó el aviso.
    /// </summary>
    Task AvisarSesionPerdidaYSalirAsync();

    /// <summary>Aviso no bloqueante de un mantenimiento programado: devuelve enseguida, sin esperar al usuario.</summary>
    void AvisarMantenimiento(string mensaje);
}

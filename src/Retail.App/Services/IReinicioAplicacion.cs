namespace Retail.App.Services;

/// <summary>
/// Reinicia la aplicación. Después de restaurar una copia, las conexiones abiertas y las pantallas en memoria apuntan a
/// la base reemplazada, así que la única salida segura es arrancar de nuevo. Vive detrás de una interfaz para que las
/// pruebas no reinicien nada.
/// </summary>
public interface IReinicioAplicacion
{
    /// <summary>
    /// Inicia una nueva instancia del ejecutable y cierra la actual.
    /// </summary>
    void Reiniciar();
}

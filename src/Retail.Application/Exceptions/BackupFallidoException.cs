namespace Retail.Application.Exceptions;

/// <summary>
/// La copia de seguridad no pudo completarse o no pasó la verificación (RF-22). Es una excepción de Aplicación:
/// no viola una regla de negocio, sino que informa un fallo del motor o del disco. El intento queda registrado
/// como <c>Fallido</c> en el historial.
/// </summary>
public class BackupFallidoException : Exception
{
    public BackupFallidoException(string message)
        : base(message)
    {
    }

    public BackupFallidoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

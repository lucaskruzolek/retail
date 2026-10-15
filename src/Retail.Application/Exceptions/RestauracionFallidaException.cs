namespace Retail.Application.Exceptions;

/// <summary>
/// La restauración se rechazó o no pudo completarse (RF-22): la copia no es restaurable, el archivo cambió, el esquema
/// es más nuevo que la aplicación, no hay espacio, etc. Si ocurre antes del intercambio, la base vigente queda intacta
/// y la base temporal se descarta (D-17).
/// </summary>
public class RestauracionFallidaException : Exception
{
    public RestauracionFallidaException(string message)
        : base(message)
    {
    }

    public RestauracionFallidaException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

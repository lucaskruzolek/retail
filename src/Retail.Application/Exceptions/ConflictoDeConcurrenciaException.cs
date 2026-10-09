namespace Retail.Application.Exceptions;

/// <summary>
/// Otro usuario modificó los mismos datos entre la lectura y el guardado (control de concurrencia optimista, D-12).
/// Es una excepción de Aplicación y no de Dominio: no viola ninguna regla de negocio, y la operación se puede
/// reintentar con los datos actualizados. La Infraestructura la usa para no exponer el tipo de EF Core
/// (<c>DbUpdateConcurrencyException</c>) a las capas superiores (Ley 1).
/// </summary>
public class ConflictoDeConcurrenciaException : Exception
{
    public ConflictoDeConcurrenciaException()
        : base("Otro usuario modificó los mismos datos mientras se procesaba la operación. Vuelva a intentarlo.")
    {
    }

    public ConflictoDeConcurrenciaException(string message)
        : base(message)
    {
    }

    public ConflictoDeConcurrenciaException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

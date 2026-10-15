namespace Retail.Application.Exceptions;

/// <summary>
/// El guardado violó un índice único: otro proceso insertó un dato equivalente entre la comprobación y el guardado
/// (por ejemplo, dos copias de seguridad que pasan a la vez el control de "copia en curso"). Es una excepción de
/// Aplicación y no de Dominio: no viola una regla de negocio por sí sola, y quien la captura sabe qué regla protegía
/// el índice y la traduce a un mensaje para el operador. La Infraestructura la usa para no exponer
/// <c>DbUpdateException</c> de EF Core a las capas superiores (Ley 1).
/// </summary>
public class RestriccionDeUnicidadException : Exception
{
    public RestriccionDeUnicidadException()
        : base("Ya existe un dato equivalente: la operación choca con otra realizada al mismo tiempo.")
    {
    }

    public RestriccionDeUnicidadException(string message)
        : base(message)
    {
    }

    public RestriccionDeUnicidadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

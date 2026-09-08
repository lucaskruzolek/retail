namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando una operación de baja o cambio de rol dejaría al sistema sin ningún operador con rol Gerente.
/// </summary>
public class UltimoGerenteException : DomainException
{
    public int UsuarioId { get; }

    public UltimoGerenteException(int usuarioId)
        : base($"No se puede eliminar ni revocar el rol al usuario ID {usuarioId} porque es el único Gerente activo del sistema.")
    {
        UsuarioId = usuarioId;
    }

    public UltimoGerenteException(string message)
        : base(message)
    {
    }
}

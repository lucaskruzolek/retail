namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando un usuario con baja lógica intenta autenticarse u operar en el sistema.
/// </summary>
public class UsuarioInactivoException : DomainException
{
    public string NombreUsuario { get; }

    public UsuarioInactivoException(string nombreUsuario)
        : base($"El usuario '{nombreUsuario}' se encuentra inactivo o dado de baja.")
    {
        NombreUsuario = nombreUsuario;
    }

    public UsuarioInactivoException(string nombreUsuario, string message)
        : base(message)
    {
        NombreUsuario = nombreUsuario;
    }
}

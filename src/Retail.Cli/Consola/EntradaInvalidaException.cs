namespace Retail.Cli.Consola;

/// <summary>
/// El operador ingresó datos que la herramienta no puede usar (por ejemplo, contraseñas que no coinciden).
/// Es un error esperado: se informa sin stack trace, igual que una validación de negocio.
/// </summary>
internal sealed class EntradaInvalidaException : Exception
{
    public EntradaInvalidaException(string message)
        : base(message)
    {
    }
}

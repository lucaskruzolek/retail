namespace Retail.Cli;

/// <summary>
/// Códigos de salida del proceso: los scripts y el Programador de tareas los usan para saber si el comando
/// funcionó sin leer la salida de texto.
/// </summary>
internal static class CodigoSalida
{
    public const int Exito = 0;

    /// <summary>
    /// El comando no pudo completarse (base inaccesible, datos inválidos, regla de negocio).
    /// </summary>
    public const int Fallo = 1;

    /// <summary>
    /// El comando no existe o le faltan argumentos.
    /// </summary>
    public const int UsoIncorrecto = 2;
}

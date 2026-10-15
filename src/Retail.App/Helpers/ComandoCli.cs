using System.IO;

namespace Retail.App.Helpers;

/// <summary>
/// Línea de comando lista para pegar en PowerShell y si, para que funcione, hay que estar parado en la raíz del repositorio.
/// </summary>
/// <param name="Linea">Texto completo a pegar en la consola.</param>
/// <param name="RequiereRaizDelRepositorio"><c>true</c> en desarrollo (<c>dotnet run --project</c> usa una ruta relativa).</param>
public sealed record ComandoCliFormateado(string Linea, bool RequiereRaizDelRepositorio);

/// <summary>
/// Arma la línea con la que se invoca <c>Retail.Cli</c> desde una consola recién abierta. La aplicación nunca la ejecuta
/// (D-10/D-11): solo la muestra para que el operador la copie.
/// </summary>
public static class ComandoCli
{
    private const string NombreEjecutable = "Retail.Cli.exe";
    private const string ProyectoDesarrollo = "src/Retail.Cli";

    /// <summary>
    /// Instalación publicada (<c>Retail.Cli.exe</c> junto a la aplicación): ruta completa, siempre entre comillas y con
    /// el operador de llamada <c>&amp;</c> de PowerShell, porque la ruta puede tener espacios y PowerShell no ejecuta
    /// una cadena entrecomillada sin él. Sin el ejecutable (desarrollo): <c>dotnet run --project</c>.
    /// </summary>
    /// <param name="argumentos">Argumentos de la CLI, por ejemplo <c>migrar</c>.</param>
    /// <param name="directorioBase">Carpeta de la aplicación; <c>null</c> usa <see cref="AppContext.BaseDirectory"/>.</param>
    /// <param name="existeArchivo">Comprobación de existencia de archivo; <c>null</c> usa <see cref="File.Exists(string?)"/>.</param>
    public static ComandoCliFormateado Formatear(
        string argumentos,
        string? directorioBase = null,
        Func<string, bool>? existeArchivo = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argumentos);
        existeArchivo ??= File.Exists;

        var rutaEjecutable = Path.Combine(directorioBase ?? AppContext.BaseDirectory, NombreEjecutable);
        if (existeArchivo(rutaEjecutable))
        {
            return new ComandoCliFormateado($"& \"{rutaEjecutable}\" {argumentos}", false);
        }

        return new ComandoCliFormateado($"dotnet run --project {ProyectoDesarrollo} -- {argumentos}", true);
    }
}

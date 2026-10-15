namespace Retail.Infrastructure.Logging;

/// <summary>
/// Única regla de ubicación de los archivos de registro (Serilog) para la aplicación y para <c>Retail.Cli</c>.
/// Los registros van a <c>%LOCALAPPDATA%\Retail POS\logs</c>, nunca junto al ejecutable: Program Files es solo para
/// binarios, y un proceso privilegiado (la tarea programada corre como SYSTEM) no debe escribir en una carpeta que un
/// usuario sin privilegios pueda controlar (podría redirigir las escrituras con un punto de montaje).
/// </summary>
/// <remarks>
/// <c>LocalApplicationData</c> se resuelve contra la cuenta que ejecuta el proceso: para un usuario es
/// <c>C:\Users\&lt;usuario&gt;\AppData\Local</c> y para SYSTEM es
/// <c>C:\Windows\System32\config\systemprofile\AppData\Local</c>, que solo SYSTEM y los administradores pueden escribir.
/// Por eso no hace falta detectar la identidad ni tocar permisos.
/// </remarks>
public static class UbicacionDeLogs
{
    /// <summary>Nombre de la carpeta de la aplicación dentro de la carpeta base.</summary>
    public const string CarpetaDeLaAplicacion = "Retail POS";

    /// <summary>Nombre de la subcarpeta que contiene los archivos de registro.</summary>
    public const string CarpetaDeLogs = "logs";

    /// <summary>
    /// Carpeta de registros del usuario que ejecuta el proceso.
    /// </summary>
    public static string Directorio()
    {
        return Directorio(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
    }

    /// <summary>
    /// Carpeta de registros dentro de la carpeta base indicada. Si la carpeta base está vacía (hay cuentas sin perfil
    /// que devuelven una cadena vacía) se usa la carpeta temporal del sistema, para que el registro nunca falle ni caiga
    /// en el directorio de trabajo.
    /// </summary>
    /// <param name="carpetaBase">Equivale a <c>%LOCALAPPDATA%</c>; se inyecta para poder probar la regla.</param>
    public static string Directorio(string? carpetaBase)
    {
        var raiz = string.IsNullOrWhiteSpace(carpetaBase) ? Path.GetTempPath() : carpetaBase;
        return Path.Combine(raiz, CarpetaDeLaAplicacion, CarpetaDeLogs);
    }
}

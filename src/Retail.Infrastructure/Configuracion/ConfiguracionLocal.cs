using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace Retail.Infrastructure.Configuracion;

/// <summary>
/// Configuración propia de cada PC (MT-7, H17): un <c>appsettings.local.json</c> opcional que vive fuera de la carpeta de
/// la aplicación, así que actualizar la aplicación (descomprimir un ZIP nuevo) nunca lo pisa.
/// </summary>
/// <remarks>
/// Ubicación: <c>%ProgramData%\Retail\appsettings.local.json</c>. La variable de entorno
/// <see cref="VariableCarpeta"/> (<c>RETAIL_CONFIG_DIR</c>) reemplaza la carpeta <c>%ProgramData%\Retail</c>: sirve para
/// las pruebas y para probar en desarrollo sin tocar la configuración real de la PC.
/// <para>
/// Precedencia, de menor a mayor: <c>appsettings.json</c> (y sus variantes por entorno) &lt; archivo local &lt;
/// variables de entorno &lt; línea de comandos. Una variable de entorno siempre gana.
/// </para>
/// </remarks>
public static class ConfiguracionLocal
{
    /// <summary>Variable de entorno que reemplaza la carpeta de la configuración local.</summary>
    public const string VariableCarpeta = "RETAIL_CONFIG_DIR";

    /// <summary>Nombre del archivo de configuración de la PC.</summary>
    public const string NombreArchivo = "appsettings.local.json";

    /// <summary>
    /// Carpeta de la configuración local: <c>RETAIL_CONFIG_DIR</c> si está definida; si no, <c>%ProgramData%\Retail</c>.
    /// </summary>
    public static string ObtenerCarpeta()
    {
        var personalizada = Environment.GetEnvironmentVariable(VariableCarpeta);
        if (!string.IsNullOrWhiteSpace(personalizada))
        {
            return personalizada;
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Retail");
    }

    /// <summary>
    /// Ruta completa del <c>appsettings.local.json</c> de esta PC.
    /// </summary>
    public static string ObtenerRutaArchivo()
    {
        return Path.Combine(ObtenerCarpeta(), NombreArchivo);
    }

    /// <summary>
    /// Agrega el archivo local (opcional) con la precedencia descripta: después de todos los archivos JSON ya cargados y
    /// antes de las variables de entorno y de la línea de comandos. Es la única implementación: la usan la aplicación y
    /// <c>Retail.Cli</c>.
    /// </summary>
    /// <remarks>
    /// El orden de las fuentes decide la precedencia (la última gana). Se inserta justo detrás de la última fuente JSON
    /// (<c>appsettings.json</c>, <c>appsettings.{entorno}.json</c>, secretos de usuario) y no al final, porque las
    /// variables de entorno y la línea de comandos ya fueron agregadas por el host y deben seguir ganando.
    /// </remarks>
    public static IConfigurationBuilder AgregarConfiguracionLocal(this IConfigurationBuilder builder)
    {
        return builder.AgregarConfiguracionLocal(ObtenerCarpeta());
    }

    /// <summary>
    /// Igual que <see cref="AgregarConfiguracionLocal(IConfigurationBuilder)"/> pero con la carpeta indicada (las pruebas la
    /// usan para no depender de variables de entorno del proceso).
    /// </summary>
    public static IConfigurationBuilder AgregarConfiguracionLocal(this IConfigurationBuilder builder, string carpeta)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(carpeta);

        var fuente = new JsonConfigurationSource
        {
            Path = Path.Combine(carpeta, NombreArchivo),
            Optional = true,
            ReloadOnChange = false
        };

        // Con una ruta absoluta, esto crea el proveedor de archivos sobre la carpeta (aunque todavía no exista).
        fuente.ResolveFileProvider();

        var ultimaJson = -1;
        for (var i = 0; i < builder.Sources.Count; i++)
        {
            if (builder.Sources[i] is JsonConfigurationSource)
            {
                ultimaJson = i;
            }
        }

        builder.Sources.Insert(ultimaJson + 1, fuente);
        return builder;
    }
}

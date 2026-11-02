using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Retail.Infrastructure.Logging;

namespace Retail.App.Services;

/// <summary>
/// Guarda las preferencias del usuario en un JSON del perfil de Windows (<c>%LOCALAPPDATA%\Retail POS\preferencias.json</c>).
/// </summary>
/// <remarks>
/// Lectura tolerante: un archivo inexistente o dañado no es un error de la aplicación, se vuelve al tema claro.
/// La escritura es atómica (archivo temporal + movimiento) y un fallo de disco se registra pero no interrumpe la UI:
/// perder una preferencia es menos grave que cerrar la pantalla de Ajustes con una excepción.
/// </remarks>
public sealed class PreferenciasUsuario : IPreferenciasUsuario
{
    private const string NombreDeArchivo = "preferencias.json";

    private static readonly JsonSerializerOptions s_opciones = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _ruta;
    private readonly ILogger _logger;

    /// <param name="ruta">Ruta completa del archivo JSON; se inyecta para poder probar con una carpeta temporal.</param>
    /// <param name="logger">Registro opcional de lecturas fallidas y errores de escritura.</param>
    public PreferenciasUsuario(string ruta, ILogger<PreferenciasUsuario>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(ruta))
        {
            throw new ArgumentException("La ruta del archivo de preferencias es obligatoria.", nameof(ruta));
        }

        _ruta = ruta;
        _logger = logger ?? NullLogger<PreferenciasUsuario>.Instance;
    }

    public TemaVisual Tema { get; set; } = TemaVisual.Claro;

    /// <summary>
    /// Ruta por defecto: <c>%LOCALAPPDATA%\Retail POS\preferencias.json</c>, en la misma carpeta que los registros y
    /// los avisos de restauración. Si la carpeta base está vacía se usa la temporal del sistema.
    /// </summary>
    public static string RutaPorDefecto()
    {
        var carpetaBase = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var raiz = string.IsNullOrWhiteSpace(carpetaBase) ? Path.GetTempPath() : carpetaBase;
        return Path.Combine(raiz, UbicacionDeLogs.CarpetaDeLaAplicacion, NombreDeArchivo);
    }

    public void Cargar()
    {
        Tema = TemaVisual.Claro;

        if (!File.Exists(_ruta))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_ruta);
            var datos = JsonSerializer.Deserialize<DatosPreferencias>(json, s_opciones);
            if (datos is not null && Enum.IsDefined(datos.Tema))
            {
                Tema = datos.Tema;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "No se pudieron leer las preferencias de {Ruta}; se usa el tema claro.", _ruta);
        }
    }

    public void Guardar()
    {
        try
        {
            var carpeta = Path.GetDirectoryName(_ruta);
            if (!string.IsNullOrEmpty(carpeta))
            {
                Directory.CreateDirectory(carpeta);
            }

            var temporal = _ruta + ".tmp";
            File.WriteAllText(temporal, JsonSerializer.Serialize(new DatosPreferencias { Tema = Tema }, s_opciones));
            File.Move(temporal, _ruta, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "No se pudieron guardar las preferencias en {Ruta}.", _ruta);
        }
    }

    /// <summary>Forma del JSON en disco.</summary>
    private sealed class DatosPreferencias
    {
        public TemaVisual Tema { get; set; } = TemaVisual.Claro;
    }
}

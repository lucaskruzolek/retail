using System.Text.Json;
using Retail.Application.Interfaces.Infrastructure;

namespace Retail.Infrastructure.Terminales;

/// <summary>
/// Guarda la identidad de esta PC como terminal en un archivo JSON local (con la forma de un objeto con la propiedad
/// <c>IdTerminal</c>), por defecto <c>%ProgramData%\Retail\terminal.json</c>. Vive fuera de la base porque la base la
/// comparten todas las terminales y no puede decir cuál de ellas es "esta" PC; y en una carpeta común del equipo (no
/// del perfil de usuario) para que cada cajero que inicie sesión en Windows vea la misma terminal.
/// </summary>
/// <remarks>
/// Se registra como Singleton: lee el archivo una sola vez, de forma perezosa, y mantiene el valor en memoria. Un archivo
/// ilegible o con un contenido inesperado equivale a "no activada" (el Gerente la activa de nuevo); en cambio, un
/// fallo al ESCRIBIR sí se propaga, porque la activación no puede darse por hecha si no quedó guardada.
/// </remarks>
public sealed class TerminalLocalArchivo : ITerminalLocal
{
    private readonly string _rutaArchivo;
    private readonly object _bloqueo = new();
    private bool _leido;
    private int? _idTerminal;

    public TerminalLocalArchivo(string? rutaArchivo = null)
    {
        _rutaArchivo = string.IsNullOrWhiteSpace(rutaArchivo) ? ObtenerRutaPorDefecto() : rutaArchivo;
    }

    public int? IdTerminal
    {
        get
        {
            lock (_bloqueo)
            {
                if (!_leido)
                {
                    _idTerminal = LeerArchivo();
                    _leido = true;
                }

                return _idTerminal;
            }
        }
    }

    public string NombreEquipo => Environment.MachineName;

    public async Task GuardarAsync(int idTerminal, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(idTerminal);

        var carpeta = Path.GetDirectoryName(_rutaArchivo);
        if (!string.IsNullOrEmpty(carpeta))
        {
            Directory.CreateDirectory(carpeta);
        }

        // Escritura atómica: se escribe un temporal en la misma carpeta y se lo mueve sobre el definitivo, así un corte
        // de luz no deja el archivo a medias.
        var temporal = _rutaArchivo + ".tmp";
        await using (var flujo = new FileStream(temporal, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(flujo, new ContenidoArchivo { IdTerminal = idTerminal }, cancellationToken: cancellationToken);
        }

        File.Move(temporal, _rutaArchivo, overwrite: true);

        lock (_bloqueo)
        {
            _idTerminal = idTerminal;
            _leido = true;
        }
    }

    private static string ObtenerRutaPorDefecto()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Retail", "terminal.json");
    }

    private int? LeerArchivo()
    {
        try
        {
            if (!File.Exists(_rutaArchivo))
            {
                return null;
            }

            var contenido = JsonSerializer.Deserialize<ContenidoArchivo>(File.ReadAllText(_rutaArchivo));

            return contenido is { IdTerminal: > 0 } ? contenido.IdTerminal : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Archivo corrupto o ilegible: se trata como "no activada".
            return null;
        }
    }

    private sealed class ContenidoArchivo
    {
        public int IdTerminal { get; set; }
    }
}

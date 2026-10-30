using System.IO;
using System.Text.Json;
using Retail.Infrastructure.Logging;

namespace Retail.App.Services;

/// <summary>
/// Guarda las confirmaciones del aviso de restauración (R-20) en un archivo JSON del perfil de Windows.
/// El contenido es un objeto que asocia cada operador con la última restauración que confirmó.
/// </summary>
/// <remarks>
/// Lectura tolerante: si el archivo no existe o está dañado se considera que no hay nada confirmado (el aviso se
/// muestra, que es el lado seguro). La escritura es atómica: se vuelca a un archivo temporal de la misma carpeta y
/// se lo mueve sobre el definitivo, de modo que un corte a mitad de camino no deja el JSON a medias. Los errores
/// de escritura se propagan al llamador, que decide cómo registrarlos.
/// </remarks>
public sealed class ConfirmacionesAvisoRestauracionEnArchivo : IConfirmacionesAvisoRestauracion
{
    private const string NombreDeArchivo = "avisos-restauracion.json";

    private readonly string _ruta;
    // Estático: serializa el leer-modificar-escribir aunque haya varias instancias sobre el mismo archivo.
    private static readonly SemaphoreSlim _candado = new(1, 1);

    /// <param name="ruta">Ruta completa del archivo JSON; se inyecta para poder probar con una carpeta temporal.</param>
    public ConfirmacionesAvisoRestauracionEnArchivo(string ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta))
        {
            throw new ArgumentException("La ruta del archivo de confirmaciones es obligatoria.", nameof(ruta));
        }

        _ruta = ruta;
    }

    /// <summary>
    /// Ruta por defecto: <c>%LOCALAPPDATA%\Retail POS\avisos-restauracion.json</c>. Si la carpeta base está vacía
    /// se usa la carpeta temporal del sistema, igual que los registros (<see cref="UbicacionDeLogs"/>).
    /// </summary>
    public static string RutaPorDefecto()
    {
        var carpetaBase = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var raiz = string.IsNullOrWhiteSpace(carpetaBase) ? Path.GetTempPath() : carpetaBase;
        return Path.Combine(raiz, UbicacionDeLogs.CarpetaDeLaAplicacion, NombreDeArchivo);
    }

    public async Task<bool> EstaConfirmadaAsync(int idUsuario, Guid referenciaRestauracion, CancellationToken cancellationToken = default)
    {
        await _candado.WaitAsync(cancellationToken);
        try
        {
            var confirmaciones = await LeerAsync(cancellationToken);
            return confirmaciones.TryGetValue(idUsuario, out var ultima) && ultima == referenciaRestauracion;
        }
        finally
        {
            _candado.Release();
        }
    }

    public async Task ConfirmarAsync(int idUsuario, Guid referenciaRestauracion, CancellationToken cancellationToken = default)
    {
        await _candado.WaitAsync(cancellationToken);
        try
        {
            var confirmaciones = await LeerAsync(cancellationToken);
            confirmaciones[idUsuario] = referenciaRestauracion;
            await EscribirAsync(confirmaciones, cancellationToken);
        }
        finally
        {
            _candado.Release();
        }
    }

    private async Task<Dictionary<int, Guid>> LeerAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_ruta))
        {
            return [];
        }

        try
        {
            await using var flujo = File.OpenRead(_ruta);
            var confirmaciones = await JsonSerializer.DeserializeAsync<Dictionary<int, Guid>>(flujo, cancellationToken: cancellationToken);
            return confirmaciones ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
        catch (FileNotFoundException)
        {
            return [];
        }
    }

    private async Task EscribirAsync(Dictionary<int, Guid> confirmaciones, CancellationToken cancellationToken)
    {
        var carpeta = Path.GetDirectoryName(_ruta);
        if (!string.IsNullOrEmpty(carpeta))
        {
            Directory.CreateDirectory(carpeta);
        }

        var temporal = _ruta + ".tmp";
        await using (var flujo = File.Create(temporal))
        {
            await JsonSerializer.SerializeAsync(flujo, confirmaciones, cancellationToken: cancellationToken);
        }

        File.Move(temporal, _ruta, overwrite: true);
    }
}

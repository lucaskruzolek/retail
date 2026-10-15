using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Retail.Application.DTOs.Backups;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Domain.Enums;

namespace Retail.Infrastructure.Backups;

/// <summary>
/// Manejo del directorio dedicado de respaldos (D-15): nombres de archivo, manifiestos y borrado.
/// </summary>
public class ArchivosBackup : IArchivosBackup
{
    private const long BytesPorMegabyte = 1024L * 1024L;

    private static readonly JsonSerializerOptions OpcionesJson = new() { WriteIndented = true };

    // UTF-8 sin BOM: el manifiesto lo pueden leer otras herramientas (un script, un editor) sin sorpresas.
    private static readonly UTF8Encoding Utf8SinBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly BackupOptions _opciones;

    public ArchivosBackup(IOptions<BackupOptions> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);
        _opciones = opciones.Value;
    }

    /// <summary>
    /// Directorio efectivo, con las variables de entorno expandidas.
    /// </summary>
    public string Directorio => Path.GetFullPath(Environment.ExpandEnvironmentVariables(_opciones.Directorio));

    public string PrepararRutaNueva(TipoBackupEnum tipo)
    {
        var directorio = Directorio;

        try
        {
            Directory.CreateDirectory(directorio);

            // Se prueba la escritura antes de pedirle la copia al motor: el error del motor ("Operating system
            // error 5") no le dice al operador qué carpeta revisar.
            var prueba = Path.Combine(directorio, $".prueba-escritura-{Guid.NewGuid():N}");
            File.WriteAllText(prueba, string.Empty);
            File.Delete(prueba);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new BackupFallidoException(
                $"No se puede escribir en el directorio de respaldos '{directorio}': verifique que exista y que la cuenta tenga permiso de escritura.", ex);
        }

        var disco = new DriveInfo(Path.GetPathRoot(directorio)!);
        var libreMb = disco.AvailableFreeSpace / (1024 * 1024);
        if (libreMb < _opciones.EspacioLibreMinimoMb)
        {
            throw new BackupFallidoException(
                $"Espacio insuficiente en {disco.Name}: hay {libreMb} MB libres y se requieren al menos " +
                $"{_opciones.EspacioLibreMinimoMb} MB para iniciar una copia.");
        }

        // Hora local en el nombre: es lo que lee una persona que busca la copia de "anoche".
        var nombreBase = $"Retail_{DateTime.Now:yyyyMMdd_HHmmss}_{tipo}";
        var ruta = Path.Combine(directorio, $"{nombreBase}.bak");
        for (var intento = 2; File.Exists(ruta); intento++)
        {
            ruta = Path.Combine(directorio, $"{nombreBase}_{intento}.bak");
        }

        return ruta;
    }

    public async Task EscribirManifiestoAsync(string rutaArchivo, ManifiestoBackupDto manifiesto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifiesto);

        var json = JsonSerializer.Serialize(manifiesto, OpcionesJson);
        await File.WriteAllTextAsync(RutaManifiesto(rutaArchivo), json, Utf8SinBom, cancellationToken);
    }

    public async Task<ManifiestoBackupDto> LeerManifiestoAsync(string rutaArchivo, CancellationToken cancellationToken = default)
    {
        var ruta = RutaCopiaBackup.Normalizar(rutaArchivo);

        if (!File.Exists(ruta))
        {
            throw new RestauracionFallidaException("No se encontró el archivo de la copia (.bak) en la ruta indicada.");
        }

        var rutaManifiesto = RutaManifiesto(ruta);
        if (!File.Exists(rutaManifiesto))
        {
            throw new RestauracionFallidaException(
                "La copia no tiene su manifiesto .json junto al archivo .bak: no se puede verificar su contenido.");
        }

        ManifiestoBackupDto? manifiesto;
        try
        {
            var json = await File.ReadAllTextAsync(rutaManifiesto, Encoding.UTF8, cancellationToken);
            manifiesto = JsonSerializer.Deserialize<ManifiestoBackupDto>(json, OpcionesJson);
        }
        catch (JsonException ex)
        {
            throw new RestauracionFallidaException("El manifiesto .json de la copia está dañado o no es válido.", ex);
        }

        if (manifiesto is null)
        {
            throw new RestauracionFallidaException("El manifiesto .json de la copia está vacío.");
        }

        ValidarCampos(manifiesto);

        // Un manifiesto copiado de otra copia describiría otro archivo: su hash nunca coincidiría con el de este .bak.
        if (!string.Equals(manifiesto.ArchivoBak, Path.GetFileName(ruta), StringComparison.OrdinalIgnoreCase))
        {
            throw new RestauracionFallidaException("El manifiesto .json pertenece a otra copia, no a este archivo .bak.");
        }

        return manifiesto;
    }

    /// <summary>
    /// <c>required</c> solo exige que la propiedad figure en el JSON: acepta <c>null</c>, así que se valida cada campo
    /// de forma explícita antes de que el resto de la restauración confíe en ellos.
    /// </summary>
    private static void ValidarCampos(ManifiestoBackupDto manifiesto)
    {
        if (manifiesto.Referencia == Guid.Empty)
        {
            throw new RestauracionFallidaException("El manifiesto .json de la copia no tiene una referencia válida.");
        }

        if (string.IsNullOrWhiteSpace(manifiesto.ArchivoBak))
        {
            throw new RestauracionFallidaException("El manifiesto .json de la copia no indica el nombre del archivo .bak.");
        }

        if (string.IsNullOrWhiteSpace(manifiesto.Tipo)
            || string.IsNullOrWhiteSpace(manifiesto.Operador)
            || string.IsNullOrWhiteSpace(manifiesto.UltimaMigracion)
            || string.IsNullOrWhiteSpace(manifiesto.VersionApp))
        {
            throw new RestauracionFallidaException("El manifiesto .json de la copia está incompleto: faltan datos obligatorios.");
        }

        if (manifiesto.TamanoBytes <= 0)
        {
            throw new RestauracionFallidaException("El manifiesto .json de la copia indica un tamaño no válido.");
        }

        if (!HashSha256Valido(manifiesto.HashSha256))
        {
            throw new RestauracionFallidaException("El manifiesto .json de la copia tiene una huella SHA-256 no válida.");
        }
    }

    private static bool HashSha256Valido(string? hash)
    {
        return hash is { Length: 64 } && hash.All(char.IsAsciiHexDigit);
    }

    public Task<ArchivoBackupDto> CalcularHuellaAsync(string rutaArchivo, CancellationToken cancellationToken = default)
    {
        return HuellaArchivo.CalcularAsync(RutaCopiaBackup.Normalizar(rutaArchivo), cancellationToken);
    }

    public async Task<string> PrepararParaRestaurarAsync(string rutaArchivo, CancellationToken cancellationToken = default)
    {
        var origen = RutaCopiaBackup.Normalizar(rutaArchivo);
        var directorio = Directorio;

        if (EstaEnElDirectorio(origen, directorio))
        {
            return origen;
        }

        try
        {
            if (!File.Exists(origen))
            {
                throw new RestauracionFallidaException("No se encontró el archivo de la copia (.bak) en la ruta indicada.");
            }

            // Se lee (y valida) el manifiesto de origen antes de copiar nada: sin él la copia no es restaurable.
            var manifiesto = await LeerManifiestoAsync(origen, cancellationToken);

            Directory.CreateDirectory(directorio);
            var huellaOrigen = await HuellaArchivo.CalcularAsync(origen, cancellationToken);
            var nombreBase = Path.GetFileNameWithoutExtension(origen);

            for (var intento = 1; ; intento++)
            {
                var nombre = intento == 1 ? nombreBase : $"{nombreBase}_{intento}";
                var destino = Path.Combine(directorio, $"{nombre}.bak");

                if (!File.Exists(destino) && !File.Exists(RutaManifiesto(destino)))
                {
                    ExigirEspacioParaCopiar(directorio, new FileInfo(origen).Length);
                    await CopiarAsync(origen, destino, manifiesto, cancellationToken);
                    return destino;
                }

                if (File.Exists(destino) && await TieneLaMismaHuellaAsync(destino, huellaOrigen, cancellationToken))
                {
                    // Ya se había copiado antes (por ejemplo, un intento anterior de restaurar el mismo archivo).
                    if (!File.Exists(RutaManifiesto(destino)))
                    {
                        await EscribirManifiestoAsync(destino, manifiesto with { ArchivoBak = Path.GetFileName(destino) }, cancellationToken);
                    }

                    return destino;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new RestauracionFallidaException(
                $"No se pudo copiar la copia al directorio de respaldos '{directorio}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Falla antes de empezar a copiar si el disco del directorio de respaldos no alcanza para el archivo más el margen
    /// mínimo (<see cref="BackupOptions.EspacioLibreMinimoMb"/>): un disco lleno a mitad de la copia dejaría una falla
    /// confusa y, además, sin espacio para que el motor restaure.
    /// </summary>
    private void ExigirEspacioParaCopiar(string directorio, long tamanoBytes)
    {
        var disco = new DriveInfo(Path.GetPathRoot(directorio)!);
        var necesarios = tamanoBytes + (_opciones.EspacioLibreMinimoMb * BytesPorMegabyte);

        if (disco.AvailableFreeSpace < necesarios)
        {
            throw new RestauracionFallidaException(
                $"Espacio insuficiente en {disco.Name} para traer la copia al directorio de respaldos: hay " +
                $"{disco.AvailableFreeSpace / BytesPorMegabyte} MB libres y se requieren {necesarios / BytesPorMegabyte} MB " +
                $"(el archivo más un margen de {_opciones.EspacioLibreMinimoMb} MB).");
        }
    }

    private static bool EstaEnElDirectorio(string ruta, string directorio)
    {
        var carpeta = Path.GetDirectoryName(ruta);
        return carpeta is not null && string.Equals(
            carpeta.TrimEnd(Path.DirectorySeparatorChar),
            directorio.TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<bool> TieneLaMismaHuellaAsync(string ruta, ArchivoBackupDto huellaOrigen, CancellationToken cancellationToken)
    {
        if (new FileInfo(ruta).Length != huellaOrigen.TamanoBytes)
        {
            return false;
        }

        var huella = await HuellaArchivo.CalcularAsync(ruta, cancellationToken);
        return string.Equals(huella.HashSha256, huellaOrigen.HashSha256, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Copia el <c>.bak</c> a un nombre temporal y recién entonces lo renombra: un corte a mitad de camino no deja un
    /// <c>.bak</c> truncado con un nombre que parezca una copia válida. El manifiesto copiado nombra al archivo nuevo.
    /// </summary>
    private async Task CopiarAsync(string origen, string destino, ManifiestoBackupDto manifiesto, CancellationToken cancellationToken)
    {
        var temporal = $"{destino}.copiando";

        // Solo se borra lo que esta operación creó. Antes de mover el temporal, "destino" puede ser de otro (por ejemplo,
        // otro proceso lo creó entre la comprobación de que no existía y el Move): no es nuestro y no se toca.
        var destinoCreado = false;

        try
        {
            await using (var entrada = new FileStream(origen, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
            await using (var salida = new FileStream(temporal, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await entrada.CopyToAsync(salida, cancellationToken);
            }

            File.Move(temporal, destino);
            destinoCreado = true;

            await EscribirManifiestoAsync(destino, manifiesto with { ArchivoBak = Path.GetFileName(destino) }, cancellationToken);
        }
        catch
        {
            // No se deja una copia a medias: el próximo intento empieza limpio. La limpieza no puede ocultar la causa
            // original: sus propios errores de E/S se ignoran y la excepción original sigue su camino.
            EliminarSinPropagar(temporal);
            if (destinoCreado)
            {
                EliminarSinPropagar(destino);
                EliminarSinPropagar(RutaManifiesto(destino));
            }

            throw;
        }
    }

    private static void EliminarSinPropagar(string ruta)
    {
        try
        {
            File.Delete(ruta);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Un resto que no se pudo borrar no es restaurable (no tiene su manifiesto o su huella no coincide).
        }
    }

    public void EliminarCopia(string rutaArchivo)
    {
        // File.Delete no falla si el archivo no existe: una copia que falló antes de crearse se "elimina" igual.
        File.Delete(rutaArchivo);
        File.Delete(RutaManifiesto(rutaArchivo));
    }

    public static string RutaManifiesto(string rutaArchivo) => Path.ChangeExtension(rutaArchivo, ".json");
}

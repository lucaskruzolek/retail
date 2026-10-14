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

    public void EliminarCopia(string rutaArchivo)
    {
        // File.Delete no falla si el archivo no existe: una copia que falló antes de crearse se "elimina" igual.
        File.Delete(rutaArchivo);
        File.Delete(RutaManifiesto(rutaArchivo));
    }

    public static string RutaManifiesto(string rutaArchivo) => Path.ChangeExtension(rutaArchivo, ".json");
}

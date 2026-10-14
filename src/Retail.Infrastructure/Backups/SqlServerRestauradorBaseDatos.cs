using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Backups;

/// <summary>
/// Restaura una copia en una base temporal, junto a la vigente (D-17, RF-22).
/// </summary>
/// <remarks>
/// Todas las sentencias de servidor (<c>RESTORE</c>, <c>DROP</c>, consultas a <c>sys.databases</c>) corren sobre una
/// conexión a <c>master</c>: la base vigente puede no existir o estar siendo reemplazada. Esta clase no decide reglas de
/// negocio (Ley 8): solo restaura, elimina y mide espacio; las transiciones de dominio las hace Application.
/// <para>
/// Los archivos físicos de la base temporal llevan un nombre único por restauración. <c>MODIFY NAME</c> renombra la base
/// pero no sus archivos, así que la temporal que se vuelve vigente conserva sus archivos; con nombres fijos la
/// siguiente restauración chocaría con ellos.
/// </para>
/// </remarks>
public class SqlServerRestauradorBaseDatos : IRestauradorBaseDatos
{
    private const long BytesPorMegabyte = 1024L * 1024L;

    private readonly RetailDbContext _context;
    private readonly BackupOptions _opciones;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    public SqlServerRestauradorBaseDatos(RetailDbContext context, IOptions<BackupOptions> opciones, ILoggerFactory loggerFactory)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _opciones = opciones?.Value ?? throw new ArgumentNullException(nameof(opciones));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<SqlServerRestauradorBaseDatos>();
    }

    public async Task<bool> ExisteBaseVigenteAsync(CancellationToken cancellationToken = default)
    {
        await using var conexion = await AbrirMasterAsync(cancellationToken);
        return await ExisteBaseAsync(conexion, ObtenerNombreVigente(), cancellationToken);
    }

    /// <remarks>
    /// Supuesto de estabilidad (D-26): la vigente puede tener un esquema más viejo (o dañado), así que no se usa el
    /// modelo de EF sino SQL mínimo contra <c>TURNOS_CAJA</c>. Desde la migración inicial, el turno abierto se guarda
    /// en la columna <c>estado</c> (nvarchar) con el texto <c>'Abierto'</c> y el borrado lógico en <c>deleted_at</c>.
    /// Se comprueba con <c>OBJECT_ID</c>/<c>COL_LENGTH</c> que existan antes de nombrarlos, para que un esquema distinto
    /// del esperado no haga fallar la consulta: se arma con las columnas que existan.
    /// </remarks>
    public async Task<bool> HayTurnoDeCajaAbiertoEnBaseVigenteAsync(CancellationToken cancellationToken = default)
    {
        var vigente = ObtenerNombreVigente();

        await using var conexion = await AbrirMasterAsync(cancellationToken);
        if (!await ExisteBaseAsync(conexion, vigente, cancellationToken))
        {
            return false;
        }

        // Nombre de tres partes desde master: no hace falta conectarse a la vigente.
        var tabla = $"{ComandosSqlServer.Delimitar(vigente)}.dbo.{ComandosSqlServer.Delimitar("TURNOS_CAJA")}";

        bool existeTabla;
        bool tieneEstado;
        bool tieneBajaLogica;
        await using (var inspeccion = conexion.CreateCommand())
        {
            inspeccion.CommandText =
                "SELECT CASE WHEN OBJECT_ID(@tabla, N'U') IS NULL THEN 0 ELSE 1 END, " +
                "CASE WHEN COL_LENGTH(@tabla, N'estado') IS NULL THEN 0 ELSE 1 END, " +
                "CASE WHEN COL_LENGTH(@tabla, N'deleted_at') IS NULL THEN 0 ELSE 1 END;";
            inspeccion.Parameters.AddWithValue("@tabla", tabla);

            await using var lector = await inspeccion.ExecuteReaderAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            existeTabla = lector.GetInt32(0) == 1;
            tieneEstado = lector.GetInt32(1) == 1;
            tieneBajaLogica = lector.GetInt32(2) == 1;
        }

        if (!existeTabla || !tieneEstado)
        {
            return false;
        }

        await using var consulta = conexion.CreateCommand();
        consulta.CommandText =
            $"SELECT CASE WHEN EXISTS (SELECT 1 FROM {tabla} WHERE estado = @abierto" +
            (tieneBajaLogica ? " AND deleted_at IS NULL" : string.Empty) +
            ") THEN 1 ELSE 0 END;";
        consulta.Parameters.AddWithValue("@abierto", "Abierto");

        return Convert.ToInt32(await consulta.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) == 1;
    }

    public async Task<IReadOnlyList<string>> ObtenerConexionesAjenasABaseVigenteAsync(CancellationToken cancellationToken = default)
    {
        await using var conexion = await AbrirMasterAsync(cancellationToken);

        // Las conexiones de este mismo proceso (incluidas las del pool) se excluyen: el intercambio las corta de todos
        // modos y no son "otra terminal". Si la base no existe, DB_ID es NULL y no hay sesiones que coincidan.
        await using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT DISTINCT host_name, program_name FROM sys.dm_exec_sessions " +
            "WHERE database_id = DB_ID(@vigente) AND session_id <> @@SPID AND is_user_process = 1 " +
            "AND NOT (ISNULL(host_process_id, -1) = @pid AND ISNULL(host_name, N'') = @equipo);";
        comando.Parameters.AddWithValue("@vigente", ObtenerNombreVigente());
        comando.Parameters.AddWithValue("@pid", Environment.ProcessId);

        // Un proceso de otra PC podría tener el mismo número de proceso: se compara también el nombre del equipo.
        comando.Parameters.AddWithValue("@equipo", Environment.MachineName);

        var descripciones = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            var equipo = lector.IsDBNull(0) ? "equipo desconocido" : lector.GetString(0);
            var programa = lector.IsDBNull(1) ? "programa desconocido" : lector.GetString(1);
            descripciones.Add($"{equipo} ({programa})");
        }

        return descripciones.ToList();
    }

    public async Task<IBaseRestaurada> RestaurarEnBaseTemporalAsync(
        string rutaArchivo,
        IProgress<int>? progreso,
        CancellationToken cancellationToken = default)
    {
        var ruta = RutaCopiaBackup.Normalizar(rutaArchivo);
        var cadenaMaster = ObtenerCadenaMaster();
        var vigente = ObtenerNombreVigente();
        var temporal = $"{vigente}_Restaurando";
        var ahora = DateTime.Now;
        var marca = ahora.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

        bool existeVigente;
        List<ArchivoDeCopia> archivos;
        string carpetaDatos;
        string carpetaLog;
        long tamanoVigente;

        await using (var conexion = await AbrirMasterAsync(cancellationToken))
        {
            existeVigente = await ExisteBaseAsync(conexion, vigente, cancellationToken);

            // Una temporal que quedó de un corte anterior: la restauración corre bajo el bloqueo exclusivo, así que
            // nadie más puede estar usándola.
            if (await ExisteBaseAsync(conexion, temporal, cancellationToken))
            {
                _logger.LogWarning("Quedó una base temporal {Base} de una restauración interrumpida: se descarta.", temporal);
                await ComandosSqlServer.EliminarBaseAsync(cadenaMaster, temporal, _logger, cancellationToken);
            }

            archivos = await LeerArchivosDeLaCopiaAsync(conexion, ruta, cancellationToken);
            (carpetaDatos, carpetaLog) = await ObtenerCarpetasDeDestinoAsync(conexion, cancellationToken);
            tamanoVigente = existeVigente ? await ObtenerTamanoAsignadoAsync(conexion, vigente, cancellationToken) : 0;
        }

        AsignarRutasFisicas(archivos, vigente, marca, carpetaDatos, carpetaLog);
        VerificarEspacio(archivos, carpetaDatos, carpetaLog, tamanoVigente);

        try
        {
            await RestaurarAsync(cadenaMaster, temporal, ruta, archivos, progreso, cancellationToken);

            // Una copia chica puede terminar antes de que el motor atienda la cancelación: si el operador ya la pidió,
            // se respeta (el catch descarta la temporal) en lugar de seguir con una restauración que quiso detener.
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (SqlException) when (cancellationToken.IsCancellationRequested)
        {
            // El driver informa la cancelación del comando como SqlException ("Operation cancelled by user").
            await DescartarTemporalAsync(cadenaMaster, temporal);
            throw new OperationCanceledException(cancellationToken);
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "El motor no pudo restaurar {Ruta} en {Base}.", ruta, temporal);
            await DescartarTemporalAsync(cadenaMaster, temporal);
            throw new RestauracionFallidaException(
                "El motor no pudo restaurar la copia: el archivo está dañado o no es una copia de esta base.", ex);
        }
        catch
        {
            // OperationCanceledException o cualquier otra falla: la temporal a medio restaurar no debe quedar.
            await DescartarTemporalAsync(cadenaMaster, temporal);
            throw;
        }

        var anterior = existeVigente ? $"{vigente}_Anterior_{marca}" : null;
        return new BaseRestauradaSqlServer(cadenaMaster, vigente, temporal, anterior, _loggerFactory.CreateLogger<BaseRestauradaSqlServer>());
    }

    public async Task<IReadOnlyList<string>> EliminarBasesAnterioresAsync(string? baseAConservar, CancellationToken cancellationToken = default)
    {
        // Sin una base a conservar no se elimina nada: "conservar ninguna" borraría también la anterior recién creada
        // por un intercambio, la única copia de la base que se acaba de reemplazar. El servicio ya evita llamar así;
        // esto es defensa en profundidad.
        if (baseAConservar is null)
        {
            return [];
        }

        var cadenaMaster = ObtenerCadenaMaster();
        var vigente = ObtenerNombreVigente();
        var candidatas = new List<string>();

        await using (var conexion = await AbrirMasterAsync(cancellationToken))
        {
            await using var consulta = conexion.CreateCommand();
            consulta.CommandText = @"SELECT name FROM sys.databases WHERE name LIKE @patron ESCAPE '\';";
            consulta.Parameters.AddWithValue("@patron", EscaparParaLike(vigente) + @"\_Anterior\_%");

            await using var lector = await consulta.ExecuteReaderAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                candidatas.Add(lector.GetString(0));
            }
        }

        var noEliminadas = new List<string>();
        foreach (var nombre in candidatas.Where(n => !string.Equals(n, baseAConservar, StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                await ComandosSqlServer.EliminarBaseAsync(cadenaMaster, nombre, _logger, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "No se pudo eliminar la base anterior {Base}.", nombre);
                noEliminadas.Add(nombre);
            }
        }

        return noEliminadas;
    }

    // ───────────────────── Restauración ─────────────────────

    private static async Task RestaurarAsync(
        string cadenaMaster,
        string temporal,
        string ruta,
        List<ArchivoDeCopia> archivos,
        IProgress<int>? progreso,
        CancellationToken cancellationToken)
    {
        await using var conexion = new SqlConnection(cadenaMaster);
        ComandosSqlServer.SuscribirProgreso(conexion, progreso);
        await conexion.OpenAsync(cancellationToken);

        await using var comando = conexion.CreateCommand();

        // MOVE admite variables: los nombres lógicos y físicos viajan como parámetros; solo el nombre de la base
        // (de la configuración, no del operador) se delimita en el texto.
        var movimientos = new List<string>();
        for (var i = 0; i < archivos.Count; i++)
        {
            movimientos.Add($"MOVE @logico{i} TO @fisico{i}");
            comando.Parameters.AddWithValue($"@logico{i}", archivos[i].NombreLogico);
            comando.Parameters.AddWithValue($"@fisico{i}", archivos[i].RutaFisica);
        }

        comando.CommandText =
            $"RESTORE DATABASE {ComandosSqlServer.Delimitar(temporal)} FROM DISK = @ruta " +
            $"WITH {string.Join(", ", movimientos)}, CHECKSUM, RECOVERY, STATS = 10;";
        comando.Parameters.AddWithValue("@ruta", ruta);

        // Sin límite de tiempo: una base grande puede tardar minutos. Se cancela con el token.
        comando.CommandTimeout = 0;
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<List<ArchivoDeCopia>> LeerArchivosDeLaCopiaAsync(SqlConnection conexion, string ruta, CancellationToken cancellationToken)
    {
        var archivos = new List<ArchivoDeCopia>();

        try
        {
            await using var comando = conexion.CreateCommand();
            comando.CommandText = "RESTORE FILELISTONLY FROM DISK = @ruta;";
            comando.Parameters.AddWithValue("@ruta", ruta);
            comando.CommandTimeout = 0;

            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
            var colNombre = lector.GetOrdinal("LogicalName");
            var colTipo = lector.GetOrdinal("Type");
            var colTamano = lector.GetOrdinal("Size");

            while (await lector.ReadAsync(cancellationToken))
            {
                // Size es bigint o numeric(20,0) según la versión del motor.
                // 'D' datos, 'L' registro de transacciones; cualquier otro (texto completo, etc.) va con los datos.
                var esLog = string.Equals(lector.GetString(colTipo), "L", StringComparison.OrdinalIgnoreCase);
                archivos.Add(new ArchivoDeCopia(lector.GetString(colNombre), esLog, Convert.ToInt64(lector.GetValue(colTamano), CultureInfo.InvariantCulture)));
            }
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "No se pudo leer la lista de archivos de {Ruta}.", ruta);
            throw new RestauracionFallidaException(
                "No se pudo leer la copia: el archivo no existe, no es una copia de SQL Server o el servicio de SQL Server no tiene acceso a esa carpeta.",
                ex);
        }

        if (archivos.Count == 0)
        {
            throw new RestauracionFallidaException("La copia no contiene archivos de base de datos.");
        }

        return archivos;
    }

    private static async Task<(string Datos, string Log)> ObtenerCarpetasDeDestinoAsync(SqlConnection conexion, CancellationToken cancellationToken)
    {
        await using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(512)), " +
            "CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(512)), " +
            "(SELECT TOP (1) physical_name FROM sys.master_files WHERE database_id = 1 AND file_id = 1);";

        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        await lector.ReadAsync(cancellationToken);

        var datos = lector.IsDBNull(0) ? null : lector.GetString(0);
        var log = lector.IsDBNull(1) ? null : lector.GetString(1);

        // Si el motor no informa las carpetas por defecto, se usa la de los archivos de master.
        var respaldo = lector.IsDBNull(2) ? null : Path.GetDirectoryName(lector.GetString(2));
        var carpetaDatos = datos ?? respaldo
            ?? throw new RestauracionFallidaException("No se pudo determinar la carpeta de datos del servidor SQL.");

        return (carpetaDatos, log ?? carpetaDatos);
    }

    private static async Task<long> ObtenerTamanoAsignadoAsync(SqlConnection conexion, string baseDeDatos, CancellationToken cancellationToken)
    {
        await using var comando = conexion.CreateCommand();

        // size está en páginas de 8 KB.
        comando.CommandText = "SELECT SUM(CAST(size AS bigint)) * 8192 FROM sys.master_files WHERE database_id = DB_ID(@nombre);";
        comando.Parameters.AddWithValue("@nombre", baseDeDatos);

        var resultado = await comando.ExecuteScalarAsync(cancellationToken);
        return resultado is null or DBNull ? 0 : Convert.ToInt64(resultado, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Nombres físicos únicos por restauración: <c>{vigente}_{marca}_{n}{ext}</c>. Si el archivo ya existe se sube el
    /// sufijo, porque <c>RESTORE</c> no sobrescribe archivos.
    /// </summary>
    private static void AsignarRutasFisicas(List<ArchivoDeCopia> archivos, string vigente, string marca, string carpetaDatos, string carpetaLog)
    {
        var datosAsignados = 0;

        for (var i = 0; i < archivos.Count; i++)
        {
            var archivo = archivos[i];
            string extension;
            string carpeta;

            if (archivo.EsLog)
            {
                extension = ".ldf";
                carpeta = carpetaLog;
            }
            else
            {
                extension = datosAsignados == 0 ? ".mdf" : ".ndf";
                carpeta = carpetaDatos;
                datosAsignados++;
            }

            var sufijo = i;
            string rutaFisica;
            do
            {
                rutaFisica = Path.Combine(carpeta, $"{vigente}_{marca}_{sufijo}{extension}");
                sufijo += archivos.Count;
            }
            while (File.Exists(rutaFisica));

            archivo.RutaFisica = rutaFisica;
        }
    }

    // ───────────────────── Espacio en disco (D-23) ─────────────────────

    /// <summary>
    /// Falla antes de restaurar si algún disco no alcanza para los archivos de la copia más el margen configurado y, si
    /// hay base vigente, para la copia previa a restaurar (se estima con lo que la vigente tiene asignado).
    /// </summary>
    private void VerificarEspacio(List<ArchivoDeCopia> archivos, string carpetaDatos, string carpetaLog, long tamanoVigente)
    {
        var margen = Multiplicar(Math.Max(_opciones.EspacioLibreMinimoMb, 0), BytesPorMegabyte);
        var requeridoPorDisco = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        foreach (var archivo in archivos)
        {
            var carpeta = archivo.EsLog ? carpetaLog : carpetaDatos;
            Sumar(requeridoPorDisco, ObtenerRaiz(carpeta), archivo.TamanoBytes);
        }

        if (tamanoVigente > 0)
        {
            var directorioCopias = Environment.ExpandEnvironmentVariables(_opciones.Directorio);
            Sumar(requeridoPorDisco, ObtenerRaiz(directorioCopias), tamanoVigente);
        }

        foreach (var (raiz, bytes) in requeridoPorDisco)
        {
            var requerido = Sumar(bytes, margen);
            var disponible = ObtenerEspacioLibre(raiz);

            // Si el disco no se puede medir (por ejemplo, un servidor remoto), el motor es quien dará el error.
            if (disponible is not null && disponible < requerido)
            {
                throw new RestauracionFallidaException(
                    $"No hay espacio suficiente en el disco {raiz} para restaurar la copia: " +
                    $"disponible {disponible.Value / BytesPorMegabyte} MB, necesario {requerido / BytesPorMegabyte} MB.");
            }
        }
    }

    private static string ObtenerRaiz(string ruta)
    {
        return Path.GetPathRoot(Path.GetFullPath(ruta)) ?? ruta;
    }

    private static long? ObtenerEspacioLibre(string raiz)
    {
        try
        {
            return new DriveInfo(raiz).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void Sumar(Dictionary<string, long> acumulado, string clave, long bytes)
    {
        acumulado[clave] = Sumar(acumulado.GetValueOrDefault(clave), bytes);
    }

    private static long Sumar(long a, long b)
    {
        return a > long.MaxValue - b ? long.MaxValue : a + b;
    }

    private static long Multiplicar(long a, long b)
    {
        return a > long.MaxValue / b ? long.MaxValue : a * b;
    }

    // ───────────────────── Utilidades ─────────────────────

    private async Task DescartarTemporalAsync(string cadenaMaster, string temporal)
    {
        try
        {
            // Aunque la operación se haya cancelado, la limpieza no debe cancelarse.
            await ComandosSqlServer.EliminarBaseAsync(cadenaMaster, temporal, _logger, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo eliminar la base temporal {Base} tras una restauración fallida.", temporal);
        }
    }

    private static string EscaparParaLike(string texto)
    {
        return texto
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("[", @"\[", StringComparison.Ordinal);
    }

    private static async Task<bool> ExisteBaseAsync(SqlConnection conexion, string nombre, CancellationToken cancellationToken)
    {
        await using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT DB_ID(@nombre);";
        comando.Parameters.AddWithValue("@nombre", nombre);

        var resultado = await comando.ExecuteScalarAsync(cancellationToken);
        return resultado is not null and not DBNull;
    }

    private async Task<SqlConnection> AbrirMasterAsync(CancellationToken cancellationToken)
    {
        var conexion = new SqlConnection(ObtenerCadenaMaster());
        try
        {
            await conexion.OpenAsync(cancellationToken);
            return conexion;
        }
        catch
        {
            await conexion.DisposeAsync();
            throw;
        }
    }

    private string ObtenerCadenaMaster()
    {
        var cadena = _context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("No hay una cadena de conexión configurada para la base.");
        return ComandosSqlServer.CadenaParaBase(cadena, "master");
    }

    private string ObtenerNombreVigente()
    {
        var cadena = _context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("No hay una cadena de conexión configurada para la base.");
        var nombre = new SqlConnectionStringBuilder(cadena).InitialCatalog;

        return string.IsNullOrWhiteSpace(nombre)
            ? throw new InvalidOperationException("La cadena de conexión no indica la base de datos (Initial Catalog).")
            : nombre;
    }

    private sealed class ArchivoDeCopia
    {
        public ArchivoDeCopia(string nombreLogico, bool esLog, long tamanoBytes)
        {
            NombreLogico = nombreLogico;
            EsLog = esLog;
            TamanoBytes = tamanoBytes;
        }

        public string NombreLogico { get; }

        public bool EsLog { get; }

        public long TamanoBytes { get; }

        public string RutaFisica { get; set; } = string.Empty;
    }
}

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using Retail.Application.DTOs.Sistema;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Services;

namespace Retail.Infrastructure.Backups;

/// <summary>
/// La base temporal donde se restauró una copia (D-17). Tiene su propio <see cref="RetailDbContext"/>, apuntado a la
/// temporal, para validar y preparar la copia con el modelo de la aplicación sin tocar la base vigente.
/// </summary>
/// <remarks>
/// Si se libera sin promoverla, elimina la base temporal. Al promoverla, la vigente pasa a llamarse
/// <c>NombreBaseAnterior</c> y la temporal ocupa su nombre; el motor corta las conexiones a la vigente, así que la
/// aplicación debe reiniciarse.
/// </remarks>
public sealed class BaseRestauradaSqlServer : IBaseRestaurada
{
    private readonly string _cadenaMaster;
    private readonly string _vigente;
    private const int IntentosDeRenombrado = 3;

    private static readonly TimeSpan PausaEntreIntentos = TimeSpan.FromMilliseconds(500);

    private readonly string _temporal;
    private readonly ILogger _logger;
    private readonly RetailDbContext _context;
    private bool _promovida;
    private bool _liberada;

    public BaseRestauradaSqlServer(string cadenaMaster, string vigente, string temporal, string? nombreBaseAnterior, ILogger logger)
    {
        _cadenaMaster = cadenaMaster ?? throw new ArgumentNullException(nameof(cadenaMaster));
        _vigente = vigente ?? throw new ArgumentNullException(nameof(vigente));
        _temporal = temporal ?? throw new ArgumentNullException(nameof(temporal));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        NombreBaseAnterior = nombreBaseAnterior;

        _context = new RetailDbContext(
            new DbContextOptionsBuilder<RetailDbContext>()
                .UseSqlServer(ComandosSqlServer.CadenaParaBase(cadenaMaster, temporal))
                .Options);
    }

    public string? NombreBaseAnterior { get; }

    public Task<EstadoEsquemaDto> ObtenerEstadoEsquemaAsync(CancellationToken cancellationToken = default)
    {
        return new EsquemaBaseDatos(_context).ObtenerEstadoAsync(cancellationToken);
    }

    public Task AplicarMigracionesPendientesAsync(CancellationToken cancellationToken = default)
    {
        return _context.Database.MigrateAsync(cancellationToken);
    }

    public Task<Usuario?> BuscarUsuarioActivoAsync(string nombreUsuario, CancellationToken cancellationToken = default)
    {
        // El filtro global de borrado lógico ya excluye a los usuarios dados de baja.
        return _context.Usuarios.FirstOrDefaultAsync(u => u.NombreUsuario == nombreUsuario, cancellationToken);
    }

    public Task<bool> ExisteGerenteActivoAsync(CancellationToken cancellationToken = default)
    {
        return _context.Usuarios.AnyAsync(u => u.IdRol == (int)RolUsuarioEnum.Gerente, cancellationToken);
    }

    public async Task<bool> TieneUsuarioDeLaTareaProgramadaAsync(CancellationToken cancellationToken = default)
    {
        // Consulta de catálogo sobre la propia temporal; la cuenta viaja como parámetro.
        var cuenta = new SqlParameter("@cuenta", ComandosSqlServer.CuentaDeLaTareaProgramada);

        var cantidad = await _context.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.database_principals WHERE name = @cuenta", cuenta)
            .SingleAsync(cancellationToken);

        return cantidad > 0;
    }

    /// <summary>
    /// Copia el historial de la vigente a la temporal: primero borra de la temporal los registros cuya
    /// <c>Referencia</c> también está en la vigente (la vigente es la versión más reciente de ese hecho, R-18) y luego
    /// inserta todos los de la vigente, incluidos los dados de baja. Después pasa la auditoría de accesos (AUDITORIA_ACCESOS)
    /// posterior a la copia. Las tres tablas se tocan en una misma transacción.
    /// </summary>
    public async Task ImportarHistorialDeLaBaseVigenteAsync(CancellationToken cancellationToken = default)
    {
        await using var transaccion = await _context.Database.BeginTransactionAsync(cancellationToken);

        await ImportarTablaAsync<RegistroBackup>(cancellationToken);
        await ImportarTablaAsync<RegistroRestauracion>(cancellationToken);

        await ImportarAuditoriaDeAccesosAsync(cancellationToken);

        await transaccion.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RegistroBackup>> ObtenerCopiasEnCursoAsync(CancellationToken cancellationToken = default)
    {
        return await _context.RegistrosBackup
            .Where(r => r.Estado == EstadoBackupEnum.EnCurso)
            .ToListAsync(cancellationToken);
    }

    public async Task AgregarRestauracionAsync(RegistroRestauracion registro, CancellationToken cancellationToken = default)
    {
        await _context.RegistrosRestauracion.AddAsync(registro, cancellationToken);
    }

    public Task GuardarCambiosAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> PromoverAVigenteAsync(CancellationToken cancellationToken = default)
    {
        if (_promovida || _liberada)
        {
            throw new InvalidOperationException("La base restaurada ya fue promovida o liberada.");
        }

        // La temporal no debe tener conexiones propias abiertas durante el renombrado.
        await _context.DisposeAsync();
        ComandosSqlServer.LimpiarPool(_cadenaMaster, _temporal);

        try
        {
            return await IntercambiarAsync(cancellationToken);
        }
        finally
        {
            // Las conexiones que el intercambio cortó (ROLLBACK IMMEDIATE) quedan inservibles en el pool, salga bien o
            // mal: se descartan siempre (B-6). Si salió bien, la aplicación además debe reiniciarse.
            SqlConnection.ClearAllPools();
        }
    }

    /// <summary>
    /// Devuelve las advertencias del intercambio: lo que falló después de que las bases ya cambiaron de nombre y, por eso,
    /// no se deshace pero el operador debe conocer.
    /// </summary>
    private async Task<IReadOnlyList<string>> IntercambiarAsync(CancellationToken cancellationToken)
    {
        var advertencias = new List<string>();

        await using var conexion = new SqlConnection(_cadenaMaster);
        await conexion.OpenAsync(cancellationToken);

        var vigente = ComandosSqlServer.Delimitar(_vigente);
        var temporal = ComandosSqlServer.Delimitar(_temporal);

        // Cada sentencia va por separado: ante una falla hay que saber exactamente en qué estado quedó cada base.
        if (NombreBaseAnterior is not null)
        {
            var anterior = ComandosSqlServer.Delimitar(NombreBaseAnterior);

            // Paso 1: la vigente se aparta. SINGLE_USER con ROLLBACK IMMEDIATE corta a cualquier conexión que la use.
            try
            {
                await RenombrarConReintentosAsync(conexion, vigente, anterior, cancellationToken);
            }
            catch (SqlException ex)
            {
                // No se renombró: la vigente sigue con su nombre, pero hay que devolverle el acceso multiusuario.
                await IntentarAsync(conexion, $"ALTER DATABASE {vigente} SET MULTI_USER;");
                throw new RestauracionFallidaException(
                    "No se pudo apartar la base vigente para el intercambio: quedó sin cambios.", ex);
            }

            if (!await IntentarAsync(conexion, $"ALTER DATABASE {anterior} SET MULTI_USER;"))
            {
                // Es la base de respaldo del operador: si queda en SINGLE_USER, abrirla después le fallará sin explicación.
                advertencias.Add(
                    $"La base anterior '{NombreBaseAnterior}' quedó en modo SINGLE_USER: antes de consultarla ejecute " +
                    $"ALTER DATABASE [{NombreBaseAnterior}] SET MULTI_USER (ver el log).");
            }
        }

        // Paso 2: la temporal ocupa el nombre de la vigente.
        try
        {
            await RenombrarConReintentosAsync(conexion, temporal, vigente, cancellationToken);
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "No se pudo renombrar {Temporal} como {Vigente}.", _temporal, _vigente);
            var revertida = await RevertirPrimerPasoAsync(conexion);
            throw new RestauracionFallidaException(
                revertida
                    ? "No se pudo completar el intercambio de bases: la base vigente se conservó sin cambios."
                    : $"No se pudo completar el intercambio de bases ni devolver la base vigente a su nombre: quedó como " +
                      $"'{NombreBaseAnterior}' y requiere intervención técnica (ver el log).",
                ex);
        }

        // El intercambio ya ocurrió: si el acceso multiusuario no se puede restablecer, se registra pero no se deshace.
        _promovida = true;
        if (!await IntentarAsync(conexion, $"ALTER DATABASE {vigente} SET MULTI_USER;"))
        {
            if (_logger.IsEnabled(LogLevel.Critical))
            {
                _logger.LogCritical("La base {Vigente} quedó restaurada pero en modo SINGLE_USER: ejecute ALTER DATABASE ... SET MULTI_USER.", _vigente);
            }

            // El log solo lo lee quien lo busca: el operador debe enterarse en el resultado, porque la aplicación no
            // podrá abrir la base restaurada hasta que se la devuelva a modo multiusuario.
            advertencias.Add(
                $"La base '{_vigente}' quedó restaurada pero en modo SINGLE_USER: ejecute ALTER DATABASE [{_vigente}] " +
                "SET MULTI_USER antes de abrir la aplicación (ver el log).");
        }

        return advertencias;
    }

    public async ValueTask DisposeAsync()
    {
        if (_liberada)
        {
            return;
        }

        _liberada = true;

        try
        {
            await _context.DisposeAsync();

            if (!_promovida)
            {
                await ComandosSqlServer.EliminarBaseAsync(_cadenaMaster, _temporal, _logger, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            // Liberar nunca lanza: queda la base temporal, que la próxima restauración descarta.
            _logger.LogError(ex, "No se pudo eliminar la base temporal {Base}.", _temporal);
        }
    }

    /// <summary>
    /// La auditoría de accesos no está en el modelo de EF (la crea una migración con SQL), así que se importa con SQL propio.
    /// Solo pasan las filas posteriores a la última que la copia ya tenía: las de la copia son anteriores a su fecha, y
    /// así un segundo restore no las duplica. <c>id_usuario</c> queda NULL porque en la base restaurada el mismo Id puede
    /// ser otra persona (mismo criterio que REGISTROS_RESTAURACION): el nombre queda como foto del momento del hecho.
    /// </summary>
    private async Task ImportarAuditoriaDeAccesosAsync(CancellationToken cancellationToken)
    {
        var origen = ComandosSqlServer.Delimitar(_vigente);

        // El identificador de la vigente sale de la configuración y está delimitado; ningún valor del operador entra en el SQL.
#pragma warning disable EF1002
        await _context.Database.ExecuteSqlRawAsync(
            "INSERT INTO [dbo].[AUDITORIA_ACCESOS] (fecha_hora, nombre_usuario, id_usuario, resultado, nombre_equipo, tipo_evento, detalle) " +
            "SELECT v.fecha_hora, v.nombre_usuario, NULL, v.resultado, v.nombre_equipo, v.tipo_evento, v.detalle " +
            $"FROM {origen}.[dbo].[AUDITORIA_ACCESOS] v " +
            "WHERE v.fecha_hora > ISNULL((SELECT MAX(fecha_hora) FROM [dbo].[AUDITORIA_ACCESOS]), '0001-01-01');",
            cancellationToken);
#pragma warning restore EF1002
    }

    private async Task ImportarTablaAsync<TEntidad>(CancellationToken cancellationToken)
        where TEntidad : class
    {
        var tipo = _context.Model.FindEntityType(typeof(TEntidad))
            ?? throw new InvalidOperationException($"{typeof(TEntidad).Name} no pertenece al modelo.");
        var nombreTabla = tipo.GetTableName()
            ?? throw new InvalidOperationException($"{typeof(TEntidad).Name} no está mapeada a una tabla.");
        var esquema = tipo.GetSchema() ?? "dbo";
        var tabla = StoreObjectIdentifier.Table(nombreTabla, tipo.GetSchema());

        // Se omite la clave (identidad): cada base numera la suya. La identidad estable es la Referencia.
        var columnas = tipo.GetProperties()
            .Where(p => !p.IsPrimaryKey())
            .Select(p => ComandosSqlServer.Delimitar(p.GetColumnName(tabla)
                ?? throw new InvalidOperationException($"{p.Name} no está mapeada a una columna.")))
            .ToList();
        var columnaReferencia = ComandosSqlServer.Delimitar(
            tipo.FindProperty("Referencia")?.GetColumnName(tabla)
            ?? throw new InvalidOperationException($"{typeof(TEntidad).Name} no tiene la propiedad Referencia."));

        var destino = $"{ComandosSqlServer.Delimitar(esquema)}.{ComandosSqlServer.Delimitar(nombreTabla)}";
        var origen = $"{ComandosSqlServer.Delimitar(_vigente)}.{destino}";
        var lista = string.Join(", ", columnas);

        // Los identificadores salen del modelo de EF y de la configuración, no del operador, y están delimitados.
#pragma warning disable EF1002
        await _context.Database.ExecuteSqlRawAsync(
            $"DELETE FROM {destino} WHERE {columnaReferencia} IN (SELECT {columnaReferencia} FROM {origen}); " +
            $"INSERT INTO {destino} ({lista}) SELECT {lista} FROM {origen};",
            cancellationToken);
#pragma warning restore EF1002
    }

    /// <summary>
    /// Devuelve la base anterior a su nombre original. Indica si lo logró (sin base anterior no hay nada que revertir).
    /// </summary>
    private async Task<bool> RevertirPrimerPasoAsync(SqlConnection conexion)
    {
        if (NombreBaseAnterior is null)
        {
            return true;
        }

        try
        {
            var vigente = ComandosSqlServer.Delimitar(_vigente);
            var anterior = ComandosSqlServer.Delimitar(NombreBaseAnterior);
            await RenombrarConReintentosAsync(conexion, anterior, vigente, CancellationToken.None);
            await IntentarAsync(conexion, $"ALTER DATABASE {vigente} SET MULTI_USER;");
            return true;
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Critical))
            {
                _logger.LogCritical(
                    ex,
                    "No se pudo devolver la base {Anterior} a su nombre {Vigente}: la base vigente quedó con otro nombre y requiere intervención manual.",
                    NombreBaseAnterior,
                    _vigente);
            }

            return false;
        }
    }

    /// <summary>
    /// Aparta una base (SINGLE_USER con ROLLBACK IMMEDIATE) y la renombra. Entre las dos sentencias, otra conexión (otra
    /// terminal, un pool que reconecta) puede ocupar la única plaza de la base y hacer fallar el renombrado: se reintenta
    /// el par completo unas pocas veces antes de darlo por fallido (M-1).
    /// </summary>
    private static async Task RenombrarConReintentosAsync(
        SqlConnection conexion,
        string origenDelimitado,
        string destinoDelimitado,
        CancellationToken cancellationToken)
    {
        for (var intento = 1; ; intento++)
        {
            try
            {
                await EjecutarAsync(conexion, $"ALTER DATABASE {origenDelimitado} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;", cancellationToken);
                await EjecutarAsync(conexion, $"ALTER DATABASE {origenDelimitado} MODIFY NAME = {destinoDelimitado};", cancellationToken);
                return;
            }
            catch (SqlException) when (intento < IntentosDeRenombrado)
            {
                await Task.Delay(PausaEntreIntentos, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Ejecuta una sentencia de recuperación sin propagar el error: se usa después de una falla, cuando lo importante
    /// es la excepción original. Indica si se pudo ejecutar.
    /// </summary>
    private async Task<bool> IntentarAsync(SqlConnection conexion, string sentencia)
    {
        try
        {
            await EjecutarAsync(conexion, sentencia, CancellationToken.None);
            return true;
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "No se pudo ejecutar la sentencia de recuperación: {Sentencia}", sentencia);
            return false;
        }
    }

    private static async Task EjecutarAsync(SqlConnection conexion, string sentencia, CancellationToken cancellationToken)
    {
        await using var comando = conexion.CreateCommand();
        comando.CommandText = sentencia;
        comando.CommandTimeout = 0;
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }
}

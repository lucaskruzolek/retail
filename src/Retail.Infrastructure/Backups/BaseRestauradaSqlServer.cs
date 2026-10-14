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

    /// <summary>
    /// Copia el historial de la vigente a la temporal: primero borra de la temporal los registros cuya
    /// <c>Referencia</c> también está en la vigente (la vigente es la versión más reciente de ese hecho, R-18) y luego
    /// inserta todos los de la vigente, incluidos los dados de baja. Ambas tablas se tocan en una misma transacción.
    /// </summary>
    public async Task ImportarHistorialDeLaBaseVigenteAsync(CancellationToken cancellationToken = default)
    {
        await using var transaccion = await _context.Database.BeginTransactionAsync(cancellationToken);

        await ImportarTablaAsync<RegistroBackup>(cancellationToken);
        await ImportarTablaAsync<RegistroRestauracion>(cancellationToken);

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

    public async Task PromoverAVigenteAsync(CancellationToken cancellationToken = default)
    {
        if (_promovida || _liberada)
        {
            throw new InvalidOperationException("La base restaurada ya fue promovida o liberada.");
        }

        // La temporal no debe tener conexiones propias abiertas durante el renombrado.
        await _context.DisposeAsync();
        ComandosSqlServer.LimpiarPool(_cadenaMaster, _temporal);

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
                await EjecutarAsync(conexion, $"ALTER DATABASE {vigente} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;", cancellationToken);
                await EjecutarAsync(conexion, $"ALTER DATABASE {vigente} MODIFY NAME = {anterior};", cancellationToken);
            }
            catch (SqlException ex)
            {
                // No se renombró: la vigente sigue con su nombre, pero hay que devolverle el acceso multiusuario.
                await IntentarAsync(conexion, $"ALTER DATABASE {vigente} SET MULTI_USER;");
                throw new RestauracionFallidaException(
                    "No se pudo apartar la base vigente para el intercambio: quedó sin cambios.", ex);
            }

            await IntentarAsync(conexion, $"ALTER DATABASE {anterior} SET MULTI_USER;");
        }

        // Paso 2: la temporal ocupa el nombre de la vigente.
        try
        {
            await EjecutarAsync(conexion, $"ALTER DATABASE {temporal} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;", cancellationToken);
            await EjecutarAsync(conexion, $"ALTER DATABASE {temporal} MODIFY NAME = {vigente};", cancellationToken);
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
        if (!await IntentarAsync(conexion, $"ALTER DATABASE {vigente} SET MULTI_USER;") && _logger.IsEnabled(LogLevel.Critical))
        {
            _logger.LogCritical("La base {Vigente} quedó restaurada pero en modo SINGLE_USER: ejecute ALTER DATABASE ... SET MULTI_USER.", _vigente);
        }

        // El proceso deja de usar conexiones hacia la base reemplazada: la aplicación debe reiniciarse.
        SqlConnection.ClearAllPools();
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
            await EjecutarAsync(conexion, $"ALTER DATABASE {anterior} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;", CancellationToken.None);
            await EjecutarAsync(conexion, $"ALTER DATABASE {anterior} MODIFY NAME = {vigente};", CancellationToken.None);
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

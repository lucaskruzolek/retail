using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Backups;

/// <summary>
/// Bloqueo de lectores y escritor entre restauraciones y copias, implementado con un <c>sp_getapplock</c> de SQL Server.
/// </summary>
/// <remarks>
/// Semántica de lectores y escritor: la restauración toma el bloqueo <c>Exclusive</c> (escritor, uno solo y sin ningún
/// lector) y las copias toman <c>Shared</c> (lectores, pueden convivir entre sí). Como el bloqueo vive en el motor,
/// vale entre procesos distintos: la aplicación, <c>Retail.Cli</c> y la tarea programada.
/// <para>
/// Cada bloqueo usa una conexión <b>propia</b> a <c>master</c>, con el pool desactivado: un bloqueo de sesión se libera
/// al cerrar la conexión, y con pool la conexión física volvería al pool con el bloqueo tomado y contaminaría a otra
/// operación. Se apunta a <c>master</c> porque la base vigente puede estar siendo reemplazada o no existir (PC nueva),
/// y el bloqueo no debe depender de ella. El recurso lleva el nombre de la base, así que bases distintas (por ejemplo,
/// las de las pruebas en paralelo) no se bloquean entre sí.
/// </para>
/// </remarks>
public class SqlServerBloqueoRestauracion : IBloqueoRestauracion
{
    private const string PrefijoRecurso = "Retail.Restauracion.";

    /// <summary>
    /// Espera de las copias por el bloqueo compartido. Es corta a propósito: cubre el instante en que otra copia, al
    /// iniciar, toma el exclusivo para cerrar huérfanos (B-0), pero no hace esperar a una copia detrás de una restauración
    /// (que dura minutos): esa se rechaza.
    /// </summary>
    public static readonly TimeSpan EsperaCompartidoPorDefecto = TimeSpan.FromSeconds(10);

    private readonly RetailDbContext _context;
    private readonly TimeSpan _esperaCompartido;

    public SqlServerBloqueoRestauracion(RetailDbContext context, TimeSpan? esperaCompartido = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _esperaCompartido = esperaCompartido ?? EsperaCompartidoPorDefecto;
    }

    public async Task<IAsyncDisposable> AdquirirExclusivoAsync(CancellationToken cancellationToken = default)
    {
        var bloqueo = await IntentarAdquirirExclusivoAsync(cancellationToken);
        return bloqueo
            ?? throw new RestauracionFallidaException(
                "Hay otra restauración o una copia de seguridad en curso. Espere a que termine e intente de nuevo.");
    }

    public async Task<IAsyncDisposable?> IntentarAdquirirExclusivoAsync(CancellationToken cancellationToken = default)
    {
        return await TomarAsync("Exclusive", TimeSpan.Zero, cancellationToken);
    }

    public async Task<IAsyncDisposable> AdquirirCompartidoAsync(CancellationToken cancellationToken = default)
    {
        var bloqueo = await TomarAsync("Shared", _esperaCompartido, cancellationToken);
        return bloqueo
            ?? throw new BackupFallidoException(
                "Hay una restauración de la base u otra copia iniciándose en este momento. Intente de nuevo en unos segundos.");
    }

    /// <summary>
    /// Intenta tomar el bloqueo esperando como máximo <paramref name="espera"/> (cero: no espera). Devuelve <c>null</c> si
    /// no está disponible.
    /// </summary>
    private async Task<BloqueoAplicacion?> TomarAsync(string modo, TimeSpan espera, CancellationToken cancellationToken)
    {
        var cadena = _context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("No hay una cadena de conexión configurada para la base.");

        var constructor = new SqlConnectionStringBuilder(cadena);
        var recurso = PrefijoRecurso + constructor.InitialCatalog;
        constructor.InitialCatalog = "master";
        constructor.Pooling = false;

        var conexion = new SqlConnection(constructor.ConnectionString);
        try
        {
            await conexion.OpenAsync(cancellationToken);

            await using var comando = conexion.CreateCommand();
            comando.CommandText =
                "DECLARE @r int; " +
                "EXEC @r = sp_getapplock @Resource = @recurso, @LockMode = @modo, @LockOwner = 'Session', @LockTimeout = @espera; " +
                "SELECT @r;";
            comando.Parameters.AddWithValue("@recurso", recurso);
            comando.Parameters.AddWithValue("@modo", modo);
            comando.Parameters.AddWithValue("@espera", (int)espera.TotalMilliseconds);
            comando.CommandTimeout = Math.Max(30, (int)espera.TotalSeconds + 30);

            var resultado = (int)(await comando.ExecuteScalarAsync(cancellationToken))!;
            if (resultado < 0)
            {
                await conexion.DisposeAsync();
                return null;
            }

            return new BloqueoAplicacion(conexion, recurso);
        }
        catch
        {
            await conexion.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Mantiene viva la conexión que sostiene el bloqueo de sesión; al liberarlo lo suelta y cierra la conexión.
    /// </summary>
    private sealed class BloqueoAplicacion : IAsyncDisposable
    {
        private readonly SqlConnection _conexion;
        private readonly string _recurso;
        private int _liberado;

        public BloqueoAplicacion(SqlConnection conexion, string recurso)
        {
            _conexion = conexion;
            _recurso = recurso;
        }

        public async ValueTask DisposeAsync()
        {
            // Idempotente: solo la primera llamada libera.
            if (Interlocked.Exchange(ref _liberado, 1) == 1)
            {
                return;
            }

            try
            {
                if (_conexion.State == ConnectionState.Open)
                {
                    await using var comando = _conexion.CreateCommand();
                    comando.CommandText = "EXEC sp_releaseapplock @Resource = @recurso, @LockOwner = 'Session';";
                    comando.Parameters.AddWithValue("@recurso", _recurso);
                    await comando.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex) when (ex is SqlException or InvalidOperationException)
            {
                // Se ignora: cerrar la conexión (sin pool) libera igualmente el bloqueo de sesión.
            }
            finally
            {
                await _conexion.DisposeAsync();
            }
        }
    }
}

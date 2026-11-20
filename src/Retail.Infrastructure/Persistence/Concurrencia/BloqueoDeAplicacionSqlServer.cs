using Microsoft.Data.SqlClient;

namespace Retail.Infrastructure.Persistence.Concurrencia;

/// <summary>
/// Mecanismo reutilizable de bloqueo de aplicación de SQL Server (<c>sp_getapplock</c>): un bloqueo <b>con nombre</b> que
/// administra el motor, sin relación con tablas ni filas. Como vive en el servidor, vale entre procesos y PCs distintos.
/// </summary>
/// <remarks>
/// Cada bloqueo usa una conexión <b>propia</b> a <c>master</c>, con el pool desactivado: un bloqueo de sesión se libera
/// al cerrar la conexión, y con pool la conexión física volvería al pool con el bloqueo tomado y contaminaría a otra
/// operación. Se apunta a <c>master</c> porque la base vigente puede estar siendo reemplazada o no existir (PC nueva),
/// y el bloqueo no debe depender de ella.
/// </remarks>
internal static class BloqueoDeAplicacionSqlServer
{
    /// <summary>
    /// Devuelve el nombre de la base de la cadena de conexión, para que los recursos de bloqueo lleven el nombre de la
    /// base y bases distintas (por ejemplo, las de las pruebas en paralelo) no se bloqueen entre sí.
    /// </summary>
    public static string ObtenerNombreDeBase(string cadenaConexion)
    {
        return new SqlConnectionStringBuilder(cadenaConexion).InitialCatalog;
    }

    /// <summary>
    /// Intenta tomar el bloqueo <paramref name="recurso"/> esperando como máximo <paramref name="espera"/> (cero: no
    /// espera). Devuelve <c>null</c> si no está disponible.
    /// </summary>
    public static async Task<IAsyncDisposable?> TomarAsync(
        string cadenaConexion,
        string recurso,
        string modo,
        TimeSpan espera,
        CancellationToken cancellationToken)
    {
        var constructor = new SqlConnectionStringBuilder(cadenaConexion)
        {
            InitialCatalog = "master",
            Pooling = false
        };

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
}

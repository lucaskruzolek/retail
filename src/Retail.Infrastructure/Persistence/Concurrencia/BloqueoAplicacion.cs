using System.Data;
using Microsoft.Data.SqlClient;

namespace Retail.Infrastructure.Persistence.Concurrencia;

/// <summary>
/// Mantiene viva la conexión que sostiene el bloqueo de sesión; al liberarlo lo suelta y cierra la conexión.
/// </summary>
internal sealed class BloqueoAplicacion : IAsyncDisposable
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

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Backups;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Backups;

/// <summary>
/// Copias completas con las sentencias nativas de SQL Server (RF-22).
/// </summary>
/// <remarks>
/// Usa ADO.NET y no EF Core: <c>BACKUP DATABASE</c> no admite una transacción de usuario y EF no ofrece una API
/// para copias. La ruta viaja como parámetro; el nombre de la base sale de la cadena de conexión (configuración
/// de confianza) y además se delimita, de modo que no hay concatenación de datos externos en el SQL.
/// </remarks>
public class SqlServerEjecutorBackup : IEjecutorBackupBaseDatos
{
    private readonly RetailDbContext _context;

    public SqlServerEjecutorBackup(RetailDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ArchivoBackupDto> CrearCopiaAsync(string rutaArchivo, IProgress<int>? progreso, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rutaArchivo);

        var cadena = ObtenerCadenaDeConexion();
        var baseDeDatos = new SqlConnectionStringBuilder(cadena).InitialCatalog;

        await using (var conexion = new SqlConnection(cadena))
        {
            // Con STATS = 10 el motor informa el avance como mensajes de la conexión.
            ComandosSqlServer.SuscribirProgreso(conexion, progreso);

            await conexion.OpenAsync(cancellationToken);

            await using var comando = conexion.CreateCommand();
            comando.CommandText =
                $"BACKUP DATABASE {ComandosSqlServer.Delimitar(baseDeDatos)} TO DISK = @ruta " +
                "WITH FORMAT, INIT, CHECKSUM, STATS = 10, NAME = @nombre;";
            comando.Parameters.AddWithValue("@ruta", rutaArchivo);
            comando.Parameters.AddWithValue("@nombre", $"Retail POS - {Path.GetFileNameWithoutExtension(rutaArchivo)}");

            // Sin límite de tiempo: una base grande puede tardar minutos. Se cancela con el token.
            comando.CommandTimeout = 0;

            await comando.ExecuteNonQueryAsync(cancellationToken);
        }

        return await HuellaArchivo.CalcularAsync(rutaArchivo, cancellationToken);
    }

    public async Task VerificarCopiaAsync(string rutaArchivo, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rutaArchivo);

        await using var conexion = new SqlConnection(ObtenerCadenaDeConexion());
        await conexion.OpenAsync(cancellationToken);

        await using var comando = conexion.CreateCommand();

        // Lee el archivo completo y recalcula los checksums de página sin restaurar nada.
        comando.CommandText = "RESTORE VERIFYONLY FROM DISK = @ruta WITH CHECKSUM;";
        comando.Parameters.AddWithValue("@ruta", rutaArchivo);
        comando.CommandTimeout = 0;

        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private string ObtenerCadenaDeConexion()
    {
        return _context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("No hay una cadena de conexión configurada para la base.");
    }
}

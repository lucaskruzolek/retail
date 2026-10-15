using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Retail.Infrastructure.Backups;

/// <summary>
/// Piezas comunes de las sentencias nativas de SQL Server que usan la copia y la restauración: delimitar
/// identificadores, informar el avance del motor, armar cadenas de conexión y eliminar una base.
/// </summary>
internal static partial class ComandosSqlServer
{
    /// <summary>
    /// Cuenta de Windows con la que corre la tarea programada de la copia diaria (<c>scripts/registrar-tarea-backup.ps1</c>).
    /// Su usuario y sus permisos viven dentro de la base de datos.
    /// </summary>
    public const string CuentaDeLaTareaProgramada = @"NT AUTHORITY\SYSTEM";

    /// <summary>
    /// Encierra el nombre entre corchetes (duplicando los <c>]</c>): los identificadores no pueden viajar como
    /// parámetros, así que se delimitan para que un nombre nunca pueda salirse de su lugar en la sentencia.
    /// </summary>
    public static string Delimitar(string identificador)
    {
        return $"[{identificador.Replace("]", "]]", StringComparison.Ordinal)}]";
    }

    /// <summary>
    /// Misma cadena de conexión pero apuntando a otra base. Con <paramref name="pooling"/> en <c>false</c> la conexión
    /// no vuelve al pool (útil cuando su estado de sesión importa).
    /// </summary>
    public static string CadenaParaBase(string cadenaBase, string baseDeDatos, bool pooling = true)
    {
        var constructor = new SqlConnectionStringBuilder(cadenaBase)
        {
            InitialCatalog = baseDeDatos,
            Pooling = pooling
        };

        return constructor.ConnectionString;
    }

    /// <summary>
    /// Con <c>STATS = 10</c> el motor informa el avance como mensajes ("10 percent processed."). Puede agrupar varios
    /// en un mismo evento, así que se recorre cada uno.
    /// </summary>
    public static void SuscribirProgreso(SqlConnection conexion, IProgress<int>? progreso)
    {
        if (progreso is null)
        {
            return;
        }

        conexion.InfoMessage += (_, e) =>
        {
            foreach (SqlError mensaje in e.Errors)
            {
                InformarProgreso(mensaje.Message, progreso);
            }
        };
    }

    /// <summary>
    /// Descarta las conexiones agrupadas (pool) hacia una base, para que no la mantengan abierta ni reciban una
    /// conexión muerta cuando se elimine o se renombre.
    /// </summary>
    public static void LimpiarPool(string cadenaBase, string baseDeDatos)
    {
        using var conexion = new SqlConnection(CadenaParaBase(cadenaBase, baseDeDatos));
        SqlConnection.ClearPool(conexion);
    }

    /// <summary>
    /// Elimina la base si existe. Una base en estado <c>RESTORING</c> (restauración cortada) no admite
    /// <c>ALTER DATABASE</c>, así que se elimina directamente; las demás se pasan a <c>SINGLE_USER</c> para cortar
    /// cualquier conexión que la tenga abierta. Lanza si el motor no puede eliminarla.
    /// </summary>
    /// <param name="cadenaMaster">Cadena de conexión a <c>master</c>.</param>
    public static async Task EliminarBaseAsync(string cadenaMaster, string nombre, ILogger logger, CancellationToken cancellationToken)
    {
        LimpiarPool(cadenaMaster, nombre);

        await using var conexion = new SqlConnection(cadenaMaster);
        await conexion.OpenAsync(cancellationToken);

        string? estado;
        await using (var consulta = conexion.CreateCommand())
        {
            consulta.CommandText = "SELECT state_desc FROM sys.databases WHERE name = @nombre;";
            consulta.Parameters.AddWithValue("@nombre", nombre);
            estado = (string?)await consulta.ExecuteScalarAsync(cancellationToken);
        }

        if (estado is null)
        {
            return;
        }

        await using var comando = conexion.CreateCommand();
        var delimitado = Delimitar(nombre);
        comando.CommandText = string.Equals(estado, "RESTORING", StringComparison.OrdinalIgnoreCase)
            ? $"DROP DATABASE {delimitado};"
            : $"ALTER DATABASE {delimitado} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {delimitado};";
        comando.CommandTimeout = 0;
        await comando.ExecuteNonQueryAsync(cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Base {Base} eliminada.", nombre);
        }
    }

    private static void InformarProgreso(string mensaje, IProgress<int> progreso)
    {
        var coincidencia = PorcentajeInformado().Match(mensaje);
        if (coincidencia.Success && int.TryParse(coincidencia.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var porcentaje))
        {
            progreso.Report(Math.Clamp(porcentaje, 0, 100));
        }
    }

    // El mensaje empieza con el porcentaje en cualquier idioma del motor ("10 percent processed." / "10 por ciento...").
    [GeneratedRegex(@"^\s*(\d{1,3})\s")]
    private static partial Regex PorcentajeInformado();
}

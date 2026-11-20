using Microsoft.Data.SqlClient;
using Retail.Application.DTOs.Sistema;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Infrastructure.Backups;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Persistence.Services;

/// <summary>
/// Decide si esta PC es la que aloja SQL Server comparando el nombre físico del equipo del motor
/// (<c>SERVERPROPERTY('ComputerNamePhysicalNetBIOS')</c>) con <see cref="Environment.MachineName"/> (MT-12, H9).
/// </summary>
/// <remarks>
/// Se consulta contra <c>master</c> con la cadena de mantenimiento: sirve aunque la base de la aplicación todavía no
/// exista (recuperación ante desastres en una PC nueva). Un origen de datos local (<c>.</c>, <c>(local)</c>,
/// <c>localhost</c>, <c>(localdb)</c>, <c>127.0.0.1</c>) solo puede alcanzar esta PC: en un contenedor o un LocalDB la
/// propiedad puede devolver otro nombre, y aun así el motor está en este equipo.
/// </remarks>
public class EquipoServidorSqlServer : IEquipoServidor
{
    private static readonly string[] OrigenesLocales = [".", "(local)", "localhost", "127.0.0.1", "::1", "(localdb)"];

    private readonly RetailDbContext _context;
    private readonly CadenaConexionMantenimiento _mantenimiento;

    public EquipoServidorSqlServer(RetailDbContext context, CadenaConexionMantenimiento? mantenimiento = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mantenimiento = mantenimiento ?? new CadenaConexionMantenimiento(null);
    }

    public async Task<InformacionServidorDto> ObtenerAsync(CancellationToken cancellationToken = default)
    {
        var cadena = ComandosSqlServer.CadenaParaBase(_mantenimiento.Obtener(_context), "master", pooling: false);
        var origen = new SqlConnectionStringBuilder(cadena).DataSource;

        string? nombreFisico;
        await using (var conexion = new SqlConnection(cadena))
        {
            await conexion.OpenAsync(cancellationToken);

            await using var comando = conexion.CreateCommand();
            comando.CommandText = "SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ComputerNamePhysicalNetBIOS'));";
            nombreFisico = await comando.ExecuteScalarAsync(cancellationToken) as string;
        }

        return Evaluar(origen, nombreFisico, Environment.MachineName);
    }

    /// <summary>
    /// Regla pura (sin SQL) para poder probarla: <paramref name="origenDeDatos"/> es el <c>Data Source</c> de la cadena y
    /// <paramref name="nombreFisico"/> lo que informa el motor (puede ser <c>null</c>).
    /// </summary>
    internal static InformacionServidorDto Evaluar(string origenDeDatos, string? nombreFisico, string nombreEstaPc)
    {
        var host = ExtraerHost(origenDeDatos);
        var sinNombreFisico = string.IsNullOrWhiteSpace(nombreFisico);

        var esLocal = OrigenesLocales.Contains(host, StringComparer.OrdinalIgnoreCase);
        var mismoNombre = string.Equals(nombreFisico, nombreEstaPc, StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, nombreEstaPc, StringComparison.OrdinalIgnoreCase);

        // Un origen local es esta PC: se informa su nombre real y no el alias ("." o "localhost").
        var nombreServidor = esLocal && sinNombreFisico
            ? nombreEstaPc
            : sinNombreFisico ? host : nombreFisico!;

        return new InformacionServidorDto
        {
            EsEstaPc = esLocal || mismoNombre,
            NombreEquipoServidor = nombreServidor
        };
    }

    /// <summary>
    /// Nombre del equipo dentro de un <c>Data Source</c>: sin prefijo de protocolo (<c>tcp:</c>), sin puerto (<c>,1433</c>)
    /// ni instancia (<c>\SQLEXPRESS</c>).
    /// </summary>
    private static string ExtraerHost(string origenDeDatos)
    {
        var host = origenDeDatos.Trim();

        var protocolo = host.IndexOf(':', StringComparison.Ordinal);
        if (protocolo > 0 && host[..protocolo] is "tcp" or "np" or "lpc" or "admin")
        {
            host = host[(protocolo + 1)..];
        }

        foreach (var separador in new[] { ',', '\\' })
        {
            var corte = host.IndexOf(separador, StringComparison.Ordinal);
            if (corte > 0)
            {
                host = host[..corte];
            }
        }

        return host;
    }
}

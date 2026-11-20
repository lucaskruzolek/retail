using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Backups;

/// <summary>
/// Decide con qué cadena de conexión se hacen las copias, la restauración, su bloqueo y la migración posterior (H19).
/// </summary>
/// <remarks>
/// En una red local la aplicación se conecta con el login de mínimo privilegio <c>retail_app</c>, que no puede hacer
/// <c>BACKUP</c>, <c>RESTORE</c>, crear bases ni aplicar migraciones (DDL). Esas tareas se hacen solo en la PC servidor
/// (MT-12) y usan <c>ConnectionStrings:Mantenimiento</c>: una conexión local con autenticación de Windows y los permisos
/// necesarios. Si no está configurada (desarrollo en una sola PC), se usa la cadena del <see cref="RetailDbContext"/> y
/// todo funciona como antes. Es un único componente para que ninguna clase de <c>Backups/</c> decida por su cuenta.
/// </remarks>
public sealed class CadenaConexionMantenimiento
{
    private readonly string? _configurada;

    /// <param name="configurada">Valor de <c>ConnectionStrings:Mantenimiento</c>, o <c>null</c> si no está configurada.</param>
    public CadenaConexionMantenimiento(string? configurada)
    {
        _configurada = string.IsNullOrWhiteSpace(configurada) ? null : configurada;
    }

    /// <summary>Indica si hay una cadena de mantenimiento propia.</summary>
    public bool EstaConfigurada => _configurada is not null;

    /// <summary>
    /// Cadena para operaciones de mantenimiento. Si la configurada no indica la base (<c>Initial Catalog</c>), se toma la
    /// del contexto de la aplicación: el nombre de la base vigente sigue siendo uno solo.
    /// </summary>
    public string Obtener(RetailDbContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var deLaAplicacion = contexto.Database.GetConnectionString()
            ?? throw new InvalidOperationException("No hay una cadena de conexión configurada para la base.");

        if (_configurada is null)
        {
            return deLaAplicacion;
        }

        var constructor = new SqlConnectionStringBuilder(_configurada);
        if (string.IsNullOrWhiteSpace(constructor.InitialCatalog))
        {
            constructor.InitialCatalog = new SqlConnectionStringBuilder(deLaAplicacion).InitialCatalog;
        }

        return constructor.ConnectionString;
    }
}

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Retail.Infrastructure.Security;

namespace Retail.Infrastructure.Configuracion;

/// <summary>
/// Arma la cadena de conexión de la aplicación a partir de la configuración en capas (MT-7, MT-10).
/// </summary>
/// <remarks>
/// <c>ConnectionStrings:DefaultConnection</c> (o <c>RetailDatabase</c>) trae el servidor, la base y el usuario. Si existe
/// <c>BaseDeDatos:PasswordCifrada</c> (lo escribe <c>configurar-terminal.ps1</c> en <c>appsettings.local.json</c>), se
/// descifra con DPAPI y se aplica como <c>Password</c>, forzando la autenticación de SQL Server: así la contraseña nunca
/// queda en texto plano en el archivo. Sin ese valor, la cadena se usa tal cual (desarrollo con <c>Trusted_Connection</c>).
/// </remarks>
public static class CadenaDeConexion
{
    /// <summary>Clave de la contraseña cifrada (base64) dentro de la configuración.</summary>
    public const string ClavePasswordCifrada = "BaseDeDatos:PasswordCifrada";

    /// <summary>Nombre de la cadena opcional de mantenimiento (copias y restauración), en <c>ConnectionStrings</c>.</summary>
    public const string NombreMantenimiento = "Mantenimiento";

    private const string PorDefecto =
        @"Server=.;Database=RetailDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

    /// <summary>
    /// Cadena de conexión de la aplicación, con la contraseña descifrada aplicada si corresponde.
    /// </summary>
    /// <exception cref="InvalidOperationException">La contraseña cifrada no se puede descifrar en esta PC.</exception>
    public static string Resolver(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var cadena = configuration.GetConnectionString("DefaultConnection")
            ?? configuration.GetConnectionString("RetailDatabase")
            ?? PorDefecto;

        var passwordCifrada = configuration[ClavePasswordCifrada];
        if (string.IsNullOrWhiteSpace(passwordCifrada))
        {
            return cadena;
        }

        var constructor = new SqlConnectionStringBuilder(cadena)
        {
            Password = ProtectorCredencialSql.Descifrar(passwordCifrada),
            IntegratedSecurity = false
        };

        return constructor.ConnectionString;
    }

    /// <summary>
    /// Cadena de mantenimiento configurada (<c>ConnectionStrings:Mantenimiento</c>), o <c>null</c> si no hay.
    /// </summary>
    public static string? ResolverMantenimiento(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var cadena = configuration.GetConnectionString(NombreMantenimiento);
        return string.IsNullOrWhiteSpace(cadena) ? null : cadena;
    }

    /// <summary>
    /// Para <c>Retail.Cli</c>, la herramienta de administración que corre en la PC servidor: si hay una cadena de
    /// mantenimiento, pasa a ser la conexión principal. Así <c>migrar</c> crea y modifica el esquema con una identidad
    /// que tiene permisos de DDL, y no con el login <c>retail_app</c> de las terminales, que deliberadamente no los tiene
    /// (MT-10). Si la cadena de mantenimiento no indica la base, se toma la de <c>DefaultConnection</c>. Sin cadena de
    /// mantenimiento (por ejemplo, en desarrollo) la configuración no cambia.
    /// </summary>
    /// <remarks>
    /// Se agrega como la última fuente de configuración, así que también tiene prioridad sobre las variables de entorno:
    /// quien configuró una conexión de mantenimiento en esta PC eligió esa identidad para administrar la base.
    /// </remarks>
    public static void UsarMantenimientoComoPrincipal(IConfigurationManager configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var mantenimiento = ResolverMantenimiento(configuration);
        if (mantenimiento is null)
        {
            return;
        }

        var constructor = new SqlConnectionStringBuilder(mantenimiento);
        if (string.IsNullOrWhiteSpace(constructor.InitialCatalog))
        {
            var principal = configuration.GetConnectionString("DefaultConnection")
                ?? configuration.GetConnectionString("RetailDatabase")
                ?? PorDefecto;
            constructor.InitialCatalog = new SqlConnectionStringBuilder(principal).InitialCatalog;
        }

        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = constructor.ConnectionString,
            ["ConnectionStrings:RetailDatabase"] = constructor.ConnectionString,
            // La contraseña cifrada es la de retail_app: con la identidad de mantenimiento no se aplica.
            [ClavePasswordCifrada] = string.Empty
        });
    }
}

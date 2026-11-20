using Retail.Cli.Consola;
using Retail.Infrastructure.Configuracion;
using Retail.Infrastructure.Security;

namespace Retail.Cli.Comandos;

/// <summary>
/// <c>cifrar-password</c>: cifra la contraseña del login de SQL Server para pegarla en <c>appsettings.local.json</c> (MT-10).
/// El resultado solo sirve en ESTA PC (DPAPI con alcance de equipo). No necesita la base: por eso se despacha antes de
/// construir el host, así sirve también para reparar un valor ilegible que impide arrancar.
/// </summary>
internal static class ComandoCifrarPassword
{
    public static int Ejecutar()
    {
        var password = EntradaConsola.LeerSecretoConfirmado("Contraseña del login de SQL Server");
        if (string.IsNullOrEmpty(password))
        {
            Console.Error.WriteLine("La contraseña no puede estar vacía.");
            return CodigoSalida.UsoIncorrecto;
        }

        var cifrada = ProtectorCredencialSql.Cifrar(password);

        Console.WriteLine();
        Console.WriteLine($"Agregue esto a {ConfiguracionLocal.ObtenerRutaArchivo()} (y quite la contraseña de la cadena de conexión):");
        Console.WriteLine();
        Console.WriteLine("  \"BaseDeDatos\": {");
        Console.WriteLine($"    \"PasswordCifrada\": \"{cifrada}\"");
        Console.WriteLine("  }");
        Console.WriteLine();
        Console.WriteLine("El valor solo se puede descifrar en esta PC.");

        return CodigoSalida.Exito;
    }
}

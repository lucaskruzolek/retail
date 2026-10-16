using System.Text;

namespace Retail.Cli.Consola;

/// <summary>
/// Lectura interactiva de datos. Si la entrada viene redirigida (un script), lee líneas completas.
/// </summary>
internal static class EntradaConsola
{
    public static string LeerTexto(string etiqueta)
    {
        Console.Write($"{etiqueta}: ");
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Lee una contraseña sin mostrarla: cada carácter se reemplaza por un asterisco.
    /// </summary>
    public static string LeerSecreto(string etiqueta)
    {
        Console.Write($"{etiqueta}: ");

        if (Console.IsInputRedirected)
        {
            return Console.ReadLine() ?? string.Empty;
        }

        var secreto = new StringBuilder();
        while (true)
        {
            var tecla = Console.ReadKey(intercept: true);

            if (tecla.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return secreto.ToString();
            }

            if (tecla.Key == ConsoleKey.Backspace)
            {
                if (secreto.Length > 0)
                {
                    secreto.Length--;
                    Console.Write("\b \b");
                }

                continue;
            }

            if (!char.IsControl(tecla.KeyChar))
            {
                secreto.Append(tecla.KeyChar);
                Console.Write('*');
            }
        }
    }

    /// <summary>
    /// Pide la contraseña dos veces y repite hasta que coincidan (protección contra errores de tipeo).
    /// </summary>
    public static string LeerSecretoConfirmado(string etiqueta)
    {
        while (true)
        {
            var secreto = LeerSecreto(etiqueta);
            var confirmacion = LeerSecreto("Repita la contraseña");

            if (secreto == confirmacion)
            {
                return secreto;
            }

            Console.WriteLine("Las contraseñas no coinciden. Intente nuevamente.");

            // Con entrada redirigida no hay un usuario que pueda corregir: se evita un ciclo infinito.
            if (Console.IsInputRedirected)
            {
                throw new EntradaInvalidaException("Las contraseñas ingresadas no coinciden.");
            }
        }
    }
}

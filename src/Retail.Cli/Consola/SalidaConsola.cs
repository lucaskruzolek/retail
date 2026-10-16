using FluentValidation;
using Retail.Application.Exceptions;
using Retail.Domain.Exceptions;
using Retail.Infrastructure.Logging;

namespace Retail.Cli.Consola;

internal static class SalidaConsola
{
    /// <summary>
    /// Muestra un error de forma legible: los de validación, uno por línea; las reglas de negocio, con su mensaje;
    /// el resto, con su tipo para facilitar el diagnóstico (el detalle completo queda en el log).
    /// </summary>
    public static void Error(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        Console.Error.WriteLine();
        switch (ex)
        {
            case ValidationException validacion when validacion.Errors.Any():
                Console.Error.WriteLine("Los datos ingresados no son válidos:");
                foreach (var mensaje in validacion.Errors.Select(e => e.ErrorMessage).Distinct())
                {
                    Console.Error.WriteLine($"  - {mensaje}");
                }

                break;

            case DomainException or EntradaInvalidaException or BackupFallidoException or RestauracionFallidaException:
                Console.Error.WriteLine($"Error: {ex.Message}");
                break;

            default:
                Console.Error.WriteLine($"Error inesperado ({ex.GetType().Name}): {ex.Message}");
                Console.Error.WriteLine($"El detalle completo quedó registrado en {Path.Combine(UbicacionDeLogs.Directorio(), "retail-cli-*.log")}.");
                break;
        }
    }
}

using System.Globalization;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Retail.Application;
using Retail.Application.Exceptions;
using Retail.Cli.Comandos;
using Retail.Cli.Consola;
using Retail.Domain.Exceptions;
using Retail.Infrastructure;
using Retail.Infrastructure.Logging;
using Serilog;
using Serilog.Events;

namespace Retail.Cli;

/// <summary>
/// Herramienta de administración de la base fuera de la interfaz gráfica (RF-23, IS-04).
/// Es una segunda raíz de composición: registra las mismas capas que <c>Retail.App</c> más las operaciones de
/// <c>AddAdministracionSistema</c>. Cualquiera con acceso al equipo puede ejecutarla, por eso ninguno de sus comandos
/// otorga acceso a una cuenta existente (D-13); <c>restaurar</c> exige la contraseña de un Gerente verificada dentro de
/// la copia (D-18).
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        // La consola de Windows usa por defecto una página de códigos que corrompe los acentos. El Programador de tareas
        // (B-5) puede ejecutar la herramienta sin consola o con la salida cerrada: sin consola no hay nada que
        // configurar, y no es motivo para no hacer la copia.
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch (IOException)
        {
        }

        if (args.Length == 0 || args[0] is "ayuda" or "-h" or "--help")
        {
            Ayuda.Mostrar();
            return args.Length == 0 ? CodigoSalida.UsoIncorrecto : CodigoSalida.Exito;
        }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            // Los propios registros de la herramienta se conservan en Information; el ruido de las bibliotecas no. EF Core
            // escribe cada sentencia SQL ("Executed DbCommand") en Information: una copia dejaba 19 entradas con el SQL completo.
            // Los errores y advertencias de EF Core (por ejemplo, un comando que falla) siguen apareciendo.
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            // Solo a archivo: la consola es del operador y la escriben los comandos (SalidaConsola).
            .WriteTo.File(
                path: Path.Combine(UbicacionDeLogs.Directorio(), "retail-cli-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31,
                formatProvider: CultureInfo.InvariantCulture,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            // Los argumentos son del comando, no de configuración: no se pasan al host.
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                ContentRootPath = AppContext.BaseDirectory
            });

            builder.Services.AddSerilog();
            builder.Services.AddApplicationServices();
            builder.Services.AddAdministracionSistema();
            builder.Services.AddInfrastructureServices(builder.Configuration);

            builder.Services.AddTransient<ComandoEstado>();
            builder.Services.AddTransient<ComandoMigrar>();
            builder.Services.AddTransient<ComandoInicializar>();
            builder.Services.AddTransient<ComandoDatosDemo>();
            builder.Services.AddTransient<ComandoCargarDiccionarios>();
            builder.Services.AddTransient<ComandoBackup>();
            builder.Services.AddTransient<ComandoRestaurar>();

            using var host = builder.Build();
            using var scope = host.Services.CreateScope();
            var servicios = scope.ServiceProvider;

            Log.Information("Retail.Cli: comando '{Comando}' ejecutado por {UsuarioWindows} en {Equipo}",
                args[0], Environment.UserName, Environment.MachineName);

            return args[0] switch
            {
                "estado" => await servicios.GetRequiredService<ComandoEstado>().EjecutarAsync(),
                "migrar" => await servicios.GetRequiredService<ComandoMigrar>().EjecutarAsync(),
                "inicializar" => await servicios.GetRequiredService<ComandoInicializar>().EjecutarAsync(),
                "datos-demo" => await servicios.GetRequiredService<ComandoDatosDemo>().EjecutarAsync(),
                "cargar-diccionarios" => await servicios.GetRequiredService<ComandoCargarDiccionarios>().EjecutarAsync(),
                "backup" => await servicios.GetRequiredService<ComandoBackup>().EjecutarAsync(),
                "restaurar" => await servicios.GetRequiredService<ComandoRestaurar>().EjecutarAsync(args[1..]),
                _ => ComandoDesconocido(args[0])
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("Operación cancelada.");
            Log.Warning("Retail.Cli: el comando '{Comando}' fue cancelado por el operador", args[0]);
            return CodigoSalida.Fallo;
        }
        catch (Exception ex) when (ex is BackupFallidoException or RestauracionFallidaException)
        {
            // Error esperado: a la consola va el mensaje claro; al log, también la excepción, para conservar el detalle
            // técnico de la causa (InnerException) que el operador no necesita ver.
            Log.Warning(ex, "Retail.Cli: el comando '{Comando}' fue rechazado: {Motivo}", args[0], ex.Message);
            SalidaConsola.Error(ex);
            return CodigoSalida.Fallo;
        }
        catch (Exception ex) when (ex is ValidationException or DomainException or EntradaInvalidaException)
        {
            // Error esperado (datos inválidos o regla de negocio): se informa sin el stack trace.
            Log.Warning("Retail.Cli: el comando '{Comando}' fue rechazado: {Motivo}", args[0], ex.Message);
            SalidaConsola.Error(ex);
            return CodigoSalida.Fallo;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Retail.Cli: el comando '{Comando}' falló", args[0]);
            SalidaConsola.Error(ex);
            return CodigoSalida.Fallo;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    private static int ComandoDesconocido(string comando)
    {
        Console.Error.WriteLine($"Comando desconocido: '{comando}'.");
        Console.Error.WriteLine();
        Ayuda.Mostrar();
        return CodigoSalida.UsoIncorrecto;
    }
}

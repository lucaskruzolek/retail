using Microsoft.Extensions.Logging;
using Retail.Application.Interfaces.Services;

namespace Retail.Cli.Comandos;

/// <summary>
/// <c>migrar</c>: actualiza el esquema de la base (D-10). Reemplaza al MigrateAsync que antes corría al abrir la app
/// y, si la base ya tiene datos, toma antes una copia <c>PreMigracion</c>.
/// </summary>
internal sealed class ComandoMigrar
{
    private readonly IEstadoSistemaService _estadoSistema;
    private readonly IAdministracionSistemaService _administracion;
    private readonly ILogger<ComandoMigrar> _logger;

    public ComandoMigrar(
        IEstadoSistemaService estadoSistema,
        IAdministracionSistemaService administracion,
        ILogger<ComandoMigrar> logger)
    {
        _estadoSistema = estadoSistema ?? throw new ArgumentNullException(nameof(estadoSistema));
        _administracion = administracion ?? throw new ArgumentNullException(nameof(administracion));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> EjecutarAsync()
    {
        var esquema = (await _estadoSistema.ObtenerEstadoAsync()).Esquema;

        if (esquema.EstaAlDia)
        {
            Console.WriteLine("La base ya está al día. No hay migraciones pendientes.");
            return CodigoSalida.Exito;
        }

        Console.WriteLine(esquema.PuedeConectar
            ? $"Hay {esquema.MigracionesPendientes.Count} migraciones pendientes. Primero se toma una copia de seguridad..."
            : "La base no existe o no responde: se intentará crearla y aplicar todas las migraciones...");

        var resultado = await _administracion.MigrarAsync();

        if (resultado.CopiaPrevia is { } copia)
        {
            Console.WriteLine($"Copia previa verificada: {copia.RutaArchivo} ({copia.TamanoBytes / 1024:N0} KB)");
            if (!copia.RegistradaEnHistorial)
            {
                Console.WriteLine("  (La base todavía no tenía historial de copias: quedó documentada en su manifiesto .json)");
            }
        }

        foreach (var migracion in resultado.Aplicadas)
        {
            Console.WriteLine($"  + {migracion}");
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Migraciones aplicadas desde Retail.Cli: {Migraciones}. Copia previa: {Copia}",
                resultado.Aplicadas,
                resultado.CopiaPrevia?.RutaArchivo ?? "ninguna");
        }

        Console.WriteLine($"Listo: {resultado.Aplicadas.Count} migraciones aplicadas.");
        return CodigoSalida.Exito;
    }
}

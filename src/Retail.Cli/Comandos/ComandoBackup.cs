using Microsoft.Extensions.Logging;
using Retail.Application.Interfaces.Services;
using Retail.Cli.Consola;
using Retail.Domain.Enums;

namespace Retail.Cli.Comandos;

/// <summary>
/// <c>backup</c>: copia completa de la base, registrada como automática (RF-22, D-14). La invoca el Programador de
/// tareas; también se puede ejecutar a mano. No pide credenciales: una copia no da acceso a ninguna cuenta ni
/// modifica datos (D-13). El contenido del archivo lo protegen los permisos de Windows del directorio (R-17).
/// </summary>
internal sealed class ComandoBackup
{
    private readonly IEstadoSistemaService _estadoSistema;
    private readonly IBackupService _backupService;
    private readonly ILogger<ComandoBackup> _logger;

    public ComandoBackup(IEstadoSistemaService estadoSistema, IBackupService backupService, ILogger<ComandoBackup> logger)
    {
        _estadoSistema = estadoSistema ?? throw new ArgumentNullException(nameof(estadoSistema));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> EjecutarAsync()
    {
        var estado = await _estadoSistema.ObtenerEstadoAsync();
        if (!estado.Esquema.EstaAlDia)
        {
            Console.Error.WriteLine("La base no está al día. Ejecute primero: Retail.Cli migrar (ya incluye una copia previa).");
            return CodigoSalida.Fallo;
        }

        Console.Write("Copiando la base");
        var progreso = new ProgresoEnConsola();

        using var cancelacion = new CancelacionConsola();
        var resultado = await _backupService.CrearBackupDelSistemaAsync(TipoBackupEnum.Automatico, progreso, cancelacion.Token);

        Console.WriteLine();
        Console.WriteLine($"Copia verificada: {resultado.RutaArchivo} ({resultado.TamanoBytes / 1024:N0} KB)");
        Console.WriteLine($"SHA-256: {resultado.HashSha256}");

        foreach (var purgada in resultado.CopiasPurgadas)
        {
            Console.WriteLine($"  Retención: se borró {Path.GetFileName(purgada)}");
        }

        foreach (var error in resultado.ErroresDeRetencion)
        {
            Console.WriteLine($"  Retención: no se pudo borrar {error}");
            _logger.LogWarning("Retención de copias: {Error}", error);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Copia automática desde Retail.Cli: {Ruta}", resultado.RutaArchivo);
        }

        return CodigoSalida.Exito;
    }
}

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Retail.Application.DTOs.Sistema;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Services;
using Retail.Cli.Consola;
using Retail.Domain.Enums;

namespace Retail.Cli.Comandos;

/// <summary>
/// <c>backup</c>: copia completa de la base, registrada como automática (RF-22, D-28). La invoca el Programador de
/// tareas; también se puede ejecutar a mano. No pide credenciales: una copia no da acceso a ninguna cuenta ni
/// modifica datos (D-13). El contenido del archivo lo protegen los permisos de Windows del directorio (R-17). Si el
/// motor todavía no acepta conexiones (la tarea pudo lanzarse al encender la PC), espera hasta 2 minutos; si la base no
/// está lista deja el motivo en pantalla y en el log.
/// </summary>
internal sealed class ComandoBackup
{
    // Con "iniciar si se perdió", el Programador de tareas puede lanzar la copia apenas enciende la PC, antes de que
    // SQL Server termine de arrancar. Se espera al motor (no se reintenta la copia: D-28): si responde, se copia una vez.
    private static readonly TimeSpan IntervaloDeEspera = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan EsperaMaxima = TimeSpan.FromMinutes(2);

    private readonly IEsquemaBaseDatos _esquema;
    private readonly IBackupService _backupService;
    private readonly ILogger<ComandoBackup> _logger;

    public ComandoBackup(IEsquemaBaseDatos esquema, IBackupService backupService, ILogger<ComandoBackup> logger)
    {
        _esquema = esquema ?? throw new ArgumentNullException(nameof(esquema));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> EjecutarAsync()
    {
        using var cancelacion = new CancelacionConsola();

        // Solo se consulta el esquema (migraciones), no el estado de los usuarios: la cuenta de la tarea programada no
        // tiene ni necesita permiso sobre USUARIOS (mínimo privilegio).
        var esquema = await EsperarAlMotorAsync(cancelacion.Token);
        if (!esquema.EstaAlDia)
        {
            return InformarBaseNoLista(esquema);
        }

        Console.Write("Copiando la base");
        var progreso = new ProgresoEnConsola();

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

    /// <summary>
    /// Consulta el esquema y, mientras el motor no acepte conexiones, vuelve a consultar cada 10 segundos hasta un máximo
    /// de 2 minutos. Después devuelve el último estado, sea cual sea.
    /// </summary>
    private async Task<EstadoEsquemaDto> EsperarAlMotorAsync(CancellationToken cancellationToken)
    {
        var cronometro = Stopwatch.StartNew();
        var esquema = await _esquema.ObtenerEstadoAsync(cancellationToken);

        while (!esquema.PuedeConectar && cronometro.Elapsed < EsperaMaxima)
        {
            var transcurridos = (int)cronometro.Elapsed.TotalSeconds;
            var maximo = (int)EsperaMaxima.TotalSeconds;
            Console.Error.WriteLine($"SQL Server todavía no responde ({transcurridos} s de {maximo} s): se vuelve a intentar en {(int)IntervaloDeEspera.TotalSeconds} s...");
            _logger.LogWarning(
                "SQL Server no acepta conexiones todavía (esperando {Transcurridos} s de {Maximo} s); se vuelve a consultar en 10 s.",
                transcurridos,
                maximo);

            await Task.Delay(IntervaloDeEspera, cancellationToken);
            esquema = await _esquema.ObtenerEstadoAsync(cancellationToken);
        }

        return esquema;
    }

    /// <summary>
    /// La copia automática no corre sobre una base que no responde, sin migrar o de una versión posterior. Deja el motivo
    /// exacto en pantalla y en el log (el Programador de tareas solo guarda un código de salida).
    /// </summary>
    private int InformarBaseNoLista(EstadoEsquemaDto esquema)
    {
        var pendientes = esquema.MigracionesPendientes.Count;
        var desconocidas = esquema.MigracionesDesconocidas.Count;

        if (!esquema.PuedeConectar)
        {
            Console.Error.WriteLine("No se pudo conectar con la base: SQL Server no responde o la base no existe. Revise que el servicio de SQL Server esté iniciado y que la base exista (Retail.Cli estado).");
            _logger.LogError(
                "Copia automática no realizada: no se pudo conectar con la base (PuedeConectar={PuedeConectar}, migraciones pendientes={Pendientes}, desconocidas={Desconocidas}).",
                esquema.PuedeConectar,
                pendientes,
                desconocidas);
            return CodigoSalida.Fallo;
        }

        if (desconocidas > 0)
        {
            Console.Error.WriteLine("La base es más nueva que esta versión de Retail.Cli (tiene migraciones que esta versión no conoce): actualice la aplicación. No se copia con una versión anterior.");
        }

        if (pendientes > 0)
        {
            Console.Error.WriteLine("La base no está migrada (faltan migraciones). Ejecute primero: Retail.Cli migrar (ya incluye una copia previa).");
        }

        _logger.LogError(
            "Copia automática no realizada: la base no está al día (PuedeConectar={PuedeConectar}, migraciones pendientes={Pendientes}: {ListaPendientes}; desconocidas={Desconocidas}: {ListaDesconocidas}).",
            esquema.PuedeConectar,
            pendientes,
            string.Join(", ", esquema.MigracionesPendientes),
            desconocidas,
            string.Join(", ", esquema.MigracionesDesconocidas));
        return CodigoSalida.Fallo;
    }
}

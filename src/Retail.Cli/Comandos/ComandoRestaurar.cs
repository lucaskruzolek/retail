using System.Globalization;
using Microsoft.Extensions.Logging;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Services;
using Retail.Cli.Consola;

namespace Retail.Cli.Comandos;

/// <summary>
/// <c>restaurar</c>: recuperación ante desastres (RF-22, D-18, R-2). Reemplaza la base vigente por una copia
/// <c>.bak</c>. No concede acceso por sí mismo (D-13): exige el usuario y la contraseña de un Gerente que exista
/// <b>dentro de la copia</b>, porque la base vigente puede no existir o estar dañada.
/// </summary>
internal sealed class ComandoRestaurar
{
    private const string PalabraDeConfirmacion = "RESTAURAR";

    private readonly IArchivosBackup _archivosBackup;
    private readonly IRestauracionService _restauracionService;
    private readonly ILogger<ComandoRestaurar> _logger;

    public ComandoRestaurar(
        IArchivosBackup archivosBackup,
        IRestauracionService restauracionService,
        ILogger<ComandoRestaurar> logger)
    {
        _archivosBackup = archivosBackup ?? throw new ArgumentNullException(nameof(archivosBackup));
        _restauracionService = restauracionService ?? throw new ArgumentNullException(nameof(restauracionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <param name="argumentos">Argumentos del comando (sin su nombre): debe ser exactamente la ruta del <c>.bak</c>.</param>
    public async Task<int> EjecutarAsync(string[] argumentos)
    {
        ArgumentNullException.ThrowIfNull(argumentos);

        if (argumentos.Length != 1)
        {
            Console.Error.WriteLine("Uso: Retail.Cli restaurar <ruta-del-archivo.bak>");
            return CodigoSalida.UsoIncorrecto;
        }

        var ruta = argumentos[0];

        // Si el archivo o su manifiesto no existen o son ilegibles, lanza RestauracionFallidaException (Program la informa).
        var manifiesto = await _archivosBackup.LeerManifiestoAsync(ruta);

        Console.WriteLine("Copia a restaurar");
        Console.WriteLine($"  Archivo:           {Path.GetFileName(ruta)}");
        Console.WriteLine($"  Fecha:             {manifiesto.FechaInicioUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture)}");
        Console.WriteLine($"  Tipo:              {manifiesto.Tipo}");
        Console.WriteLine($"  Operador:          {manifiesto.Operador}");
        Console.WriteLine($"  Tamaño:            {manifiesto.TamanoBytes / 1024:N0} KB");
        Console.WriteLine($"  Última migración:  {manifiesto.UltimaMigracion}");
        Console.WriteLine($"  Versión de la app: {manifiesto.VersionApp}");
        Console.WriteLine($"  SHA-256:           {Abreviar(manifiesto.HashSha256)}");
        Console.WriteLine();
        Console.WriteLine("ATENCIÓN");
        Console.WriteLine("  - La base de datos actual será REEMPLAZADA por esta copia.");
        Console.WriteLine("  - La base actual no se borra: queda renombrada como <base>_Anterior_<fecha>, y si es posible");
        Console.WriteLine("    se toma antes una copia de tipo PreRestauracion.");
        Console.WriteLine("  - Cierre Retail POS en TODAS las terminales antes de continuar: las conexiones abiertas se cortarán.");
        Console.WriteLine("  - Al terminar, toda aplicación abierta debe reiniciarse.");
        Console.WriteLine();
        Console.WriteLine("Para autorizar la restauración ingrese un Gerente que exista en ESTA copia");
        Console.WriteLine("(las credenciales se verifican dentro de la copia, no contra la base actual).");

        var usuario = EntradaConsola.LeerTexto("Usuario");
        var password = EntradaConsola.LeerSecreto("Contraseña");

        Console.WriteLine();
        var confirmacion = EntradaConsola.LeerTexto($"Escriba {PalabraDeConfirmacion} para confirmar");
        if (!string.Equals(confirmacion, PalabraDeConfirmacion, StringComparison.Ordinal))
        {
            // Se devuelve Fallo y no Exito: una operación cancelada no hizo lo que el script o el operador pidieron.
            Console.Error.WriteLine("Restauración cancelada.");
            return CodigoSalida.Fallo;
        }

        Console.Write("Restaurando la base");
        var progreso = new ProgresoEnConsola();

        using var cancelacion = new CancelacionConsola();
        var resultado = await _restauracionService.RestaurarDesdeArchivoAsync(ruta, usuario, password, progreso, cancelacion.Token);

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine("Restauración completada.");
        Console.WriteLine(resultado.BaseAnterior is null
            ? "  Base anterior:  no había una base vigente."
            : $"  Base anterior:  conservada como {resultado.BaseAnterior}");
        Console.WriteLine(resultado.ReferenciaCopiaPrevia is null
            ? "  Copia previa:   sin copia previa."
            : $"  Copia previa:   {resultado.ReferenciaCopiaPrevia}");

        if (resultado.MigracionesAplicadas.Count > 0)
        {
            Console.WriteLine("  Migraciones aplicadas a la copia:");
            foreach (var migracion in resultado.MigracionesAplicadas)
            {
                Console.WriteLine($"    - {migracion}");
            }
        }

        // La primera advertencia es siempre la conciliación con ARCA (R-3): se destaca para que no pase inadvertida.
        for (var i = 0; i < resultado.Advertencias.Count; i++)
        {
            Console.WriteLine();
            Console.WriteLine(i == 0 ? "!!! IMPORTANTE !!!" : "Advertencia:");
            Console.WriteLine($"  {resultado.Advertencias[i]}");
        }

        Console.WriteLine();
        Console.WriteLine("Reinicie Retail POS en todas las terminales.");

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Restauración {Referencia} completada desde Retail.Cli por {UsuarioWindows} en {Equipo}",
                resultado.ReferenciaRestauracion, Environment.UserName, Environment.MachineName);
        }

        return CodigoSalida.Exito;
    }

    private static string Abreviar(string hash)
    {
        return hash.Length > 16 ? hash[..16] + "..." : hash;
    }
}

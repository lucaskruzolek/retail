using Retail.Application.Interfaces.Services;

namespace Retail.Cli.Comandos;

/// <summary>
/// <c>estado</c>: mismo diagnóstico que la aplicación hace al arrancar, en texto. Devuelve 0 si puede operar.
/// </summary>
internal sealed class ComandoEstado
{
    private readonly IEstadoSistemaService _estadoSistema;

    public ComandoEstado(IEstadoSistemaService estadoSistema)
    {
        _estadoSistema = estadoSistema ?? throw new ArgumentNullException(nameof(estadoSistema));
    }

    public async Task<int> EjecutarAsync()
    {
        var estado = await _estadoSistema.ObtenerEstadoAsync();
        var esquema = estado.Esquema;

        Console.WriteLine($"Conexión con la base:      {(esquema.PuedeConectar ? "OK" : "NO (servidor inaccesible o base inexistente)")}");
        Console.WriteLine($"Migraciones pendientes:    {esquema.MigracionesPendientes.Count}");
        foreach (var migracion in esquema.MigracionesPendientes)
        {
            Console.WriteLine($"    - {migracion}");
        }

        Console.WriteLine($"Migraciones desconocidas:  {esquema.MigracionesDesconocidas.Count}");
        foreach (var migracion in esquema.MigracionesDesconocidas)
        {
            Console.WriteLine($"    - {migracion}  (la base es más nueva que esta versión)");
        }

        if (esquema.EstaAlDia)
        {
            Console.WriteLine($"Gerente activo:            {(estado.ExisteGerenteActivo ? "Sí" : "NO (ejecute 'inicializar')")}");
            if (estado.GerentesSinEmail.Count > 0)
            {
                Console.WriteLine($"Gerentes sin email:        {string.Join(", ", estado.GerentesSinEmail)}");
            }
        }

        Console.WriteLine();
        Console.WriteLine(estado.PuedeOperar ? "La aplicación puede operar." : "La aplicación NO puede operar con la base en este estado.");

        return estado.PuedeOperar ? CodigoSalida.Exito : CodigoSalida.Fallo;
    }
}

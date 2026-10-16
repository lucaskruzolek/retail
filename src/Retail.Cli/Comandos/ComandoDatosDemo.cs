using Retail.Application.Interfaces.Services;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Initialization;

namespace Retail.Cli.Comandos;

/// <summary>
/// <c>datos-demo</c>: carga el proveedor, las categorías y las marcas de demostración. Es opcional y se puede
/// repetir: solo agrega lo que falta.
/// </summary>
internal sealed class ComandoDatosDemo
{
    private readonly IEstadoSistemaService _estadoSistema;
    private readonly RetailDbContext _context;

    public ComandoDatosDemo(IEstadoSistemaService estadoSistema, RetailDbContext context)
    {
        _estadoSistema = estadoSistema ?? throw new ArgumentNullException(nameof(estadoSistema));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<int> EjecutarAsync()
    {
        var estado = await _estadoSistema.ObtenerEstadoAsync();
        if (!estado.Esquema.EstaAlDia)
        {
            Console.Error.WriteLine("La base no está al día. Ejecute primero: Retail.Cli migrar");
            return CodigoSalida.Fallo;
        }

        var agregados = await SemilleroDatosDemo.CargarAsync(_context);

        Console.WriteLine(agregados == 0
            ? "Los datos de demostración ya estaban cargados. No se agregó nada."
            : $"Se agregaron {agregados} registros de demostración (proveedor, categorías y marcas).");
        return CodigoSalida.Exito;
    }
}

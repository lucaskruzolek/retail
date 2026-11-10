using Microsoft.EntityFrameworkCore;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Services;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Initialization;

namespace Retail.Cli.Comandos;

/// <summary>
/// <c>historial-demo</c>: simula meses de operación para demostrar las funciones y los reportes. Se rechaza en una base
/// que ya tenga ventas o turnos de caja, así que no se puede repetir ni mezclar con datos reales.
/// </summary>
internal sealed class ComandoHistorialDemo
{
    private readonly IEstadoSistemaService _estadoSistema;
    private readonly RetailDbContext _context;
    private readonly IPasswordHasher _hasher;

    public ComandoHistorialDemo(IEstadoSistemaService estadoSistema, RetailDbContext context, IPasswordHasher hasher)
    {
        _estadoSistema = estadoSistema ?? throw new ArgumentNullException(nameof(estadoSistema));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
    }

    public async Task<int> EjecutarAsync()
    {
        var estado = await _estadoSistema.ObtenerEstadoAsync();
        if (!estado.Esquema.EstaAlDia)
        {
            Console.Error.WriteLine("La base no está al día. Ejecute primero: Retail.Cli migrar");
            return CodigoSalida.Fallo;
        }

        Console.WriteLine($"Simulando el historial de demostración en la base '{_context.Database.GetDbConnection().Database}'...");

        var hoy = DateOnly.FromDateTime(TimeProvider.System.GetLocalNow().DateTime);
        var resultado = await SemilleroHistorialDemo.CargarAsync(_context, _hasher, hoy, TimeProvider.System.LocalTimeZone);

        Console.WriteLine($"Historial generado del {resultado.Desde:dd/MM/yyyy} al {resultado.Hasta:dd/MM/yyyy}:");
        Console.WriteLine($"  {resultado.Articulos} artículos, {resultado.Clientes} clientes y {resultado.Turnos} turnos de caja");
        Console.WriteLine($"  {resultado.Ventas} ventas, {resultado.Compras} compras y {resultado.Cobranzas} cobranzas");
        Console.WriteLine("Los cajeros de demostración no tienen una contraseña conocida: un Gerente la blanquea desde Usuarios.");
        return CodigoSalida.Exito;
    }
}

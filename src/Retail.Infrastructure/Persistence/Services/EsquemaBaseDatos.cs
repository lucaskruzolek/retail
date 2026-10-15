using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Sistema;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Persistence.Services;

/// <summary>
/// Implementación del puerto de esquema con la API de migraciones de EF Core (RF-23). Compara la tabla
/// __EFMigrationsHistory de la base con las migraciones compiladas en esta versión de la aplicación.
/// </summary>
public class EsquemaBaseDatos : IEsquemaBaseDatos
{
    private readonly RetailDbContext _context;

    public EsquemaBaseDatos(RetailDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<EstadoEsquemaDto> ObtenerEstadoAsync(CancellationToken cancellationToken = default)
    {
        // CanConnect devuelve false tanto si el servidor no responde como si la base todavía no existe.
        if (!await _context.Database.CanConnectAsync(cancellationToken))
        {
            return new EstadoEsquemaDto
            {
                PuedeConectar = false,
                MigracionesPendientes = _context.Database.GetMigrations().ToList(),
                MigracionesDesconocidas = []
            };
        }

        var conocidas = _context.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
        var aplicadas = (await _context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        var pendientes = await _context.Database.GetPendingMigrationsAsync(cancellationToken);

        return new EstadoEsquemaDto
        {
            PuedeConectar = true,
            MigracionesPendientes = pendientes.ToList(),
            MigracionesDesconocidas = aplicadas.Where(m => !conocidas.Contains(m)).ToList()
        };
    }

    public async Task<IReadOnlyList<string>> AplicarMigracionesPendientesAsync(CancellationToken cancellationToken = default)
    {
        var pendientes = (await _context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

        await _context.Database.MigrateAsync(cancellationToken);

        return pendientes;
    }

    public async Task<string?> ObtenerUltimaMigracionAplicadaAsync(CancellationToken cancellationToken = default)
    {
        // El historial de EF devuelve las migraciones en orden: su identificador empieza con la fecha de creación.
        var aplicadas = await _context.Database.GetAppliedMigrationsAsync(cancellationToken);
        return aplicadas.LastOrDefault();
    }

    public async Task<bool> ExisteHistorialDeBackupsAsync(CancellationToken cancellationToken = default)
    {
        var existe = await _context.Database
            .SqlQueryRaw<int>("SELECT CASE WHEN OBJECT_ID(N'dbo.REGISTROS_BACKUP', N'U') IS NULL THEN 0 ELSE 1 END AS [Value]")
            .SingleAsync(cancellationToken);

        return existe == 1;
    }
}

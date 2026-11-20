using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Caja;
using Retail.Application.Interfaces.Persistence;
using Retail.Domain.Enums;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Persistence.Services;

/// <summary>
/// Consultas de solo lectura sobre los turnos de caja abiertos: se proyectan a DTOs en SQL Server, sin Change Tracker
/// (Ley 8), uniendo el nombre del cajero y el de la terminal en la misma consulta.
/// </summary>
/// <remarks>
/// Usuarios y terminales se unen con <c>IgnoreQueryFilters</c>: un turno abierto debe mostrar a su cajero y su terminal
/// aunque alguno se haya dado de baja después; con el filtro de borrado lógico el turno desaparecería de la consulta
/// y la caja parecería libre. El filtro del propio turno sigue activo.
/// </remarks>
public class TurnoQueryService : ITurnoQueryService
{
    private readonly RetailDbContext _context;

    public TurnoQueryService(RetailDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<TurnoCajaDto?> ObtenerAbiertoDeTerminalAsync(int idTerminal, CancellationToken cancellationToken = default)
    {
        return await ProyectarAbiertos()
            .Where(t => t.IdTerminal == idTerminal)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TurnoCajaDto?> ObtenerAbiertoDeUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default)
    {
        return await ProyectarAbiertos()
            .Where(t => t.IdUsuario == idUsuario)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TurnoAbiertoDto>> ListarAbiertosAsync(CancellationToken cancellationToken = default)
    {
        return await (
                from turno in _context.TurnosCaja.AsNoTracking()
                join usuario in _context.Usuarios.IgnoreQueryFilters() on turno.IdUsuario equals usuario.Id
                join terminal in _context.Terminales.IgnoreQueryFilters() on turno.IdTerminal equals terminal.Id
                where turno.Estado == EstadoTurnoEnum.Abierto
                orderby terminal.Nombre, turno.Id
                select new TurnoAbiertoDto
                {
                    IdTurno = turno.Id,
                    IdTerminal = turno.IdTerminal,
                    NombreTerminal = terminal.Nombre,
                    IdUsuario = turno.IdUsuario,
                    NombreCajero = usuario.Nombre + " " + usuario.Apellido,
                    FechaApertura = turno.FechaApertura,
                    SaldoTeoricoEfectivo = turno.SaldoTeoricoEfectivo,
                    TotalVentasElectronicas = turno.TotalVentasElectronicas,
                    CantidadVentas = turno.CantidadVentas
                })
            .ToListAsync(cancellationToken);
    }

    private IQueryable<TurnoCajaDto> ProyectarAbiertos()
    {
        return from turno in _context.TurnosCaja.AsNoTracking()
               join usuario in _context.Usuarios.IgnoreQueryFilters() on turno.IdUsuario equals usuario.Id
               join terminal in _context.Terminales.IgnoreQueryFilters() on turno.IdTerminal equals terminal.Id
               where turno.Estado == EstadoTurnoEnum.Abierto
               select new TurnoCajaDto
               {
                   IdTurno = turno.Id,
                   IdUsuario = turno.IdUsuario,
                   NombreUsuario = usuario.Nombre + " " + usuario.Apellido,
                   IdTerminal = turno.IdTerminal,
                   NombreTerminal = terminal.Nombre,
                   FechaApertura = turno.FechaApertura,
                   SaldoInicial = turno.SaldoInicial,
                   FechaCierre = turno.FechaCierre,
                   TotalVentasEfectivo = turno.TotalVentasEfectivo,
                   TotalIngresosEfectivo = turno.TotalIngresosEfectivo,
                   TotalEgresosEfectivo = turno.TotalEgresosEfectivo,
                   SaldoTeoricoEfectivo = turno.SaldoTeoricoEfectivo,
                   SaldoDeclaradoEfectivo = turno.SaldoDeclaradoEfectivo,
                   DiferenciaEfectivo = turno.DiferenciaEfectivo,
                   TotalVentasElectronicas = turno.TotalVentasElectronicas,
                   MontoRetenidoEnCaja = turno.MontoRetenidoEnCaja,
                   CantidadVentas = turno.CantidadVentas,
                   Estado = turno.Estado
               };
    }
}

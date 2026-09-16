using Microsoft.Extensions.Logging;
using Retail.Application.DTOs.Caja;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;

namespace Retail.Infrastructure.ExternalServices.Mocks;

/// <summary>
/// Doble de prueba y contingencia de <see cref="ICajaService"/> para desarrollo local.
/// Cumple con la Ley 10 de AGENTS.md aislando el Módulo 3.2 de las tareas asignadas a Pablo (Módulo 3.1).
/// </summary>
public partial class FakeCajaService : ICajaService
{
    private readonly ILogger<FakeCajaService>? _logger;
    private readonly TurnoCajaDto _turnoSimulado;

    public FakeCajaService(ILogger<FakeCajaService>? logger = null)
    {
        _logger = logger;
        _turnoSimulado = new TurnoCajaDto
        {
            IdTurno = 1,
            IdUsuario = 1,
            NombreUsuario = "admin",
            FechaApertura = DateTime.UtcNow.Date.AddHours(8),
            SaldoInicial = 10000m,
            SaldoTeoricoEfectivo = 10000m,
            TotalVentasEfectivo = 0m,
            TotalIngresosEfectivo = 0m,
            TotalEgresosEfectivo = 0m,
            TotalVentasElectronicas = 0m,
            Estado = EstadoTurnoEnum.Abierto
        };
    }

    public Task<TurnoCajaDto?> ObtenerTurnoActivoAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<TurnoCajaDto?>(_turnoSimulado);
    }

    public Task<TurnoCajaDto> AbrirTurnoAsync(AperturaTurnoDto dto, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_turnoSimulado);
    }

    public Task<TurnoCajaDto> RegistrarMovimientoAsync(MovimientoCajaDto dto, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_turnoSimulado);
    }

    public Task<ResultadoArqueoDto> CerrarTurnoConArqueoCiegoAsync(ArqueoCiegoDto dto, CancellationToken cancellationToken = default)
    {
        var resultado = new ResultadoArqueoDto
        {
            IdTurno = _turnoSimulado.IdTurno,
            FechaCierre = DateTime.UtcNow,
            SaldoInicial = _turnoSimulado.SaldoInicial,
            TotalVentasEfectivo = _turnoSimulado.TotalVentasEfectivo,
            TotalIngresosEfectivo = _turnoSimulado.TotalIngresosEfectivo,
            TotalEgresosEfectivo = _turnoSimulado.TotalEgresosEfectivo,
            SaldoTeoricoEfectivo = _turnoSimulado.SaldoTeoricoEfectivo,
            SaldoDeclaradoEfectivo = dto.SaldoDeclaradoEfectivo,
            DiferenciaEfectivo = dto.SaldoDeclaradoEfectivo - _turnoSimulado.SaldoTeoricoEfectivo,
            TotalVentasElectronicas = _turnoSimulado.TotalVentasElectronicas,
            MontoRetenidoEnCaja = dto.MontoRetenidoEnCaja
        };

        return Task.FromResult(resultado);
    }

    public Task RegistrarIngresoCobranzaAsync(int idTurno, decimal monto, MedioPagoEnum medioPago, CancellationToken cancellationToken = default)
    {
        if (_logger != null)
        {
            LogIngresoCobranza(_logger, idTurno, monto, medioPago);
        }

        return Task.CompletedTask;
    }

    public Task RegistrarVentaEnCajaAsync(int idTurno, decimal totalEfectivo, decimal totalElectronico, CancellationToken cancellationToken = default)
    {
        if (_logger != null)
        {
            LogVenta(_logger, idTurno, totalEfectivo, totalElectronico);
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "FakeCajaService: Imputando cobranza en turno #{IdTurno}. Monto: {Monto} ({MedioPago}).")]
    private static partial void LogIngresoCobranza(ILogger logger, int idTurno, decimal monto, MedioPagoEnum medioPago);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "FakeCajaService: Registrando venta en turno #{IdTurno}. Efectivo: {Efectivo}, Electrónico: {Electronico}.")]
    private static partial void LogVenta(ILogger logger, int idTurno, decimal efectivo, decimal electronico);
}

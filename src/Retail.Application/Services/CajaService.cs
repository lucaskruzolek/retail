using FluentValidation;
using Retail.Application.DTOs.Caja;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

public class CajaService : ICajaService
{
    private readonly IRepository<TurnoCaja> _turnoRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<AperturaTurnoDto> _aperturaValidator;
    private readonly IValidator<MovimientoCajaDto> _movimientoValidator;
    private readonly IValidator<ArqueoCiegoDto> _arqueoValidator;

    public CajaService(
        IRepository<TurnoCaja> turnoRepository,
        IUnitOfWork unitOfWork,
        IValidator<AperturaTurnoDto> aperturaValidator,
        IValidator<MovimientoCajaDto> movimientoValidator,
        IValidator<ArqueoCiegoDto> arqueoValidator)
    {
        _turnoRepository = turnoRepository;
        _unitOfWork = unitOfWork;
        _aperturaValidator = aperturaValidator;
        _movimientoValidator = movimientoValidator;
        _arqueoValidator = arqueoValidator;
    }

    public async Task<TurnoCajaDto?> ObtenerTurnoActivoAsync(CancellationToken cancellationToken = default)
    {
        var turnosAbiertos = await _turnoRepository.FindAsync(t => t.Estado == EstadoTurnoEnum.Abierto, cancellationToken);
        
        var listaTurnos = turnosAbiertos as IList<TurnoCaja> ?? turnosAbiertos.ToList();
        if (listaTurnos.Count == 0) return null;

        return MapearATurnoDto(listaTurnos[0]);
    }

    public async Task<TurnoCajaDto> AbrirTurnoAsync(AperturaTurnoDto dto, CancellationToken cancellationToken = default)
    {
        await _aperturaValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var turnoActivo = await ObtenerTurnoActivoAsync(cancellationToken);
        if (turnoActivo != null)
        {
            throw new TurnoYaAbiertoException("Ya existe un turno de caja abierto en el sistema.");
        }

        // Asumimos terminal por defecto o ID de terminal según requerimiento
        var nuevoTurno = TurnoCaja.Abrir(dto.IdUsuario, dto.SaldoInicial);
        
        await _turnoRepository.AddAsync(nuevoTurno, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapearATurnoDto(nuevoTurno);
    }

    public async Task<TurnoCajaDto> RegistrarMovimientoAsync(MovimientoCajaDto dto, CancellationToken cancellationToken = default)
    {
        await _movimientoValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var turno = await _turnoRepository.GetByIdAsync(dto.IdTurno, cancellationToken)
            ?? throw new KeyNotFoundException($"No se encontró el turno de caja con ID {dto.IdTurno}.");

        turno.RegistrarMovimiento(dto.TipoMovimiento, dto.Monto, dto.Concepto);

        await _turnoRepository.UpdateAsync(turno, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapearATurnoDto(turno);
    }

    public async Task<ResultadoArqueoDto> CerrarTurnoConArqueoCiegoAsync(ArqueoCiegoDto dto, CancellationToken cancellationToken = default)
    {
        await _arqueoValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var turno = await _turnoRepository.GetByIdAsync(dto.IdTurno, cancellationToken)
            ?? throw new KeyNotFoundException($"No se encontró el turno de caja con ID {dto.IdTurno}.");

        turno.CerrarConArqueoCiego(dto.SaldoDeclaradoEfectivo, dto.MontoRetenidoEnCaja);

        await _turnoRepository.UpdateAsync(turno, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ResultadoArqueoDto
        {
            IdTurno = turno.Id,
            SaldoInicial = turno.SaldoInicial,
            TotalVentasEfectivo = turno.TotalVentasEfectivo,
            TotalIngresosEfectivo = turno.TotalIngresosEfectivo,
            TotalEgresosEfectivo = turno.TotalEgresosEfectivo,
            SaldoTeoricoEfectivo = turno.SaldoTeoricoEfectivo,
            SaldoDeclaradoEfectivo = turno.SaldoDeclaradoEfectivo ?? 0,
            DiferenciaEfectivo = turno.DiferenciaEfectivo ?? 0,
            TotalVentasElectronicas = turno.TotalVentasElectronicas,
            MontoRetenidoEnCaja = turno.MontoRetenidoEnCaja,
            FechaCierre = turno.FechaCierre ?? DateTime.UtcNow
        };
    }

    public async Task RegistrarIngresoCobranzaAsync(int idTurno, decimal monto, MedioPagoEnum medioPago, CancellationToken cancellationToken = default)
    {
        var turno = await _turnoRepository.GetByIdAsync(idTurno, cancellationToken)
            ?? throw new KeyNotFoundException($"No se encontró el turno de caja abierto con ID {idTurno}.");

        turno.ImputarCobranza(monto, medioPago);

        await _turnoRepository.UpdateAsync(turno, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RegistrarVentaEnCajaAsync(int idTurno, decimal totalEfectivo, decimal totalElectronico, CancellationToken cancellationToken = default)
    {
        var turno = await _turnoRepository.GetByIdAsync(idTurno, cancellationToken)
            ?? throw new KeyNotFoundException($"No se encontró el turno de caja abierto con ID {idTurno}.");

        turno.ImputarVenta(totalEfectivo, totalElectronico);

        await _turnoRepository.UpdateAsync(turno, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static TurnoCajaDto MapearATurnoDto(TurnoCaja turno)
    {
        return new TurnoCajaDto
        {
            IdTurno = turno.Id,
            IdUsuario = turno.IdUsuario,
            NombreUsuario = turno.Usuario?.NombreUsuario,
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
            Estado = turno.Estado
        };
    }
}
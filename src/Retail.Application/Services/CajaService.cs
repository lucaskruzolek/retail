using FluentValidation;
using Retail.Application.DTOs.Caja;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

public class CajaService : ICajaService
{
    // Nombres de los índices únicos filtrados de TURNOS_CAJA (MT-1, MT-9): el mensaje de SQL Server los nombra y así
    // el servicio sabe cuál de las dos reglas (una terminal / un cajero) violó un guardado concurrente.
    private const string IndiceTurnoPorTerminal = "UX_TURNOS_CAJA_AbiertoPorTerminal";
    private const string IndiceTurnoPorUsuario = "UX_TURNOS_CAJA_AbiertoPorUsuario";

    private readonly IRepository<TurnoCaja> _turnoRepository;
    private readonly IRepository<Usuario> _usuarioRepository;
    private readonly IRepository<Terminal> _terminalRepository;
    private readonly ITurnoQueryService _consultas;
    private readonly ITerminalLocal _terminalLocal;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<AperturaTurnoDto> _aperturaValidator;
    private readonly IValidator<MovimientoCajaDto> _movimientoValidator;
    private readonly IValidator<ArqueoCiegoDto> _arqueoValidator;

    public CajaService(
        IRepository<TurnoCaja> turnoRepository,
        IRepository<Usuario> usuarioRepository,
        IRepository<Terminal> terminalRepository,
        ITurnoQueryService consultas,
        ITerminalLocal terminalLocal,
        IUnitOfWork unitOfWork,
        IValidator<AperturaTurnoDto> aperturaValidator,
        IValidator<MovimientoCajaDto> movimientoValidator,
        IValidator<ArqueoCiegoDto> arqueoValidator)
    {
        _turnoRepository = turnoRepository;
        _usuarioRepository = usuarioRepository;
        _terminalRepository = terminalRepository;
        _consultas = consultas;
        _terminalLocal = terminalLocal;
        _unitOfWork = unitOfWork;
        _aperturaValidator = aperturaValidator;
        _movimientoValidator = movimientoValidator;
        _arqueoValidator = arqueoValidator;
    }

    /// <summary>
    /// El turno abierto DE ESTA TERMINAL (<c>null</c> si no tiene ninguno o si esta PC no está activada). Una sola
    /// consulta proyectada: trae también los nombres del cajero y de la terminal.
    /// </summary>
    public async Task<TurnoCajaDto?> ObtenerTurnoActivoAsync(CancellationToken cancellationToken = default)
    {
        if (_terminalLocal.IdTerminal is not int idTerminal)
        {
            return null;
        }

        return await _consultas.ObtenerAbiertoDeTerminalAsync(idTerminal, cancellationToken);
    }

    public async Task<TurnoCajaDto> AbrirTurnoAsync(AperturaTurnoDto dto, CancellationToken cancellationToken = default)
    {
        await _aperturaValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var idTerminal = _terminalLocal.IdTerminal
            ?? throw new DomainException("Esta PC no está activada como terminal. Pida a un Gerente que la active antes de abrir un turno.");

        var terminal = await _terminalRepository.GetByIdAsync(idTerminal, cancellationToken)
            ?? throw new DomainException("Esta PC no está activada como terminal: la terminal registrada ya no existe. Pida a un Gerente que la active de nuevo.");

        // Las comprobaciones previas dan un mensaje claro en el caso normal; los índices únicos filtrados son la
        // garantía final cuando dos terminales abren a la vez (MT-1, MT-9).
        var abiertoEnTerminal = await _consultas.ObtenerAbiertoDeTerminalAsync(idTerminal, cancellationToken);
        if (abiertoEnTerminal is not null)
        {
            throw new TurnoYaAbiertoException(MensajeTerminalOcupada(abiertoEnTerminal.NombreUsuario));
        }

        var abiertoDelUsuario = await _consultas.ObtenerAbiertoDeUsuarioAsync(dto.IdUsuario, cancellationToken);
        if (abiertoDelUsuario is not null)
        {
            throw new TurnoYaAbiertoException(MensajeUsuarioOcupado(abiertoDelUsuario.NombreTerminal));
        }

        var nuevoTurno = TurnoCaja.Abrir(dto.IdUsuario, idTerminal, dto.SaldoInicial);

        try
        {
            await _turnoRepository.AddAsync(nuevoTurno, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (RestriccionDeUnicidadException ex)
        {
            // El turno rechazado quedó en el ChangeTracker: se descarta para que no se guarde con la próxima operación.
            _unitOfWork.DescartarCambios();

            if (ex.CorrespondeAIndice(IndiceTurnoPorTerminal))
            {
                var ocupante = await _consultas.ObtenerAbiertoDeTerminalAsync(idTerminal, cancellationToken);
                throw new TurnoYaAbiertoException(MensajeTerminalOcupada(ocupante?.NombreUsuario));
            }

            if (ex.CorrespondeAIndice(IndiceTurnoPorUsuario))
            {
                var otro = await _consultas.ObtenerAbiertoDeUsuarioAsync(dto.IdUsuario, cancellationToken);
                throw new TurnoYaAbiertoException(MensajeUsuarioOcupado(otro?.NombreTerminal));
            }

            throw;
        }

        return await _consultas.ObtenerAbiertoDeTerminalAsync(idTerminal, cancellationToken)
            ?? MapearATurnoDto(nuevoTurno, terminal.Nombre);
    }

    public async Task<TurnoCajaDto> RegistrarMovimientoAsync(MovimientoCajaDto dto, CancellationToken cancellationToken = default)
    {
        await _movimientoValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var turno = await _turnoRepository.GetByIdAsync(dto.IdTurno, cancellationToken)
            ?? throw new KeyNotFoundException($"No se encontró el turno de caja con ID {dto.IdTurno}.");

        await ExigirDuenoDelTurnoAsync(turno, dto.IdUsuario, cancellationToken);

        turno.RegistrarMovimiento(dto.TipoMovimiento, dto.Monto, dto.Concepto);

        await _turnoRepository.UpdateAsync(turno, cancellationToken);
        await GuardarDiagnosticandoTurnoCerradoAsync(dto.IdTurno, cancellationToken);

        return MapearATurnoDto(turno, turno.Terminal?.Nombre);
    }

    public async Task<ResultadoArqueoDto> CerrarTurnoConArqueoCiegoAsync(ArqueoCiegoDto dto, CancellationToken cancellationToken = default)
    {
        await _arqueoValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var turno = await _turnoRepository.GetByIdAsync(dto.IdTurno, cancellationToken)
            ?? throw new KeyNotFoundException($"No se encontró el turno de caja con ID {dto.IdTurno}.");

        // Cierra el dueño del turno o un Gerente (MT-14). El dueño no necesita releerse; un tercero sí se autoriza
        // contra la base, porque ocultar el botón en la UI no es un control de acceso.
        if (!turno.EsDe(dto.IdUsuarioActor))
        {
            await AutorizacionGerente.ExigirAsync(
                _usuarioRepository,
                dto.IdUsuarioActor,
                "Solo el dueño del turno o un Gerente puede cerrarlo.",
                cancellationToken);
        }

        // Quien cierra decide con importes que leyó antes: si el turno siguió operando, hay que volver a leerlos.
        turno.ExigirSinCambiosDesde(dto.SaldoTeoricoLeido, dto.TotalElectronicoLeido, dto.CantidadVentasLeida);
        turno.CerrarConArqueoCiego(dto.SaldoDeclaradoEfectivo, dto.MontoRetenidoEnCaja);

        await _turnoRepository.UpdateAsync(turno, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictoDeConcurrenciaException ex)
        {
            // Otra terminal registró una venta, un movimiento o una cobranza después de que se cargó el turno: el
            // saldo teórico que vio el encargado ya no es el real, y un arqueo ciego contra un saldo viejo mentiría.
            throw new ConflictoDeConcurrenciaException(
                "Se registró una operación en el turno mientras se hacía el arqueo. Vuelva a abrir el cierre para ver el saldo actualizado.",
                ex);
        }

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

    public async Task<IReadOnlyList<TurnoAbiertoDto>> ListarTurnosAbiertosAsync(int idActor, CancellationToken cancellationToken = default)
    {
        await AutorizacionGerente.ExigirAsync(
            _usuarioRepository,
            idActor,
            "Solo un Gerente puede ver los turnos abiertos de todas las terminales.",
            cancellationToken);

        return await _consultas.ListarAbiertosAsync(cancellationToken);
    }

    public async Task RegistrarIngresoCobranzaAsync(int idTurno, decimal monto, MedioPagoEnum medioPago, CancellationToken cancellationToken = default)
    {
        var turno = await _turnoRepository.GetByIdAsync(idTurno, cancellationToken)
            ?? throw new KeyNotFoundException($"No se encontró el turno de caja abierto con ID {idTurno}.");

        turno.ImputarCobranza(monto, medioPago);

        await _turnoRepository.UpdateAsync(turno, cancellationToken);
        await GuardarDiagnosticandoTurnoCerradoAsync(idTurno, cancellationToken);
    }

    public async Task RegistrarVentaEnCajaAsync(int idTurno, decimal totalEfectivo, decimal totalElectronico, CancellationToken cancellationToken = default)
    {
        var turno = await _turnoRepository.GetByIdAsync(idTurno, cancellationToken)
            ?? throw new KeyNotFoundException($"No se encontró el turno de caja abierto con ID {idTurno}.");

        turno.ImputarVenta(totalEfectivo, totalElectronico);

        await _turnoRepository.UpdateAsync(turno, cancellationToken);
        await GuardarDiagnosticandoTurnoCerradoAsync(idTurno, cancellationToken);
    }

    /// <summary>
    /// Guarda los cambios y, si hay un conflicto de concurrencia, diagnostica si la causa es que otra terminal cerró
    /// el turno. La Unit of Work solo sabe que "algo" cambió, así que el servicio relee el turno (el ChangeTracker ya
    /// fue descartado) para dar un mensaje claro; si el turno sigue abierto, relanza el conflicto original.
    /// </summary>
    private async Task GuardarDiagnosticandoTurnoCerradoAsync(int idTurno, CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictoDeConcurrenciaException ex)
        {
            var turnoActual = await _turnoRepository.GetByIdAsync(idTurno, cancellationToken);
            if (turnoActual == null || turnoActual.Estado != EstadoTurnoEnum.Abierto)
            {
                throw new CajaCerradaException(
                    "El turno de caja fue cerrado desde otra terminal. La operación no se registró.",
                    ex);
            }

            throw;
        }
    }

    /// <summary>
    /// Solo el dueño de un turno lo opera (MT-14). El nombre del cajero se lee únicamente cuando hay que informar el rechazo.
    /// </summary>
    private async Task ExigirDuenoDelTurnoAsync(TurnoCaja turno, int idUsuario, CancellationToken cancellationToken)
    {
        if (turno.EsDe(idUsuario))
        {
            return;
        }

        var dueno = await _usuarioRepository.GetByIdAsync(turno.IdUsuario, includeDeleted: true, cancellationToken);
        throw new TurnoAjenoException(MensajesDeTurno.TurnoAjeno(dueno?.NombreCompleto));
    }

    private static string MensajeTerminalOcupada(string? nombreCajero)
    {
        var cajero = string.IsNullOrWhiteSpace(nombreCajero) ? "otro cajero" : nombreCajero;

        return $"Esta caja ya tiene un turno abierto de {cajero}. Ciérrelo antes de abrir otro.";
    }

    private static string MensajeUsuarioOcupado(string? nombreTerminal)
    {
        var terminal = string.IsNullOrWhiteSpace(nombreTerminal) ? "otra terminal" : $"la terminal «{nombreTerminal}»";

        return $"Usted ya tiene un turno abierto en {terminal}. Ciérrelo allí antes de abrir otro.";
    }

    private static TurnoCajaDto MapearATurnoDto(TurnoCaja turno, string? nombreTerminal)
    {
        return new TurnoCajaDto
        {
            IdTurno = turno.Id,
            IdUsuario = turno.IdUsuario,
            NombreUsuario = turno.Usuario?.NombreCompleto,
            IdTerminal = turno.IdTerminal,
            NombreTerminal = nombreTerminal,
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

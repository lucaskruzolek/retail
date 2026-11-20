using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.Application.DTOs.Caja;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Application.Validators.Caja;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Application.UnitTests.Services;

public class CajaServiceTests
{
    private const int IdTurno = 1;

    private readonly IRepository<TurnoCaja> _turnoRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CajaService _sut;
    private readonly TurnoCaja _turno;

    public CajaServiceTests()
    {
        _turnoRepository = Substitute.For<IRepository<TurnoCaja>>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _turno = TurnoCaja.Abrir(idUsuario: 2, saldoInicial: 5000m);
        _turno.Id = IdTurno;
        _turnoRepository.GetByIdAsync(IdTurno, Arg.Any<CancellationToken>()).Returns(_turno);

        _sut = new CajaService(
            _turnoRepository,
            _unitOfWork,
            new AperturaTurnoValidator(),
            new MovimientoCajaValidator(),
            new ArqueoCiegoValidator());
    }

    [Fact]
    public async Task CerrarTurnoConArqueoCiegoAsync_ConflictoDeConcurrencia_LanzaConflictoConMensajeClaro()
    {
        // Arrange
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new ConflictoDeConcurrenciaException());
        var dto = new ArqueoCiegoDto { IdTurno = IdTurno, SaldoDeclaradoEfectivo = 5000m };

        // Act
        var act = async () => await _sut.CerrarTurnoConArqueoCiegoAsync(dto);

        // Assert
        var excepcion = await act.Should().ThrowAsync<ConflictoDeConcurrenciaException>();
        excepcion.WithMessage("*mientras se hacía el arqueo*");
        excepcion.And.InnerException.Should().BeOfType<ConflictoDeConcurrenciaException>();
    }

    [Fact]
    public async Task RegistrarMovimientoAsync_ConflictoYTurnoCerradoEnOtraTerminal_LanzaCajaCerrada()
    {
        // Arrange: tras el conflicto, la relectura devuelve el turno ya cerrado por otra terminal.
        ConfigurarRelecturaConTurnoCerrado();
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new ConflictoDeConcurrenciaException());

        // Act
        var act = async () => await _sut.RegistrarMovimientoAsync(NuevoMovimiento());

        // Assert
        await act.Should().ThrowAsync<CajaCerradaException>().WithMessage("*cerrado desde otra terminal*");
    }

    [Fact]
    public async Task RegistrarMovimientoAsync_ConflictoConTurnoAunAbierto_RelanzaElConflictoOriginal()
    {
        // Arrange
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new ConflictoDeConcurrenciaException());

        // Act
        var act = async () => await _sut.RegistrarMovimientoAsync(NuevoMovimiento());

        // Assert
        await act.Should().ThrowAsync<ConflictoDeConcurrenciaException>();
    }

    [Fact]
    public async Task RegistrarIngresoCobranzaAsync_ConflictoYTurnoCerradoEnOtraTerminal_LanzaCajaCerrada()
    {
        // Arrange
        ConfigurarRelecturaConTurnoCerrado();
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new ConflictoDeConcurrenciaException());

        // Act
        var act = async () => await _sut.RegistrarIngresoCobranzaAsync(IdTurno, 100m, MedioPagoEnum.Efectivo);

        // Assert
        await act.Should().ThrowAsync<CajaCerradaException>().WithMessage("*cerrado desde otra terminal*");
    }

    [Fact]
    public async Task RegistrarIngresoCobranzaAsync_ConflictoConTurnoAunAbierto_RelanzaElConflictoOriginal()
    {
        // Arrange
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new ConflictoDeConcurrenciaException());

        // Act
        var act = async () => await _sut.RegistrarIngresoCobranzaAsync(IdTurno, 100m, MedioPagoEnum.Efectivo);

        // Assert
        await act.Should().ThrowAsync<ConflictoDeConcurrenciaException>();
    }

    /// <summary>
    /// La primera lectura devuelve el turno abierto; la relectura posterior al conflicto, el turno cerrado.
    /// </summary>
    private void ConfigurarRelecturaConTurnoCerrado()
    {
        var turnoCerrado = TurnoCaja.Abrir(2, 5000m);
        turnoCerrado.Id = IdTurno;
        turnoCerrado.CerrarConArqueoCiego(5000m, 0m);
        _turnoRepository.GetByIdAsync(IdTurno, Arg.Any<CancellationToken>()).Returns(_turno, turnoCerrado);
    }

    private static MovimientoCajaDto NuevoMovimiento()
    {
        return new MovimientoCajaDto
        {
            IdTurno = IdTurno,
            TipoMovimiento = TipoMovimientoCajaEnum.IngresoExtraordinario,
            Monto = 100m,
            Concepto = "Cambio"
        };
    }
}

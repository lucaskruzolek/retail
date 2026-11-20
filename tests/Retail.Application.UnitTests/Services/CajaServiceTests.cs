using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.Application.DTOs.Caja;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
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
    private const int IdTerminal = 5;
    private const int IdDueno = 2;
    private const int IdOtroCajero = 3;
    private const int IdGerente = 9;

    private readonly IRepository<TurnoCaja> _turnoRepository;
    private readonly IRepository<Usuario> _usuarioRepository;
    private readonly IRepository<Terminal> _terminalRepository;
    private readonly ITurnoQueryService _consultas;
    private readonly ITerminalLocal _terminalLocal;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CajaService _sut;
    private readonly TurnoCaja _turno;

    public CajaServiceTests()
    {
        _turnoRepository = Substitute.For<IRepository<TurnoCaja>>();
        _usuarioRepository = Substitute.For<IRepository<Usuario>>();
        _terminalRepository = Substitute.For<IRepository<Terminal>>();
        _consultas = Substitute.For<ITurnoQueryService>();
        _terminalLocal = Substitute.For<ITerminalLocal>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _turno = TurnoCaja.Abrir(idUsuario: IdDueno, idTerminal: IdTerminal, saldoInicial: 5000m);
        _turno.Id = IdTurno;
        _turnoRepository.GetByIdAsync(IdTurno, Arg.Any<CancellationToken>()).Returns(_turno);

        _terminalLocal.IdTerminal.Returns(IdTerminal);
        var terminal = Terminal.Crear("Mostrador 1", "CAJA-01");
        terminal.Id = IdTerminal;
        _terminalRepository.GetByIdAsync(IdTerminal, Arg.Any<CancellationToken>()).Returns(terminal);

        RegistrarUsuario(IdDueno, "Ana", "Pérez", RolUsuarioEnum.Cajero);
        RegistrarUsuario(IdOtroCajero, "Beto", "Gómez", RolUsuarioEnum.Cajero);
        RegistrarUsuario(IdGerente, "Gina", "Ruiz", RolUsuarioEnum.Gerente);

        _sut = new CajaService(
            _turnoRepository,
            _usuarioRepository,
            _terminalRepository,
            _consultas,
            _terminalLocal,
            _unitOfWork,
            new AperturaTurnoValidator(),
            new MovimientoCajaValidator(),
            new ArqueoCiegoValidator());
    }

    // --- Apertura -------------------------------------------------------------------------------------------

    [Fact]
    public async Task ObtenerTurnoActivoAsync_TerminalNoActivada_DevuelveNuloSinConsultar()
    {
        // Arrange
        _terminalLocal.IdTerminal.Returns((int?)null);

        // Act
        var turno = await _sut.ObtenerTurnoActivoAsync();

        // Assert
        turno.Should().BeNull();
        await _consultas.DidNotReceiveWithAnyArgs().ObtenerAbiertoDeTerminalAsync(default, default);
    }

    [Fact]
    public async Task ObtenerTurnoActivoAsync_TerminalActivada_ConsultaElTurnoDeEsaTerminal()
    {
        // Arrange
        var esperado = NuevoTurnoDto(IdTurno, IdDueno, "Ana Pérez");
        _consultas.ObtenerAbiertoDeTerminalAsync(IdTerminal, Arg.Any<CancellationToken>()).Returns(esperado);

        // Act
        var turno = await _sut.ObtenerTurnoActivoAsync();

        // Assert
        turno.Should().BeSameAs(esperado);
    }

    [Fact]
    public async Task AbrirTurnoAsync_TerminalNoActivada_LanzaDomainExceptionSinGuardar()
    {
        // Arrange
        _terminalLocal.IdTerminal.Returns((int?)null);

        // Act
        var act = async () => await _sut.AbrirTurnoAsync(NuevaApertura(IdDueno));

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*no está activada como terminal*");
        await _turnoRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task AbrirTurnoAsync_TerminalInexistenteEnLaBase_LanzaDomainException()
    {
        // Arrange: por ejemplo, tras restaurar una copia más vieja que la activación de esta PC.
        _terminalRepository.GetByIdAsync(IdTerminal, Arg.Any<CancellationToken>()).Returns((Terminal?)null);

        // Act
        var act = async () => await _sut.AbrirTurnoAsync(NuevaApertura(IdDueno));

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*ya no existe*");
    }

    [Fact]
    public async Task AbrirTurnoAsync_TerminalConTurnoAbierto_LanzaTurnoYaAbiertoConElNombreDelCajero()
    {
        // Arrange
        _consultas.ObtenerAbiertoDeTerminalAsync(IdTerminal, Arg.Any<CancellationToken>())
            .Returns(NuevoTurnoDto(7, IdOtroCajero, "Beto Gómez"));

        // Act
        var act = async () => await _sut.AbrirTurnoAsync(NuevaApertura(IdDueno));

        // Assert
        await act.Should().ThrowAsync<TurnoYaAbiertoException>()
            .WithMessage("Esta caja ya tiene un turno abierto de Beto Gómez. Ciérrelo antes de abrir otro.");
        await _turnoRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task AbrirTurnoAsync_CajeroConTurnoEnOtraTerminal_LanzaTurnoYaAbiertoConElNombreDeLaTerminal()
    {
        // Arrange
        _consultas.ObtenerAbiertoDeUsuarioAsync(IdDueno, Arg.Any<CancellationToken>())
            .Returns(NuevoTurnoDto(7, IdDueno, "Ana Pérez") with { NombreTerminal = "Mostrador 2" });

        // Act
        var act = async () => await _sut.AbrirTurnoAsync(NuevaApertura(IdDueno));

        // Assert
        await act.Should().ThrowAsync<TurnoYaAbiertoException>()
            .WithMessage("Usted ya tiene un turno abierto en la terminal «Mostrador 2». Ciérrelo allí antes de abrir otro.");
    }

    [Fact]
    public async Task AbrirTurnoAsync_DatosValidos_GuardaElTurnoEnLaTerminalLocal()
    {
        // Act
        await _sut.AbrirTurnoAsync(NuevaApertura(IdDueno));

        // Assert
        await _turnoRepository.Received(1).AddAsync(
            Arg.Is<TurnoCaja>(t => t.IdUsuario == IdDueno && t.IdTerminal == IdTerminal && t.SaldoInicial == 1000m),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AbrirTurnoAsync_IndiceDeTerminalViolado_TraduceAlMensajeDeCajaOcupadaYDescarta()
    {
        // Arrange: otra PC ganó la carrera entre la comprobación previa y el guardado.
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new RestriccionDeUnicidadException("Cannot insert duplicate key ... 'UX_TURNOS_CAJA_AbiertoPorTerminal'"));
        _consultas.ObtenerAbiertoDeTerminalAsync(IdTerminal, Arg.Any<CancellationToken>())
            .Returns(null, NuevoTurnoDto(7, IdOtroCajero, "Beto Gómez"));

        // Act
        var act = async () => await _sut.AbrirTurnoAsync(NuevaApertura(IdDueno));

        // Assert
        await act.Should().ThrowAsync<TurnoYaAbiertoException>().WithMessage("*turno abierto de Beto Gómez*");
        _unitOfWork.Received(1).DescartarCambios();
    }

    [Fact]
    public async Task AbrirTurnoAsync_IndiceDeUsuarioViolado_TraduceAlMensajeDeCajeroOcupado()
    {
        // Arrange
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new RestriccionDeUnicidadException("Cannot insert duplicate key ... 'UX_TURNOS_CAJA_AbiertoPorUsuario'"));
        _consultas.ObtenerAbiertoDeUsuarioAsync(IdDueno, Arg.Any<CancellationToken>())
            .Returns(null, NuevoTurnoDto(7, IdDueno, "Ana Pérez") with { NombreTerminal = "Mostrador 2" });

        // Act
        var act = async () => await _sut.AbrirTurnoAsync(NuevaApertura(IdDueno));

        // Assert
        await act.Should().ThrowAsync<TurnoYaAbiertoException>().WithMessage("*terminal «Mostrador 2»*");
        _unitOfWork.Received(1).DescartarCambios();
    }

    [Fact]
    public async Task AbrirTurnoAsync_UnicidadDeOtroIndice_RelanzaLaRestriccion()
    {
        // Arrange
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new RestriccionDeUnicidadException("otro índice"));

        // Act
        var act = async () => await _sut.AbrirTurnoAsync(NuevaApertura(IdDueno));

        // Assert
        await act.Should().ThrowAsync<RestriccionDeUnicidadException>();
    }

    // --- Movimientos ----------------------------------------------------------------------------------------

    [Fact]
    public async Task RegistrarMovimientoAsync_UsuarioQueNoEsElDueno_LanzaTurnoAjenoSinGuardar()
    {
        // Act
        var act = async () => await _sut.RegistrarMovimientoAsync(NuevoMovimiento(IdOtroCajero));

        // Assert
        await act.Should().ThrowAsync<TurnoAjenoException>().WithMessage("*Ana Pérez*Solo esa persona puede operarlo*");
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        _turno.SaldoTeoricoEfectivo.Should().Be(5000m);
    }

    [Fact]
    public async Task RegistrarMovimientoAsync_UnGerenteQueNoEsElDueno_LanzaTurnoAjeno()
    {
        // Act: un Gerente puede cerrar el turno ajeno, pero no operarlo (MT-14).
        var act = async () => await _sut.RegistrarMovimientoAsync(NuevoMovimiento(IdGerente));

        // Assert
        await act.Should().ThrowAsync<TurnoAjenoException>();
    }

    [Fact]
    public async Task RegistrarMovimientoAsync_Dueno_ActualizaElSaldo()
    {
        // Act
        await _sut.RegistrarMovimientoAsync(NuevoMovimiento(IdDueno));

        // Assert
        _turno.SaldoTeoricoEfectivo.Should().Be(5100m);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // --- Cierre ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task CerrarTurnoConArqueoCiegoAsync_Dueno_CierraSinConsultarAutorizacion()
    {
        // Act
        var resultado = await _sut.CerrarTurnoConArqueoCiegoAsync(NuevoArqueo(IdDueno));

        // Assert
        resultado.IdTurno.Should().Be(IdTurno);
        _turno.Estado.Should().Be(EstadoTurnoEnum.Cerrado);
        await _usuarioRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default, default);
    }

    [Fact]
    public async Task CerrarTurnoConArqueoCiegoAsync_GerenteSobreTurnoAjeno_CierraElTurno()
    {
        // Act
        await _sut.CerrarTurnoConArqueoCiegoAsync(NuevoArqueo(IdGerente));

        // Assert
        _turno.Estado.Should().Be(EstadoTurnoEnum.Cerrado);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CerrarTurnoConArqueoCiegoAsync_CajeroQueNoEsElDueno_LanzaOperacionNoAutorizada()
    {
        // Act
        var act = async () => await _sut.CerrarTurnoConArqueoCiegoAsync(NuevoArqueo(IdOtroCajero));

        // Assert
        await act.Should().ThrowAsync<OperacionNoAutorizadaException>();
        _turno.Estado.Should().Be(EstadoTurnoEnum.Abierto);
    }

    [Fact]
    public async Task CerrarTurnoConArqueoCiegoAsync_ActorInexistente_LanzaOperacionNoAutorizada()
    {
        // Act
        var act = async () => await _sut.CerrarTurnoConArqueoCiegoAsync(NuevoArqueo(idActor: 404));

        // Assert
        await act.Should().ThrowAsync<OperacionNoAutorizadaException>().WithMessage("*inexistente o dado de baja*");
    }

    [Fact]
    public async Task CerrarTurnoConArqueoCiegoAsync_TurnoQueOperoDespuesDeLeerlo_LanzaTurnoModificadoSinCerrar()
    {
        // Arrange: el Gerente leyó el saldo 5000, pero el turno vendió después.
        _turno.ImputarVenta(300m, 0m);

        // Act
        var act = async () => await _sut.CerrarTurnoConArqueoCiegoAsync(NuevoArqueo(IdGerente));

        // Assert
        await act.Should().ThrowAsync<TurnoModificadoException>();
        _turno.Estado.Should().Be(EstadoTurnoEnum.Abierto);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task CerrarTurnoConArqueoCiegoAsync_ConflictoDeConcurrencia_LanzaConflictoConMensajeClaro()
    {
        // Arrange
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new ConflictoDeConcurrenciaException());

        // Act
        var act = async () => await _sut.CerrarTurnoConArqueoCiegoAsync(NuevoArqueo(IdDueno));

        // Assert
        var excepcion = await act.Should().ThrowAsync<ConflictoDeConcurrenciaException>();
        excepcion.WithMessage("*mientras se hacía el arqueo*");
        excepcion.And.InnerException.Should().BeOfType<ConflictoDeConcurrenciaException>();
    }

    // --- Turnos abiertos ------------------------------------------------------------------------------------

    [Fact]
    public async Task ListarTurnosAbiertosAsync_Gerente_DevuelveLosTurnosDeLaConsulta()
    {
        // Arrange
        IReadOnlyList<TurnoAbiertoDto> turnos =
        [
            new TurnoAbiertoDto
            {
                IdTurno = IdTurno,
                IdTerminal = IdTerminal,
                NombreTerminal = "Mostrador 1",
                IdUsuario = IdDueno,
                NombreCajero = "Ana Pérez",
                FechaApertura = DateTime.UtcNow,
                SaldoTeoricoEfectivo = 5000m,
                TotalVentasElectronicas = 0m,
                CantidadVentas = 0
            }
        ];
        _consultas.ListarAbiertosAsync(Arg.Any<CancellationToken>()).Returns(turnos);

        // Act
        var resultado = await _sut.ListarTurnosAbiertosAsync(IdGerente);

        // Assert
        resultado.Should().BeSameAs(turnos);
    }

    [Fact]
    public async Task ListarTurnosAbiertosAsync_Cajero_LanzaOperacionNoAutorizadaSinConsultar()
    {
        // Act
        var act = async () => await _sut.ListarTurnosAbiertosAsync(IdDueno);

        // Assert
        await act.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _consultas.DidNotReceiveWithAnyArgs().ListarAbiertosAsync(default);
    }

    // --- Conflictos al registrar operaciones ----------------------------------------------------------------

    [Fact]
    public async Task RegistrarMovimientoAsync_ConflictoYTurnoCerradoEnOtraTerminal_LanzaCajaCerrada()
    {
        // Arrange: tras el conflicto, la relectura devuelve el turno ya cerrado por otra terminal.
        ConfigurarRelecturaConTurnoCerrado();
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new ConflictoDeConcurrenciaException());

        // Act
        var act = async () => await _sut.RegistrarMovimientoAsync(NuevoMovimiento(IdDueno));

        // Assert
        await act.Should().ThrowAsync<CajaCerradaException>().WithMessage("*cerrado desde otra terminal*");
    }

    [Fact]
    public async Task RegistrarMovimientoAsync_ConflictoConTurnoAunAbierto_RelanzaElConflictoOriginal()
    {
        // Arrange
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new ConflictoDeConcurrenciaException());

        // Act
        var act = async () => await _sut.RegistrarMovimientoAsync(NuevoMovimiento(IdDueno));

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

    private void RegistrarUsuario(int id, string nombre, string apellido, RolUsuarioEnum rol)
    {
        var usuario = Usuario.Crear($"usuario.{id}", nombre, apellido, "hash", rol, rol == RolUsuarioEnum.Gerente ? "gerente@example.com" : null);
        usuario.Id = id;
        _usuarioRepository.GetByIdAsync(id, includeDeleted: false, Arg.Any<CancellationToken>()).Returns(usuario);
        _usuarioRepository.GetByIdAsync(id, includeDeleted: true, Arg.Any<CancellationToken>()).Returns(usuario);
    }

    /// <summary>
    /// La primera lectura devuelve el turno abierto; la relectura posterior al conflicto, el turno cerrado.
    /// </summary>
    private void ConfigurarRelecturaConTurnoCerrado()
    {
        var turnoCerrado = TurnoCaja.Abrir(IdDueno, IdTerminal, 5000m);
        turnoCerrado.Id = IdTurno;
        turnoCerrado.CerrarConArqueoCiego(5000m, 0m);
        _turnoRepository.GetByIdAsync(IdTurno, Arg.Any<CancellationToken>()).Returns(_turno, turnoCerrado);
    }

    private static AperturaTurnoDto NuevaApertura(int idUsuario)
    {
        return new AperturaTurnoDto { IdUsuario = idUsuario, SaldoInicial = 1000m };
    }

    private static TurnoCajaDto NuevoTurnoDto(int idTurno, int idUsuario, string nombreUsuario)
    {
        return new TurnoCajaDto
        {
            IdTurno = idTurno,
            IdUsuario = idUsuario,
            NombreUsuario = nombreUsuario,
            IdTerminal = IdTerminal,
            NombreTerminal = "Mostrador 1",
            FechaApertura = DateTime.UtcNow,
            SaldoInicial = 5000m,
            Estado = EstadoTurnoEnum.Abierto
        };
    }

    private static MovimientoCajaDto NuevoMovimiento(int idUsuario)
    {
        return new MovimientoCajaDto
        {
            IdTurno = IdTurno,
            IdUsuario = idUsuario,
            TipoMovimiento = TipoMovimientoCajaEnum.IngresoExtraordinario,
            Monto = 100m,
            Concepto = "Cambio"
        };
    }

    /// <summary>
    /// Cierre de quien leyó el turno recién abierto: saldo teórico 5000, sin ventas electrónicas ni ventas.
    /// </summary>
    private static ArqueoCiegoDto NuevoArqueo(int idActor)
    {
        return new ArqueoCiegoDto
        {
            IdTurno = IdTurno,
            IdUsuarioActor = idActor,
            SaldoDeclaradoEfectivo = 5000m,
            SaldoTeoricoLeido = 5000m,
            TotalElectronicoLeido = 0m,
            CantidadVentasLeida = 0
        };
    }
}

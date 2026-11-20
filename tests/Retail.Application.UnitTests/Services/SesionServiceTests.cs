using FluentAssertions;
using FluentValidation;
using NSubstitute;
using Retail.Application.DTOs.Sesiones;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Application.Validators.Sesiones;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Application.UnitTests.Services;

public class SesionServiceTests
{
    private const int IdGerente = 1;
    private const int IdCajero = 2;
    private const int IdAdministrador = 3;
    private const int IdTerminal = 7;

    private readonly ISesionesActivas _sesiones = Substitute.For<ISesionesActivas>();
    private readonly IRepository<Usuario> _usuarios = Substitute.For<IRepository<Usuario>>();
    private readonly SesionService _sut;

    public SesionServiceTests()
    {
        RegistrarUsuario(IdGerente, RolUsuarioEnum.Gerente);
        RegistrarUsuario(IdCajero, RolUsuarioEnum.Cajero);
        RegistrarUsuario(IdAdministrador, RolUsuarioEnum.Administrador);

        _sut = new SesionService(_sesiones, _usuarios, new ProgramarMantenimientoValidator());
    }

    // --- Iniciar / renovar / cerrar -------------------------------------------------------------------------

    [Fact]
    public async Task IniciarAsync_ConIdsValidos_DelegaEnElPuertoYDevuelveSuResultado()
    {
        // Arrange
        var esperado = new ResultadoInicioSesionDto { Iniciada = true };
        _sesiones.IniciarAsync(IdCajero, IdTerminal, Arg.Any<CancellationToken>()).Returns(esperado);

        // Act
        var resultado = await _sut.IniciarAsync(IdCajero, IdTerminal);

        // Assert
        resultado.Should().BeSameAs(esperado);
    }

    [Theory]
    [InlineData(0, IdTerminal)]
    [InlineData(-1, IdTerminal)]
    [InlineData(IdCajero, 0)]
    [InlineData(IdCajero, -3)]
    public async Task IniciarRenovarYCerrar_ConIdNoPositivo_LanzanArgumentOutOfRange(int idUsuario, int idTerminal)
    {
        // Act
        var iniciar = () => _sut.IniciarAsync(idUsuario, idTerminal);
        var renovar = () => _sut.RenovarAsync(idUsuario, idTerminal);
        var cerrar = () => _sut.CerrarAsync(idUsuario, idTerminal);

        // Assert
        await iniciar.Should().ThrowAsync<ArgumentOutOfRangeException>();
        await renovar.Should().ThrowAsync<ArgumentOutOfRangeException>();
        await cerrar.Should().ThrowAsync<ArgumentOutOfRangeException>();
        await _sesiones.DidNotReceiveWithAnyArgs().IniciarAsync(default, default, default);
    }

    [Fact]
    public async Task RenovarAsync_ConIdsValidos_DelegaEnElPuerto()
    {
        // Arrange
        var esperado = new EstadoLatidoDto { SesionVigente = true, AhoraServidorUtc = DateTime.UtcNow };
        _sesiones.RenovarAsync(IdCajero, IdTerminal, Arg.Any<CancellationToken>()).Returns(esperado);

        // Act
        var estado = await _sut.RenovarAsync(IdCajero, IdTerminal);

        // Assert
        estado.Should().BeSameAs(esperado);
    }

    [Fact]
    public async Task CerrarAsync_ConIdsValidos_DelegaEnElPuerto()
    {
        // Act
        await _sut.CerrarAsync(IdCajero, IdTerminal);

        // Assert
        await _sesiones.Received(1).CerrarAsync(IdCajero, IdTerminal, Arg.Any<CancellationToken>());
    }

    // --- Listar ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task ListarAsync_ComoGerente_DevuelveLasSesionesDelPuerto()
    {
        // Arrange
        IReadOnlyList<SesionActivaDto> sesiones = [];
        _sesiones.ListarAsync(Arg.Any<CancellationToken>()).Returns(sesiones);

        // Act
        var resultado = await _sut.ListarAsync(IdGerente);

        // Assert
        resultado.Should().BeSameAs(sesiones);
    }

    [Fact]
    public async Task ListarAsync_ComoCajero_LanzaOperacionNoAutorizada()
    {
        // Act
        var act = () => _sut.ListarAsync(IdCajero);

        // Assert
        await act.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _sesiones.DidNotReceiveWithAnyArgs().ListarAsync(default);
    }

    // --- ForzarCierre ---------------------------------------------------------------------------------------

    [Fact]
    public async Task ForzarCierreAsync_GerenteSobreOtroUsuario_CierraSuSesionYDevuelveElResultado()
    {
        // Arrange
        _sesiones.ForzarCierreAsync(IdCajero, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var habia = await _sut.ForzarCierreAsync(IdGerente, IdCajero);

        // Assert
        habia.Should().BeTrue();
    }

    [Fact]
    public async Task ForzarCierreAsync_GerenteSobreSuPropiaSesion_LanzaDomainException()
    {
        // Act
        var act = () => _sut.ForzarCierreAsync(IdGerente, IdGerente);

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*propia sesión*");
        await _sesiones.DidNotReceiveWithAnyArgs().ForzarCierreAsync(default, default);
    }

    [Fact]
    public async Task ForzarCierreAsync_ComoCajero_LanzaOperacionNoAutorizada()
    {
        // Act
        var act = () => _sut.ForzarCierreAsync(IdCajero, IdGerente);

        // Assert
        await act.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _sesiones.DidNotReceiveWithAnyArgs().ForzarCierreAsync(default, default);
    }

    // --- Mantenimiento --------------------------------------------------------------------------------------

    [Fact]
    public async Task ProgramarMantenimientoAsync_GerenteConDatosValidos_ProgramaConElMotivoRecortado()
    {
        // Act
        await _sut.ProgramarMantenimientoAsync(IdGerente, 15, "  Restaurar la base  ");

        // Assert
        await _sesiones.Received(1).ProgramarMantenimientoAsync(15, "Restaurar la base", IdGerente, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProgramarMantenimientoAsync_MotivoEnBlanco_LoGuardaComoNulo()
    {
        // Act
        await _sut.ProgramarMantenimientoAsync(IdGerente, 0, "   ");

        // Assert
        await _sesiones.Received(1).ProgramarMantenimientoAsync(0, null, IdGerente, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(61)]
    public async Task ProgramarMantenimientoAsync_MinutosFueraDeRango_LanzaValidationException(int minutos)
    {
        // Act
        var act = () => _sut.ProgramarMantenimientoAsync(IdGerente, minutos, null);

        // Assert
        await act.Should().ThrowAsync<ValidationException>().WithMessage("*entre 0 y 60 minutos*");
        await _sesiones.DidNotReceiveWithAnyArgs().ProgramarMantenimientoAsync(default, default, default, default);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60)]
    public async Task ProgramarMantenimientoAsync_MinutosEnLosLimites_LosAcepta(int minutos)
    {
        // Act
        await _sut.ProgramarMantenimientoAsync(IdGerente, minutos, null);

        // Assert
        await _sesiones.Received(1).ProgramarMantenimientoAsync(minutos, null, IdGerente, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProgramarMantenimientoAsync_MotivoDeMasDe200Caracteres_LanzaValidationException()
    {
        // Act
        var act = () => _sut.ProgramarMantenimientoAsync(IdGerente, 10, new string('x', 201));

        // Assert
        await act.Should().ThrowAsync<ValidationException>().WithMessage("*200 caracteres*");
    }

    [Fact]
    public async Task ProgramarMantenimientoAsync_ComoCajero_LanzaOperacionNoAutorizadaSinProgramar()
    {
        // Act
        var act = () => _sut.ProgramarMantenimientoAsync(IdCajero, 10, null);

        // Assert
        await act.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _sesiones.DidNotReceiveWithAnyArgs().ProgramarMantenimientoAsync(default, default, default, default);
    }

    [Fact]
    public async Task CancelarMantenimientoAsync_ComoGerente_CancelaEnElPuerto()
    {
        // Act
        await _sut.CancelarMantenimientoAsync(IdGerente);

        // Assert
        await _sesiones.Received(1).CancelarMantenimientoAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProgramarYCancelarMantenimiento_ComoAdministrador_LoPermite()
    {
        // El Administrador restaura con autorización de un Gerente (control dual): también prepara el mantenimiento.
        await _sut.ProgramarMantenimientoAsync(IdAdministrador, 2, "Restauración");
        await _sut.CancelarMantenimientoAsync(IdAdministrador);

        await _sesiones.Received(1).ProgramarMantenimientoAsync(2, "Restauración", IdAdministrador, Arg.Any<CancellationToken>());
        await _sesiones.Received(1).CancelarMantenimientoAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListarYForzarCierre_ComoAdministrador_LanzaOperacionNoAutorizada()
    {
        // Ver y cerrar sesiones ajenas sigue siendo solo del Gerente.
        var listar = () => _sut.ListarAsync(IdAdministrador);
        var forzar = () => _sut.ForzarCierreAsync(IdAdministrador, IdCajero);

        await listar.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await forzar.Should().ThrowAsync<OperacionNoAutorizadaException>();
    }

    [Fact]
    public async Task CancelarMantenimientoAsync_ComoCajero_LanzaOperacionNoAutorizada()
    {
        // Act
        var act = () => _sut.CancelarMantenimientoAsync(IdCajero);

        // Assert
        await act.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _sesiones.DidNotReceiveWithAnyArgs().CancelarMantenimientoAsync(default);
    }

    [Fact]
    public async Task ProgramarMantenimientoAsync_ActorInexistente_LanzaOperacionNoAutorizada()
    {
        // Act
        var act = () => _sut.ProgramarMantenimientoAsync(99, 10, null);

        // Assert
        await act.Should().ThrowAsync<OperacionNoAutorizadaException>();
    }

    // --- Parámetros -----------------------------------------------------------------------------------------

    [Fact]
    public void ParametrosSesion_VencimientoEsTresVecesElIntervaloDeLatido()
    {
        // Assert
        ParametrosSesion.IntervaloLatido.Should().Be(TimeSpan.FromSeconds(60));
        ParametrosSesion.Vencimiento.Should().Be(TimeSpan.FromMinutes(3));
    }

    private void RegistrarUsuario(int id, RolUsuarioEnum rol)
    {
        var usuario = Usuario.Crear($"usuario.{id}", "Nombre", "Apellido", "hash", rol, rol == RolUsuarioEnum.Gerente ? "gerente@example.com" : null);
        usuario.Id = id;
        _usuarios.GetByIdAsync(id, includeDeleted: false, Arg.Any<CancellationToken>()).Returns(usuario);
    }
}

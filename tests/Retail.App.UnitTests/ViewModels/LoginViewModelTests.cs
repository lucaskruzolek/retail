using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Services;
using Retail.App.ViewModels.Auth;
using Retail.Application.DTOs.Auth;
using Retail.Application.DTOs.Usuarios;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class LoginViewModelTests
{
    private readonly IAuthService _authService;
    private readonly IUsuarioService _usuarioService;
    private readonly IUsuarioDialogService _dialogService;
    private readonly ICurrentUserSession _session;
    private readonly ILogger<LoginViewModel> _logger;
    private readonly LoginViewModel _sut;

    public LoginViewModelTests()
    {
        _authService = Substitute.For<IAuthService>();
        _usuarioService = Substitute.For<IUsuarioService>();
        _dialogService = Substitute.For<IUsuarioDialogService>();
        _session = Substitute.For<ICurrentUserSession>();
        _logger = Substitute.For<ILogger<LoginViewModel>>();

        _sut = new LoginViewModel(_authService, _usuarioService, _dialogService, _session, _logger);
    }

    [Fact]
    public async Task IniciarSesionCommand_CredencialesValidas_EstableceSesionYDisparaEvento()
    {
        // Arrange
        _sut.NombreUsuario = "admin";
        var password = "AdminPassword123!";

        var resultadoDto = new LoginResultDto
        {
            IdUsuario = 1,
            NombreUsuario = "admin",
            NombreCompleto = "Administrador",
            Rol = RolUsuarioEnum.Gerente
        };

        _authService.LoginAsync(Arg.Is<LoginRequestDto>(r => r.NombreUsuario == "admin" && r.Password == password))
            .Returns(resultadoDto);

        var eventoDisparado = false;
        _sut.LoginExitoso += () => eventoDisparado = true;

        // Act
        await _sut.IniciarSesionCommand.ExecuteAsync(password);

        // Assert
        _sut.TieneError.Should().BeFalse();
        _sut.MensajeError.Should().BeEmpty();
        _sut.EstaCargando.Should().BeFalse();
        _session.Received(1).EstablecerSesion(resultadoDto);
        eventoDisparado.Should().BeTrue();
    }

    [Fact]
    public async Task IniciarSesionCommand_CredencialesInvalidas_ActivaTieneErrorConMensaje()
    {
        // Arrange
        _sut.NombreUsuario = "admin";
        var password = "PasswordErronea!";

        _authService.LoginAsync(Arg.Any<LoginRequestDto>())
            .ThrowsAsync(new CredencialesInvalidasException("Nombre de usuario o contraseña incorrectos."));

        var eventoDisparado = false;
        _sut.LoginExitoso += () => eventoDisparado = true;

        // Act
        await _sut.IniciarSesionCommand.ExecuteAsync(password);

        // Assert
        _sut.TieneError.Should().BeTrue();
        _sut.MensajeError.Should().Be("Nombre de usuario o contraseña incorrectos.");
        _sut.EstaCargando.Should().BeFalse();
        _session.DidNotReceive().EstablecerSesion(Arg.Any<LoginResultDto>());
        eventoDisparado.Should().BeFalse();
    }

    [Fact]
    public async Task IniciarSesionCommand_CuentaBloqueada_MuestraElMensajeDelBloqueo()
    {
        // Arrange
        _sut.NombreUsuario = "admin";
        var excepcion = new CuentaBloqueadaException();
        _authService.LoginAsync(Arg.Any<LoginRequestDto>()).ThrowsAsync(excepcion);

        // Act
        await _sut.IniciarSesionCommand.ExecuteAsync("Password123!");

        // Assert
        _sut.TieneError.Should().BeTrue();
        _sut.MensajeError.Should().Be(excepcion.Message);
        _sut.EstaCargando.Should().BeFalse();
        _session.DidNotReceive().EstablecerSesion(Arg.Any<LoginResultDto>());
    }

    [Fact]
    public async Task IniciarSesionCommand_UsuarioInactivo_ActivaTieneErrorConMensaje()
    {
        // Arrange
        _sut.NombreUsuario = "despedido";
        var password = "Password123!";

        _authService.LoginAsync(Arg.Any<LoginRequestDto>())
            .ThrowsAsync(new UsuarioInactivoException("despedido"));

        // Act
        await _sut.IniciarSesionCommand.ExecuteAsync(password);

        // Assert
        _sut.TieneError.Should().BeTrue();
        _sut.MensajeError.Should().Contain("despedido");
        _sut.EstaCargando.Should().BeFalse();
        _session.DidNotReceive().EstablecerSesion(Arg.Any<LoginResultDto>());
    }

    [Fact]
    public async Task IniciarSesionCommand_ExcepcionInesperada_MuestraMensajeGenerico()
    {
        // Arrange
        _sut.NombreUsuario = "admin";
        _authService.LoginAsync(Arg.Any<LoginRequestDto>())
            .ThrowsAsync(new InvalidOperationException("Fallo de red"));

        // Act
        await _sut.IniciarSesionCommand.ExecuteAsync("password");

        // Assert
        _sut.TieneError.Should().BeTrue();
        _sut.MensajeError.Should().Be("Ocurrió un error inesperado al intentar iniciar sesión.");
        _sut.EstaCargando.Should().BeFalse();
    }

    [Fact]
    public async Task IniciarSesionCommand_CuandoEstaCargando_IgnoraInvocacionesConcurrentes()
    {
        // Arrange
        _sut.NombreUsuario = "admin";
        var tcs = new TaskCompletionSource<LoginResultDto>();
        _authService.LoginAsync(Arg.Any<LoginRequestDto>()).Returns(tcs.Task);

        // Act: Primera invocación (inicia operación asíncrona)
        var primeraInvocacion = _sut.IniciarSesionCommand.ExecuteAsync("password123");
        _sut.EstaCargando.Should().BeTrue();

        // Segunda invocación mientras sigue cargando
        var segundaInvocacion = _sut.IniciarSesionCommand.ExecuteAsync("password123");

        // Completar la operación en curso
        tcs.SetResult(new LoginResultDto
        {
            IdUsuario = 1,
            NombreUsuario = "admin",
            NombreCompleto = "Administrador",
            Rol = RolUsuarioEnum.Gerente
        });

        await Task.WhenAll(primeraInvocacion, segundaInvocacion);

        // Assert: El servicio de autenticación solo debe haberse invocado exactamente 1 vez
        await _authService.Received(1).LoginAsync(Arg.Any<LoginRequestDto>());
    }

    private static LoginResultDto CuentaConCambioObligatorio() => new()
    {
        IdUsuario = 7,
        NombreUsuario = "cajero",
        NombreCompleto = "Carla Díaz",
        Rol = RolUsuarioEnum.Cajero,
        DebeCambiarPassword = true
    };

    [Fact]
    public async Task IniciarSesionCommand_CuentaConCambioObligatorioYCambioRealizado_CambiaComoTitularYEstableceSesion()
    {
        // Arrange
        _sut.NombreUsuario = "cajero";
        _authService.LoginAsync(Arg.Any<LoginRequestDto>(), Arg.Any<CancellationToken>()).Returns(CuentaConCambioObligatorio());

        Func<CambiarPasswordDto, Task>? guardar = null;
        _dialogService.MostrarDialogoCambioObligatorio(7, "cajero", "Carla Díaz", Arg.Do<Func<CambiarPasswordDto, Task>>(cb => guardar = cb))
            .Returns(true);

        // Act
        await _sut.IniciarSesionCommand.ExecuteAsync("Temporal2026!");
        await guardar!(new CambiarPasswordDto { IdUsuario = 7, NuevaPassword = "MiClave2026!" });

        // Assert: el actor del cambio es el propio titular (cambio propio, no blanqueo).
        // La contraseña actual que valida el servicio es la que el titular acaba de escribir en el login.
        await _usuarioService.Received(1).CambiarPasswordAsync(
            7,
            Arg.Is<CambiarPasswordDto>(d => d.IdUsuario == 7 && d.PasswordActual == "Temporal2026!"),
            Arg.Any<CancellationToken>());
        _session.Received(1).EstablecerSesion(Arg.Is<LoginResultDto>(r => r.IdUsuario == 7 && !r.DebeCambiarPassword));
        _sut.TieneError.Should().BeFalse();
    }

    [Fact]
    public async Task IniciarSesionCommand_CuentaConCambioObligatorioYCambioCancelado_NoEstableceSesion()
    {
        // Arrange
        _sut.NombreUsuario = "cajero";
        _authService.LoginAsync(Arg.Any<LoginRequestDto>(), Arg.Any<CancellationToken>()).Returns(CuentaConCambioObligatorio());
        _dialogService.MostrarDialogoCambioObligatorio(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Func<CambiarPasswordDto, Task>>())
            .Returns(false);

        var eventoDisparado = false;
        _sut.LoginExitoso += () => eventoDisparado = true;

        // Act
        await _sut.IniciarSesionCommand.ExecuteAsync("Temporal2026!");

        // Assert
        _session.DidNotReceive().EstablecerSesion(Arg.Any<LoginResultDto>());
        eventoDisparado.Should().BeFalse();
        _sut.TieneError.Should().BeTrue();
        _sut.MensajeError.Should().Contain("nueva contraseña");
        _sut.EstaCargando.Should().BeFalse();
    }

    [Fact]
    public async Task IniciarSesionCommand_CuentaSinCambioObligatorio_NoMuestraElDialogo()
    {
        // Arrange
        _sut.NombreUsuario = "cajero";
        _authService.LoginAsync(Arg.Any<LoginRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(CuentaConCambioObligatorio() with { DebeCambiarPassword = false });

        // Act
        await _sut.IniciarSesionCommand.ExecuteAsync("MiClave2026!");

        // Assert
        _dialogService.DidNotReceiveWithAnyArgs().MostrarDialogoCambioObligatorio(default, default!, default!, default!);
        _session.Received(1).EstablecerSesion(Arg.Any<LoginResultDto>());
    }
}

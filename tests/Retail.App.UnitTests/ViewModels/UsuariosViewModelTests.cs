using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Services;
using Retail.App.UnitTests.Helpers;
using Retail.App.ViewModels.Usuarios;
using Retail.Application.DTOs.Usuarios;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class UsuariosViewModelTests : IDisposable
{
    private readonly IUsuarioService _usuarioService;
    private const int IdActor = 100;

    private readonly IUsuarioDialogService _dialogService;
    private readonly ICurrentUserSession _session;
    private readonly UsuariosViewModel _sut;

    public UsuariosViewModelTests()
    {
        _usuarioService = Substitute.For<IUsuarioService>();
        _dialogService = Substitute.For<IUsuarioDialogService>();

        _session = Substitute.For<ICurrentUserSession>();
        _session.IdUsuario.Returns(IdActor);
        _usuarioService.ObtenerRolesAsignablesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Enum.GetValues<RolUsuarioEnum>());
        _usuarioService.ListarUsuariosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<UsuarioDto>());

        _sut = new UsuariosViewModel(_usuarioService, _dialogService, _session);
    }

    [Fact]
    public async Task CargarUsuariosAsync_Exito_CargaUsuariosEnColeccion()
    {
        // Arrange
        var lista = new List<UsuarioDto>
        {
            new() { IdUsuario = 1, NombreUsuario = "admin", Nombre = "Ana", Apellido = "Gerente", Rol = RolUsuarioEnum.Gerente, Activo = true, CreatedAt = DateTime.UtcNow },
            new() { IdUsuario = 2, NombreUsuario = "cajero", Nombre = "Juan", Apellido = "Cajero", Rol = RolUsuarioEnum.Cajero, Activo = true, CreatedAt = DateTime.UtcNow }
        };

        _usuarioService.ListarUsuariosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(lista);

        // Act
        await _sut.CargarUsuariosCommand.ExecuteAsync(null);

        // Assert
        _sut.Usuarios.Should().HaveCount(2);
        _sut.IsBusy.Should().BeFalse();
        _sut.MensajeError.Should().BeNull();
        _sut.MensajeEstado.Should().Contain("2 operadores");
    }

    [Fact]
    public async Task CargarUsuariosAsync_Error_FijaMensajeYDisparaAlerta()
    {
        // Arrange
        _usuarioService.ListarUsuariosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Fallo en la conexión"));

        // Act
        await _sut.CargarUsuariosCommand.ExecuteAsync(null);

        // Assert
        _sut.Usuarios.Should().BeEmpty();
        _sut.IsBusy.Should().BeFalse();
        _sut.MensajeError.Should().Contain("Fallo en la conexión");
        _dialogService.Received(1).MostrarError("Error de Carga", "Fallo en la conexión");
    }

    [Fact]
    public async Task FiltroBusqueda_CambioDeTexto_FiltraColeccionEnMemoria()
    {
        // Arrange
        var lista = new List<UsuarioDto>
        {
            new() { IdUsuario = 1, NombreUsuario = "admin", Nombre = "Ana", Apellido = "Gerente", Rol = RolUsuarioEnum.Gerente, Activo = true, CreatedAt = DateTime.UtcNow },
            new() { IdUsuario = 2, NombreUsuario = "cajero", Nombre = "Juan", Apellido = "Perez", Rol = RolUsuarioEnum.Cajero, Activo = true, CreatedAt = DateTime.UtcNow }
        };

        _usuarioService.ListarUsuariosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(lista);

        await _sut.CargarUsuariosCommand.ExecuteAsync(null);

        // Act 1: Filtrar por "perez"
        _sut.FiltroBusqueda = "perez";

        // Assert 1
        _sut.Usuarios.Should().HaveCount(1);
        _sut.Usuarios[0].NombreUsuario.Should().Be("cajero");

        // Act 2: Filtrar por "admin"
        _sut.FiltroBusqueda = "admin";

        // Assert 2
        _sut.Usuarios.Should().HaveCount(1);
        _sut.Usuarios[0].NombreUsuario.Should().Be("admin");

        // Act 3: Limpiar filtro
        _sut.FiltroBusqueda = string.Empty;

        // Assert 3
        _sut.Usuarios.Should().HaveCount(2);
    }

    [Fact]
    public async Task NuevoUsuarioAsync_DialogoConfirmado_RegistraUsuarioYRecarga()
    {
        // Arrange
        // La pantalla carga la lista y los roles asignables al abrirse.
        await _sut.CargarUsuariosCommand.ExecuteAsync(null);

        var dto = new CrearUsuarioDto
        {
            NombreUsuario = "nuevo",
            Nombre = "Nuevo",
            Apellido = "Operador",
            Password = "Password123!",
            Rol = RolUsuarioEnum.Cajero,
            EmailRecuperacion = null
        };

        var nuevo = new UsuarioDto
        {
            IdUsuario = 5,
            NombreUsuario = "nuevo",
            Nombre = "Nuevo",
            Apellido = "Operador",
            Rol = RolUsuarioEnum.Cajero,
            Activo = true,
            CreatedAt = DateTime.UtcNow
        };

        _dialogService.MostrarDialogoCrear(Arg.Any<IReadOnlyList<RolUsuarioEnum>>(), Arg.Any<Func<CrearUsuarioDto, Task>>())
            .Returns(callInfo =>
            {
                var callback = callInfo.Arg<Func<CrearUsuarioDto, Task>?>();
                callback?.Invoke(dto).GetAwaiter().GetResult();
                return dto;
            });
        _usuarioService.RegistrarUsuarioAsync(Arg.Any<int>(), dto, Arg.Any<CancellationToken>()).Returns(nuevo);
        _usuarioService.ListarUsuariosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<UsuarioDto> { nuevo });

        // Act
        await _sut.NuevoUsuarioCommand.ExecuteAsync(null);

        // Assert
        await _usuarioService.Received(1).RegistrarUsuarioAsync(IdActor, dto, Arg.Any<CancellationToken>());
        _dialogService.Received(1).MostrarInformacion("Operación Exitosa", Arg.Is<string>(s => s.Contains("nuevo")));
        _sut.Usuarios.Should().Contain(u => u.IdUsuario == 5);
    }

    [Fact]
    public async Task NuevoUsuarioAsync_DialogoCancelado_NoLlamaAlServicio()
    {
        // Arrange
        _dialogService.MostrarDialogoCrear(Arg.Any<IReadOnlyList<RolUsuarioEnum>>(), Arg.Any<Func<CrearUsuarioDto, Task>>()).Returns((CrearUsuarioDto?)null);

        // Act
        await _sut.NuevoUsuarioCommand.ExecuteAsync(null);

        // Assert
        await _usuarioService.DidNotReceive().RegistrarUsuarioAsync(Arg.Any<int>(), Arg.Any<CrearUsuarioDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EditarUsuarioAsync_SinSeleccion_MuestraAlerta()
    {
        // Arrange
        _sut.UsuarioSeleccionado = null;

        // Act
        await _sut.EditarUsuarioCommand.ExecuteAsync(null);

        // Assert
        _dialogService.Received(1).MostrarError("Selección Requerida", Arg.Any<string>());
        _dialogService.DidNotReceive().MostrarDialogoModificar(Arg.Any<UsuarioDto>(), Arg.Any<IReadOnlyList<RolUsuarioEnum>>(), Arg.Any<Func<ModificarUsuarioDto, Task>>());
    }

    [Fact]
    public async Task EditarUsuarioAsync_ConUsuario_ActualizaYRecarga()
    {
        // Arrange
        // La pantalla carga la lista y los roles asignables al abrirse.
        await _sut.CargarUsuariosCommand.ExecuteAsync(null);

        var usuario = new UsuarioDto
        {
            IdUsuario = 2,
            NombreUsuario = "cajero",
            Nombre = "Juan",
            Apellido = "Cajero",
            Rol = RolUsuarioEnum.Cajero,
            Activo = true,
            CreatedAt = DateTime.UtcNow
        };

        var modDto = new ModificarUsuarioDto
        {
            IdUsuario = 2,
            Nombre = "Juan",
            Apellido = "Modificado",
            Rol = RolUsuarioEnum.Encargado,
            EmailRecuperacion = null
        };

        var usuarioActualizado = new UsuarioDto
        {
            IdUsuario = 2,
            NombreUsuario = "cajero",
            Nombre = "Juan",
            Apellido = "Modificado",
            Rol = RolUsuarioEnum.Encargado,
            Activo = true,
            CreatedAt = DateTime.UtcNow
        };

        _dialogService.MostrarDialogoModificar(usuario, Arg.Any<IReadOnlyList<RolUsuarioEnum>>(), Arg.Any<Func<ModificarUsuarioDto, Task>>())
            .Returns(callInfo =>
            {
                var callback = callInfo.Arg<Func<ModificarUsuarioDto, Task>?>();
                callback?.Invoke(modDto).GetAwaiter().GetResult();
                return modDto;
            });
        _usuarioService.ModificarUsuarioAsync(Arg.Any<int>(), modDto, Arg.Any<CancellationToken>()).Returns(usuarioActualizado);
        _usuarioService.ListarUsuariosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<UsuarioDto> { usuarioActualizado });

        // Act
        await _sut.EditarUsuarioCommand.ExecuteAsync(usuario);

        // Assert
        await _usuarioService.Received(1).ModificarUsuarioAsync(IdActor, modDto, Arg.Any<CancellationToken>());
        _dialogService.Received(1).MostrarInformacion("Operación Exitosa", Arg.Is<string>(s => s.Contains("Juan Modificado")));
    }

    [Fact]
    public async Task BajaUsuarioAsync_EsUltimoGerente_CapturaUltimoGerenteExceptionYNotifica()
    {
        // Arrange
        // La pantalla carga la lista y los roles asignables al abrirse.
        await _sut.CargarUsuariosCommand.ExecuteAsync(null);

        var gerente = new UsuarioDto
        {
            IdUsuario = 1,
            NombreUsuario = "admin",
            Nombre = "Admin",
            Apellido = "Unico",
            Rol = RolUsuarioEnum.Gerente,
            Activo = true,
            CreatedAt = DateTime.UtcNow
        };

        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _usuarioService.BajaUsuarioAsync(Arg.Any<int>(), 1, Arg.Any<CancellationToken>())
            .ThrowsAsync(new UltimoGerenteException(1));

        // Act
        await _sut.BajaUsuarioCommand.ExecuteAsync(gerente);

        // Assert
        _dialogService.Received(1).MostrarError("Operación Bloqueada", Arg.Is<string>(s => s.Contains("único Gerente")));
        _sut.MensajeError.Should().NotBeNull();
    }

    [Fact]
    public async Task BajaUsuarioAsync_ConfirmacionCancelada_NoInvocaBaja()
    {
        // Arrange
        var usuario = new UsuarioDto
        {
            IdUsuario = 3,
            NombreUsuario = "cajero",
            Nombre = "Carlos",
            Apellido = "Carlos",
            Rol = RolUsuarioEnum.Cajero,
            Activo = true,
            CreatedAt = DateTime.UtcNow
        };

        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        // Act
        await _sut.BajaUsuarioCommand.ExecuteAsync(usuario);

        // Assert
        await _usuarioService.DidNotReceive().BajaUsuarioAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BajaUsuarioAsync_UsuarioYaInactivo_MuestraAvisoYNoBorra()
    {
        // Arrange
        var usuarioInactivo = new UsuarioDto
        {
            IdUsuario = 4,
            NombreUsuario = "inactivo",
            Nombre = "Dado",
            Apellido = "de Baja",
            Rol = RolUsuarioEnum.Cajero,
            Activo = false,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        await _sut.BajaUsuarioCommand.ExecuteAsync(usuarioInactivo);

        // Assert
        _dialogService.Received(1).MostrarInformacion("Aviso", Arg.Is<string>(s => s.Contains("ya se encuentra dado de baja")));
        _dialogService.DidNotReceive().Confirmar(Arg.Any<string>(), Arg.Any<string>());
        await _usuarioService.DidNotReceive().BajaUsuarioAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestablecerPasswordAsync_DialogoConfirmado_InvocaServicio()
    {
        // Arrange
        // La pantalla carga la lista y los roles asignables al abrirse.
        await _sut.CargarUsuariosCommand.ExecuteAsync(null);

        var usuario = new UsuarioDto
        {
            IdUsuario = 2,
            NombreUsuario = "cajero",
            Nombre = "Juan",
            Apellido = "Cajero",
            Rol = RolUsuarioEnum.Cajero,
            Activo = true,
            CreatedAt = DateTime.UtcNow
        };

        var dto = new CambiarPasswordDto
        {
            IdUsuario = 2,
            NuevaPassword = "NuevaPassword123!"
        };

        _dialogService.MostrarDialogoCambiarPassword(usuario, Arg.Any<Func<CambiarPasswordDto, Task>>())
            .Returns(callInfo =>
            {
                var callback = callInfo.Arg<Func<CambiarPasswordDto, Task>?>();
                callback?.Invoke(dto).GetAwaiter().GetResult();
                return dto;
            });

        // Act
        await _sut.RestablecerPasswordCommand.ExecuteAsync(usuario);

        // Assert
        await _usuarioService.Received(1).CambiarPasswordAsync(Arg.Any<int>(), dto, Arg.Any<CancellationToken>());
        _dialogService.Received(1).MostrarInformacion("Contraseña Actualizada", Arg.Is<string>(s => s.Contains("cajero")));
    }

    [Fact]
    public void UsuariosView_InstanciacionEnHiloSTA_NoDebeLanzarExcepcion()
    {
        Exception? threadEx = null;
        WpfTestHelper.Run(() =>
        {
            try
            {
                var view = new Retail.App.Views.Pages.UsuariosView(_sut);
                view.Should().NotBeNull();
                view.DataContext.Should().Be(_sut);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        threadEx.Should().BeNull();
    }

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task EditarUsuarioAsync_RolFueraDeLosAsignables_AvisaSinAbrirElDialogo()
    {
        // Arrange: un Administrador solo gestiona Cajeros y Encargados.
        _usuarioService.ObtenerRolesAsignablesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { RolUsuarioEnum.Cajero, RolUsuarioEnum.Encargado });
        await _sut.CargarUsuariosCommand.ExecuteAsync(null);

        var gerente = new UsuarioDto
        {
            IdUsuario = 1,
            NombreUsuario = "gerente",
            Nombre = "Ana",
            Apellido = "Gómez",
            Rol = RolUsuarioEnum.Gerente,
            Activo = true,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        await _sut.EditarUsuarioCommand.ExecuteAsync(gerente);

        // Assert
        _dialogService.Received(1).MostrarError("Operación no permitida", Arg.Is<string>(m => m.Contains("Ana Gómez")));
        _dialogService.DidNotReceive().MostrarDialogoModificar(
            Arg.Any<UsuarioDto>(), Arg.Any<IReadOnlyList<RolUsuarioEnum>>(), Arg.Any<Func<ModificarUsuarioDto, Task>>());
    }

    [Fact]
    public async Task NuevoUsuarioAsync_PasaAlDialogoLosRolesAsignablesDelActor()
    {
        // Arrange
        var rolesAdministrador = new[] { RolUsuarioEnum.Cajero, RolUsuarioEnum.Encargado };
        _usuarioService.ObtenerRolesAsignablesAsync(IdActor, Arg.Any<CancellationToken>()).Returns(rolesAdministrador);
        await _sut.CargarUsuariosCommand.ExecuteAsync(null);

        // Act
        await _sut.NuevoUsuarioCommand.ExecuteAsync(null);

        // Assert: el combo del formulario solo ofrece lo que el servicio permitiría.
        _dialogService.Received(1).MostrarDialogoCrear(
            Arg.Is<IReadOnlyList<RolUsuarioEnum>>(r => r.SequenceEqual(rolesAdministrador)),
            Arg.Any<Func<CrearUsuarioDto, Task>>());
    }

    [Fact]
    public async Task RestablecerPasswordAsync_FilaPropia_AbreElCambioPropioEnLugarDelBlanqueo()
    {
        // Arrange
        await _sut.CargarUsuariosCommand.ExecuteAsync(null);
        var cuentaPropia = new UsuarioDto
        {
            IdUsuario = IdActor,
            NombreUsuario = "gerente",
            Nombre = "Ana",
            Apellido = "Gómez",
            Rol = RolUsuarioEnum.Gerente,
            Activo = true,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        await _sut.RestablecerPasswordCommand.ExecuteAsync(cuentaPropia);

        // Assert: blanquear la propia cuenta no pediría la contraseña actual.
        _dialogService.Received(1).MostrarDialogoCambioPropio(cuentaPropia, Arg.Any<Func<CambiarPasswordDto, Task>>());
        _dialogService.DidNotReceive().MostrarDialogoCambiarPassword(Arg.Any<UsuarioDto>(), Arg.Any<Func<CambiarPasswordDto, Task>>());
    }

    // ───────────────────────────── Desbloqueo (D-31) ─────────────────────────────

    private static UsuarioDto Administrador(bool bloqueada) => new()
    {
        IdUsuario = 5,
        NombreUsuario = "admin",
        Nombre = "Ada",
        Apellido = "Sistema",
        Rol = RolUsuarioEnum.Administrador,
        Activo = true,
        EstaBloqueada = bloqueada,
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task DesbloquearAsync_GerenteConfirma_LlamaAlServicioYRecarga()
    {
        // Arrange
        _session.EsGerente.Returns(true);
        var bloqueada = Administrador(bloqueada: true);
        _usuarioService.ListarUsuariosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<UsuarioDto> { bloqueada });
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        await _sut.CargarUsuariosCommand.ExecuteAsync(null);
        _sut.UsuarioSeleccionado = _sut.Usuarios[0];
        _usuarioService.ClearReceivedCalls();

        // Act
        await _sut.DesbloquearCommand.ExecuteAsync(null);

        // Assert
        await _usuarioService.Received(1).DesbloquearUsuarioAsync(IdActor, bloqueada.IdUsuario, Arg.Any<CancellationToken>());
        await _usuarioService.Received(1).ListarUsuariosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        _dialogService.Received(1).MostrarInformacion("Cuenta Desbloqueada", Arg.Any<string>());
        _sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task DesbloquearAsync_GerenteCancela_NoLlamaAlServicio()
    {
        // Arrange
        _session.EsGerente.Returns(true);
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        _sut.UsuarioSeleccionado = Administrador(bloqueada: true);

        // Act
        await _sut.DesbloquearCommand.ExecuteAsync(null);

        // Assert
        await _usuarioService.DidNotReceiveWithAnyArgs().DesbloquearUsuarioAsync(default, default, default);
    }

    [Fact]
    public async Task DesbloquearAsync_ServicioFalla_InformaElError()
    {
        // Arrange
        _session.EsGerente.Returns(true);
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _usuarioService.DesbloquearUsuarioAsync(IdActor, 5, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("sin conexión"));
        _sut.UsuarioSeleccionado = Administrador(bloqueada: true);

        // Act
        await _sut.DesbloquearCommand.ExecuteAsync(null);

        // Assert
        _sut.MensajeError.Should().Contain("sin conexión");
        _dialogService.Received(1).MostrarError("Error al Desbloquear", Arg.Is<string>(m => m.Contains("sin conexión")));
        _sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public void DesbloquearCommand_SesionDeAdministrador_NoSePuedeEjecutar()
    {
        // Arrange
        _session.EsGerente.Returns(false);
        _session.EsAdministrador.Returns(true);
        _sut.UsuarioSeleccionado = Administrador(bloqueada: true);

        // Assert
        _sut.PuedeDesbloquear.Should().BeFalse();
        _sut.DesbloquearCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void DesbloquearCommand_FilaNoBloqueada_NoSePuedeEjecutar()
    {
        // Arrange
        _session.EsGerente.Returns(true);

        // Act
        _sut.UsuarioSeleccionado = Administrador(bloqueada: false);

        // Assert
        _sut.DesbloquearCommand.CanExecute(null).Should().BeFalse();

        _sut.UsuarioSeleccionado = Administrador(bloqueada: true);
        _sut.DesbloquearCommand.CanExecute(null).Should().BeTrue();
    }
}

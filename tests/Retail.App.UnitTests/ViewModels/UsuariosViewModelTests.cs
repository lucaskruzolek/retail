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

public class UsuariosViewModelTests
{
    private readonly IUsuarioService _usuarioService;
    private readonly IUsuarioDialogService _dialogService;
    private readonly UsuariosViewModel _sut;

    public UsuariosViewModelTests()
    {
        _usuarioService = Substitute.For<IUsuarioService>();
        _dialogService = Substitute.For<IUsuarioDialogService>();

        _sut = new UsuariosViewModel(_usuarioService, _dialogService);
    }

    [Fact]
    public async Task CargarUsuariosAsync_Exito_CargaUsuariosEnColeccion()
    {
        // Arrange
        var lista = new List<UsuarioDto>
        {
            new() { IdUsuario = 1, NombreUsuario = "admin", NombreCompleto = "Ana Gerente", Rol = RolUsuarioEnum.Gerente, Activo = true, CreatedAt = DateTime.UtcNow },
            new() { IdUsuario = 2, NombreUsuario = "cajero", NombreCompleto = "Juan Cajero", Rol = RolUsuarioEnum.Cajero, Activo = true, CreatedAt = DateTime.UtcNow }
        };

        _usuarioService.ListarUsuariosAsync(Arg.Any<CancellationToken>())
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
        _usuarioService.ListarUsuariosAsync(Arg.Any<CancellationToken>())
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
            new() { IdUsuario = 1, NombreUsuario = "admin", NombreCompleto = "Ana Gerente", Rol = RolUsuarioEnum.Gerente, Activo = true, CreatedAt = DateTime.UtcNow },
            new() { IdUsuario = 2, NombreUsuario = "cajero", NombreCompleto = "Juan Perez", Rol = RolUsuarioEnum.Cajero, Activo = true, CreatedAt = DateTime.UtcNow }
        };

        _usuarioService.ListarUsuariosAsync(Arg.Any<CancellationToken>())
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
        var dto = new CrearUsuarioDto
        {
            NombreUsuario = "nuevo",
            NombreCompleto = "Nuevo Operador",
            Password = "Password123!",
            Rol = RolUsuarioEnum.Cajero
        };

        var nuevo = new UsuarioDto
        {
            IdUsuario = 5,
            NombreUsuario = "nuevo",
            NombreCompleto = "Nuevo Operador",
            Rol = RolUsuarioEnum.Cajero,
            Activo = true,
            CreatedAt = DateTime.UtcNow
        };

        _dialogService.MostrarDialogoCrear(Arg.Any<Func<CrearUsuarioDto, Task>>())
            .Returns(callInfo =>
            {
                var callback = callInfo.Arg<Func<CrearUsuarioDto, Task>?>();
                callback?.Invoke(dto).GetAwaiter().GetResult();
                return dto;
            });
        _usuarioService.RegistrarUsuarioAsync(dto, Arg.Any<CancellationToken>()).Returns(nuevo);
        _usuarioService.ListarUsuariosAsync(Arg.Any<CancellationToken>())
            .Returns(new List<UsuarioDto> { nuevo });

        // Act
        await _sut.NuevoUsuarioCommand.ExecuteAsync(null);

        // Assert
        await _usuarioService.Received(1).RegistrarUsuarioAsync(dto, Arg.Any<CancellationToken>());
        _dialogService.Received(1).MostrarInformacion("Operación Exitosa", Arg.Is<string>(s => s.Contains("nuevo")));
        _sut.Usuarios.Should().Contain(u => u.IdUsuario == 5);
    }

    [Fact]
    public async Task NuevoUsuarioAsync_DialogoCancelado_NoLlamaAlServicio()
    {
        // Arrange
        _dialogService.MostrarDialogoCrear(Arg.Any<Func<CrearUsuarioDto, Task>>()).Returns((CrearUsuarioDto?)null);

        // Act
        await _sut.NuevoUsuarioCommand.ExecuteAsync(null);

        // Assert
        await _usuarioService.DidNotReceive().RegistrarUsuarioAsync(Arg.Any<CrearUsuarioDto>(), Arg.Any<CancellationToken>());
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
        _dialogService.DidNotReceive().MostrarDialogoModificar(Arg.Any<UsuarioDto>(), Arg.Any<Func<ModificarUsuarioDto, Task>>());
    }

    [Fact]
    public async Task EditarUsuarioAsync_ConUsuario_ActualizaYRecarga()
    {
        // Arrange
        var usuario = new UsuarioDto
        {
            IdUsuario = 2,
            NombreUsuario = "cajero",
            NombreCompleto = "Juan Cajero",
            Rol = RolUsuarioEnum.Cajero,
            Activo = true,
            CreatedAt = DateTime.UtcNow
        };

        var modDto = new ModificarUsuarioDto
        {
            IdUsuario = 2,
            NombreCompleto = "Juan Modificado",
            Rol = RolUsuarioEnum.Encargado
        };

        var usuarioActualizado = new UsuarioDto
        {
            IdUsuario = 2,
            NombreUsuario = "cajero",
            NombreCompleto = "Juan Modificado",
            Rol = RolUsuarioEnum.Encargado,
            Activo = true,
            CreatedAt = DateTime.UtcNow
        };

        _dialogService.MostrarDialogoModificar(usuario, Arg.Any<Func<ModificarUsuarioDto, Task>>())
            .Returns(callInfo =>
            {
                var callback = callInfo.Arg<Func<ModificarUsuarioDto, Task>?>();
                callback?.Invoke(modDto).GetAwaiter().GetResult();
                return modDto;
            });
        _usuarioService.ModificarUsuarioAsync(modDto, Arg.Any<CancellationToken>()).Returns(usuarioActualizado);
        _usuarioService.ListarUsuariosAsync(Arg.Any<CancellationToken>())
            .Returns(new List<UsuarioDto> { usuarioActualizado });

        // Act
        await _sut.EditarUsuarioCommand.ExecuteAsync(usuario);

        // Assert
        await _usuarioService.Received(1).ModificarUsuarioAsync(modDto, Arg.Any<CancellationToken>());
        _dialogService.Received(1).MostrarInformacion("Operación Exitosa", Arg.Is<string>(s => s.Contains("Juan Modificado")));
    }

    [Fact]
    public async Task BajaUsuarioAsync_EsUltimoGerente_CapturaUltimoGerenteExceptionYNotifica()
    {
        // Arrange
        var gerente = new UsuarioDto
        {
            IdUsuario = 1,
            NombreUsuario = "admin",
            NombreCompleto = "Admin Unico",
            Rol = RolUsuarioEnum.Gerente,
            Activo = true,
            CreatedAt = DateTime.UtcNow
        };

        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _usuarioService.BajaUsuarioAsync(1, Arg.Any<CancellationToken>())
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
            NombreCompleto = "Carlos",
            Rol = RolUsuarioEnum.Cajero,
            Activo = true,
            CreatedAt = DateTime.UtcNow
        };

        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        // Act
        await _sut.BajaUsuarioCommand.ExecuteAsync(usuario);

        // Assert
        await _usuarioService.DidNotReceive().BajaUsuarioAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BajaUsuarioAsync_UsuarioYaInactivo_MuestraAvisoYNoBorra()
    {
        // Arrange
        var usuarioInactivo = new UsuarioDto
        {
            IdUsuario = 4,
            NombreUsuario = "inactivo",
            NombreCompleto = "Dado de Baja",
            Rol = RolUsuarioEnum.Cajero,
            Activo = false,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        await _sut.BajaUsuarioCommand.ExecuteAsync(usuarioInactivo);

        // Assert
        _dialogService.Received(1).MostrarInformacion("Aviso", Arg.Is<string>(s => s.Contains("ya se encuentra dado de baja")));
        _dialogService.DidNotReceive().Confirmar(Arg.Any<string>(), Arg.Any<string>());
        await _usuarioService.DidNotReceive().BajaUsuarioAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestablecerPasswordAsync_DialogoConfirmado_InvocaServicio()
    {
        // Arrange
        var usuario = new UsuarioDto
        {
            IdUsuario = 2,
            NombreUsuario = "cajero",
            NombreCompleto = "Juan Cajero",
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
        await _usuarioService.Received(1).CambiarPasswordAsync(dto, Arg.Any<CancellationToken>());
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
}

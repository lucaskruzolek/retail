using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Retail.App.ViewModels.Usuarios;
using Retail.Application.DTOs.Usuarios;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

/// <summary>
/// Las reglas de formato (letras en el nombre, login, longitud de la contraseña) se prueban en
/// <c>UsuarioValidatorTests</c>: el ViewModel ya no las replica. Acá se prueba solo lo propio de la interfaz.
/// </summary>
public class UsuarioFormViewModelTests
{
    private static readonly IReadOnlyList<RolUsuarioEnum> TodosLosRoles = Enum.GetValues<RolUsuarioEnum>();

    private readonly UsuarioFormViewModel _sut = new();

    private static UsuarioDto UsuarioExistente() => new()
    {
        IdUsuario = 1,
        NombreUsuario = "lucas",
        Nombre = "Lucas",
        Apellido = "Pérez",
        Rol = RolUsuarioEnum.Cajero,
        Activo = true,
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public void NombreUsuario_ConEspacios_RemueveEspaciosAutomaticamente()
    {
        // Act
        _sut.NombreUsuario = "lucas perez ";

        // Assert
        _sut.NombreUsuario.Should().Be("lucasperez");
    }

    [Fact]
    public void Validar_AltaConConfirmacionDistinta_FallaValidacion()
    {
        // Arrange
        _sut.ConfigurarAlta(TodosLosRoles);
        _sut.Password = "Clave2026!";
        _sut.ConfirmarPassword = "Clave2026?";

        // Act
        var esValido = _sut.Validar();

        // Assert
        esValido.Should().BeFalse();
        _sut.TieneError.Should().BeTrue();
        _sut.MensajeError.Should().Be("Las contraseñas ingresadas no coinciden.");
    }

    [Fact]
    public void Validar_AltaConConfirmacionIgual_RetornaTrueYSinError()
    {
        // Arrange
        _sut.ConfigurarAlta(TodosLosRoles);
        _sut.Password = "Clave2026!";
        _sut.ConfirmarPassword = "Clave2026!";

        // Act & Assert
        _sut.Validar().Should().BeTrue();
        _sut.MensajeError.Should().BeNull();
    }

    [Fact]
    public void Validar_ModoEdicion_NoExigeConfirmacionDePassword()
    {
        // Arrange
        _sut.ConfigurarEdicion(UsuarioExistente(), TodosLosRoles);

        // Act & Assert
        _sut.Validar().Should().BeTrue();
        _sut.TieneError.Should().BeFalse();
    }

    [Fact]
    public void ConfigurarEdicion_UsuarioExistente_CargaNombreYApellidoSeparados()
    {
        // Act
        _sut.ConfigurarEdicion(UsuarioExistente(), TodosLosRoles);

        // Assert
        _sut.EsModoEdicion.Should().BeTrue();
        _sut.Nombre.Should().Be("Lucas");
        _sut.Apellido.Should().Be("Pérez");
        _sut.Password.Should().BeEmpty();
        _sut.ConfirmarPassword.Should().BeEmpty();
    }

    [Fact]
    public void ObtenerCrearDto_DatosCargados_EnviaNombreYApellidoSinNormalizar()
    {
        // Arrange: la normalización es responsabilidad del agregado, no de la interfaz.
        _sut.ConfigurarAlta(TodosLosRoles);
        _sut.NombreUsuario = "lucas.perez_99";
        _sut.Nombre = "  lucas ";
        _sut.Apellido = "PÉREZ";
        _sut.Password = "PasswordSegura123";
        _sut.Rol = RolUsuarioEnum.Encargado;

        // Act
        var dto = _sut.ObtenerCrearDto();

        // Assert
        dto.NombreUsuario.Should().Be("lucas.perez_99");
        dto.Nombre.Should().Be("  lucas ");
        dto.Apellido.Should().Be("PÉREZ");
        dto.Password.Should().Be("PasswordSegura123");
        dto.Rol.Should().Be(RolUsuarioEnum.Encargado);
    }

    [Fact]
    public void ObtenerModificarDto_ModoEdicion_EnviaIdNombreApellidoYRol()
    {
        // Arrange
        _sut.ConfigurarEdicion(UsuarioExistente(), TodosLosRoles);
        _sut.Apellido = "Gómez";

        // Act
        var dto = _sut.ObtenerModificarDto();

        // Assert
        dto.IdUsuario.Should().Be(1);
        dto.Nombre.Should().Be("Lucas");
        dto.Apellido.Should().Be("Gómez");
        dto.Rol.Should().Be(RolUsuarioEnum.Cajero);
    }

    [Fact]
    public void InformarError_ValidationException_ListaCadaErrorEnUnaLineaSinRepetir()
    {
        // Arrange
        var ex = new ValidationException(
        [
            new ValidationFailure("Nombre", "Revise el nombre: solo admite letras."),
            new ValidationFailure("Apellido", "Complete el apellido."),
            new ValidationFailure("Apellido", "Complete el apellido.")
        ]);

        // Act
        _sut.InformarError(ex);

        // Assert
        _sut.MensajeError.Should().Be("Revise el nombre: solo admite letras.\nComplete el apellido.");
        _sut.TieneError.Should().BeTrue();
    }

    [Fact]
    public void InformarError_DomainException_MuestraSuMensaje()
    {
        // Act
        _sut.InformarError(new DomainException("El nombre de usuario 'jperez' ya se encuentra registrado en el sistema."));

        // Assert
        _sut.MensajeError.Should().Be("El nombre de usuario 'jperez' ya se encuentra registrado en el sistema.");
    }

    [Fact]
    public void ConfigurarAlta_RolesDelAdministrador_OfreceSoloEsosRolesYPreseleccionaElDeMenosPrivilegios()
    {
        // Act: el servicio devuelve Encargado y Cajero para un Administrador (en cualquier orden).
        _sut.ConfigurarAlta([RolUsuarioEnum.Encargado, RolUsuarioEnum.Cajero]);

        // Assert
        _sut.RolesDisponibles.Should().BeEquivalentTo([RolUsuarioEnum.Cajero, RolUsuarioEnum.Encargado]);
        _sut.Rol.Should().Be(RolUsuarioEnum.Cajero);
    }

    [Fact]
    public void ConfigurarEdicion_UsuarioConEmail_CargaElEmailYLoEnviaEnElDto()
    {
        // Arrange
        var gerente = UsuarioExistente() with { Rol = RolUsuarioEnum.Gerente, EmailRecuperacion = "lucas@libreria.com" };

        // Act
        _sut.ConfigurarEdicion(gerente, TodosLosRoles);

        // Assert: si el email no viajara en el DTO, guardar la edición lo borraría.
        _sut.EmailRecuperacion.Should().Be("lucas@libreria.com");
        _sut.ObtenerModificarDto().EmailRecuperacion.Should().Be("lucas@libreria.com");
    }

    [Fact]
    public void EtiquetaEmail_RolGerente_IndicaQueEsObligatorio()
    {
        _sut.ConfigurarAlta(TodosLosRoles);
        _sut.EtiquetaEmail.Should().Contain("opcional");

        _sut.Rol = RolUsuarioEnum.Gerente;

        _sut.EtiquetaEmail.Should().Contain("obligatorio");
    }
}

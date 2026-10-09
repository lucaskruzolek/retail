using System.Linq.Expressions;
using FluentAssertions;
using FluentValidation;
using NSubstitute;
using Retail.Application.DTOs.Auth;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Application.Validators.Auth;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Application.UnitTests.Services;

public class AuthServiceTests
{
    private readonly IRepository<Usuario> _usuarioRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly LoginRequestValidator _validator;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _usuarioRepository = Substitute.For<IRepository<Usuario>>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _validator = new LoginRequestValidator();

        _sut = new AuthService(
            _usuarioRepository,
            _passwordHasher,
            _validator);
    }

    private static Usuario CrearUsuario(
        int id,
        string nombreUsuario,
        string nombre,
        string apellido,
        string passwordHash,
        RolUsuarioEnum rol)
    {
        var usuario = Usuario.Crear(nombreUsuario, nombre, apellido, passwordHash, rol);
        usuario.Id = id;
        return usuario;
    }

    [Fact]
    public async Task LoginAsync_CredencialesValidas_RetornaLoginResultDto()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            NombreUsuario = "admin",
            Password = "Password123!"
        };

        var usuario = CrearUsuario(1, "admin", "Administrador", "Sistema", "hashedPassword", RolUsuarioEnum.Gerente);

        _usuarioRepository.FindAsync(
            Arg.Any<Expression<Func<Usuario, bool>>>(),
            includeDeleted: true,
            Arg.Any<CancellationToken>())
            .Returns(new List<Usuario> { usuario });

        _passwordHasher.VerifyPassword(request.Password, usuario.PasswordHash).Returns(true);

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IdUsuario.Should().Be(1);
        result.NombreUsuario.Should().Be("admin");
        result.NombreCompleto.Should().Be("Administrador Sistema");
        result.Rol.Should().Be(RolUsuarioEnum.Gerente);
    }

    [Fact]
    public async Task LoginAsync_UsuarioInexistente_LanzaCredencialesInvalidasException()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            NombreUsuario = "noexiste",
            Password = "Password123!"
        };

        _usuarioRepository.FindAsync(
            Arg.Any<Expression<Func<Usuario, bool>>>(),
            includeDeleted: true,
            Arg.Any<CancellationToken>())
            .Returns(new List<Usuario>());

        // Act
        var act = () => _sut.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<CredencialesInvalidasException>();
    }

    [Fact]
    public async Task LoginAsync_PasswordInvalido_LanzaCredencialesInvalidasException()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            NombreUsuario = "admin",
            Password = "PasswordErronea!"
        };

        var usuario = CrearUsuario(1, "admin", "Administrador", "General", "hashedPassword", RolUsuarioEnum.Gerente);

        _usuarioRepository.FindAsync(
            Arg.Any<Expression<Func<Usuario, bool>>>(),
            includeDeleted: true,
            Arg.Any<CancellationToken>())
            .Returns(new List<Usuario> { usuario });

        _passwordHasher.VerifyPassword(request.Password, usuario.PasswordHash).Returns(false);

        // Act
        var act = () => _sut.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<CredencialesInvalidasException>();
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("  ADMIN ")]
    public async Task LoginAsync_NombreUsuarioConMayusculas_BuscaLaFormaCanonicaEnMinusculas(string nombreIngresado)
    {
        // Arrange: el alta guarda el login en minúsculas, así que el login debe normalizarlo igual (V-8).
        var request = new LoginRequestDto { NombreUsuario = nombreIngresado, Password = "Password123!" };
        var usuario = CrearUsuario(1, "admin", "Administrador", "General", "hashedPassword", RolUsuarioEnum.Gerente);

        Expression<Func<Usuario, bool>>? filtro = null;
        _usuarioRepository.FindAsync(
                Arg.Do<Expression<Func<Usuario, bool>>>(f => filtro = f),
                includeDeleted: true,
                Arg.Any<CancellationToken>())
            .Returns(new List<Usuario> { usuario });
        _passwordHasher.VerifyPassword(request.Password, usuario.PasswordHash).Returns(true);

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        filtro!.Compile()(usuario).Should().BeTrue();
        result.NombreUsuario.Should().Be("admin");
    }

    [Fact]
    public async Task LoginAsync_UsuarioInactivo_LanzaUsuarioInactivoException()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            NombreUsuario = "despedido",
            Password = "Password123!"
        };

        var usuario = CrearUsuario(5, "despedido", "Operador", "Inactivo", "hashedPassword", RolUsuarioEnum.Cajero);
        usuario.MarkAsDeleted(); // Soft delete

        _usuarioRepository.FindAsync(
            Arg.Any<Expression<Func<Usuario, bool>>>(),
            includeDeleted: true,
            Arg.Any<CancellationToken>())
            .Returns(new List<Usuario> { usuario });

        // Act
        var act = () => _sut.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<UsuarioInactivoException>()
            .Where(ex => ex.NombreUsuario == "despedido");
    }

    [Fact]
    public async Task LoginAsync_RequestInvalida_LanzaValidationException()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            NombreUsuario = "",
            Password = ""
        };

        // Act
        var act = () => _sut.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task LoginAsync_RequestNula_LanzaArgumentNullException()
    {
        // Act
        var act = () => _sut.LoginAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task LoginAsync_UsuarioRecreadoConMismoUsername_PriorizaUsuarioActivoYAutenticaExitosamente()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            NombreUsuario = "lucas",
            Password = "PasswordNueva123!"
        };

        var usuarioInactivo = CrearUsuario(1, "lucas", "Lucas", "Anterior", "hashViejo", RolUsuarioEnum.Cajero);
        usuarioInactivo.MarkAsDeleted();

        var usuarioActivo = CrearUsuario(2, "lucas", "Lucas", "Nuevo", "hashNuevo", RolUsuarioEnum.Gerente);

        // Simula la consulta en BD donde coexisten el registro inactivo (Id=1) y el activo (Id=2)
        _usuarioRepository.FindAsync(
            Arg.Any<Expression<Func<Usuario, bool>>>(),
            includeDeleted: true,
            Arg.Any<CancellationToken>())
            .Returns(new List<Usuario> { usuarioInactivo, usuarioActivo });

        _passwordHasher.VerifyPassword(request.Password, usuarioActivo.PasswordHash).Returns(true);

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IdUsuario.Should().Be(2);
        result.NombreUsuario.Should().Be("lucas");
        result.NombreCompleto.Should().Be("Lucas Nuevo");
        result.Rol.Should().Be(RolUsuarioEnum.Gerente);
    }

    [Fact]
    public async Task LoginAsync_UsuarioRecreadoConPasswordErroneo_LanzaCredencialesInvalidasException()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            NombreUsuario = "lucas",
            Password = "PasswordIncorrecta!"
        };

        var usuarioInactivo = CrearUsuario(1, "lucas", "Lucas", "Anterior", "hashViejo", RolUsuarioEnum.Cajero);
        usuarioInactivo.MarkAsDeleted();

        var usuarioActivo = CrearUsuario(2, "lucas", "Lucas", "Nuevo", "hashNuevo", RolUsuarioEnum.Gerente);

        _usuarioRepository.FindAsync(
            Arg.Any<Expression<Func<Usuario, bool>>>(),
            includeDeleted: true,
            Arg.Any<CancellationToken>())
            .Returns(new List<Usuario> { usuarioInactivo, usuarioActivo });

        _passwordHasher.VerifyPassword(request.Password, usuarioActivo.PasswordHash).Returns(false);

        // Act
        var act = () => _sut.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<CredencialesInvalidasException>();
    }
}


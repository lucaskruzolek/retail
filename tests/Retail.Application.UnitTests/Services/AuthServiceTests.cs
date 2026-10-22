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
    private readonly IRegistroAccesosService _registroAccesosService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly LoginRequestValidator _validator;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _usuarioRepository = Substitute.For<IRepository<Usuario>>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _validator = new LoginRequestValidator();
        _registroAccesosService = Substitute.For<IRegistroAccesosService>();

        _registroAccesosService.RegistrarIntentoAsync(
                Arg.Any<string>(),
                Arg.Any<int?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(new ResultadoRegistroIntentoDto
            {
                Resultado = 0,
                IntentosRestantes = 5,
                BloqueadoHasta = null
            });

        _sut = new AuthService(
            _usuarioRepository,
            _passwordHasher,
            _validator,
            _registroAccesosService);
    }

    private static Usuario CrearUsuario(
        int id,
        string nombreUsuario,
        string nombre,
        string apellido,
        string passwordHash,
        RolUsuarioEnum rol)
    {
        // Un Gerente necesita email de recuperación; para el resto de los roles es opcional.
        var email = rol == RolUsuarioEnum.Gerente ? $"{nombreUsuario}@libreria.com" : null;
        var usuario = Usuario.Crear(nombreUsuario, nombre, apellido, passwordHash, rol, email);
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

    [Fact]
    public async Task LoginAsync_CuentaBloqueadaPorExcesoDeIntentos_LanzaCuentaBloqueadaException()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            NombreUsuario = "admin",
            Password = "Password123!"
        };

        var usuario = CrearUsuario(1, "admin", "Administrador", "General", "hashedPassword", RolUsuarioEnum.Gerente);

        _usuarioRepository.FindAsync(
            Arg.Any<Expression<Func<Usuario, bool>>>(),
            includeDeleted: true,
            Arg.Any<CancellationToken>())
            .Returns(new List<Usuario> { usuario });

        _passwordHasher.VerifyPassword(request.Password, usuario.PasswordHash).Returns(true);

        var desbloqueoUtc = DateTime.UtcNow.AddMinutes(15);
        _registroAccesosService.RegistrarIntentoAsync(
            "admin",
            1,
            true,
            Arg.Any<CancellationToken>())
            .Returns(new ResultadoRegistroIntentoDto
            {
                Resultado = 2,
                IntentosRestantes = 0,
                BloqueadoHasta = desbloqueoUtc
            });

        // Act
        var act = () => _sut.LoginAsync(request);

        // Assert
        var ex = await act.Should().ThrowAsync<CuentaBloqueadaException>();
        ex.Which.BloqueadoHastaUtc.Should().Be(desbloqueoUtc);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LoginAsync_CredencialesValidas_InformaSiDebeCambiarPassword(bool cambioPropioRealizado)
    {
        // Arrange
        var usuario = CrearUsuario(7, "cajero", "Carla", "Díaz", "hash_temporal", RolUsuarioEnum.Cajero);
        if (cambioPropioRealizado)
        {
            usuario.CambiarPasswordPropia("hash_elegido");
        }

        _usuarioRepository.FindAsync(Arg.Any<Expression<Func<Usuario, bool>>>(), includeDeleted: true, Arg.Any<CancellationToken>())
            .Returns(new List<Usuario> { usuario });
        _passwordHasher.VerifyPassword(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        // Act
        var result = await _sut.LoginAsync(new LoginRequestDto { NombreUsuario = "cajero", Password = "Clave2026!" });

        // Assert: una cuenta recién creada o blanqueada debe cambiar la contraseña antes de operar.
        result.DebeCambiarPassword.Should().Be(!cambioPropioRealizado);
    }

    [Fact]
    public async Task VerificarCredencialAsync_PasswordCorrecta_NoLanzaExcepcion()
    {
        // Arrange
        var gerente = CrearUsuario(1, "gerente", "Ana", "Gómez", "hash_gerente", RolUsuarioEnum.Gerente);
        _usuarioRepository.GetByIdAsync(1, includeDeleted: false, Arg.Any<CancellationToken>()).Returns(gerente);
        _passwordHasher.VerifyPassword("Clave2026!", "hash_gerente").Returns(true);

        // Act
        var act = async () => await _sut.VerificarCredencialAsync(1, "Clave2026!");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task VerificarCredencialAsync_PasswordIncorrecta_LanzaCredencialesInvalidasException()
    {
        // Arrange
        var gerente = CrearUsuario(1, "gerente", "Ana", "Gómez", "hash_gerente", RolUsuarioEnum.Gerente);
        _usuarioRepository.GetByIdAsync(1, includeDeleted: false, Arg.Any<CancellationToken>()).Returns(gerente);
        _passwordHasher.VerifyPassword("otra", "hash_gerente").Returns(false);

        // Act
        var act = async () => await _sut.VerificarCredencialAsync(1, "otra");

        // Assert
        await act.Should().ThrowAsync<CredencialesInvalidasException>();
    }

    private static Usuario CrearAdministradorBloqueado()
    {
        var administrador = CrearUsuario(2, "soporte", "Sara", "Ruiz", "hash_soporte", RolUsuarioEnum.Administrador);
        for (var i = 0; i < Usuario.MaximoIntentosAutorizacion; i++)
        {
            administrador.RegistrarIntentoDeAutorizacion();
        }

        return administrador;
    }

    [Fact]
    public async Task LoginAsync_CuentaBloqueadaConPasswordCorrecta_LanzaCuentaBloqueada()
    {
        // Arrange
        var bloqueado = CrearAdministradorBloqueado();
        _usuarioRepository.FindAsync(Arg.Any<Expression<Func<Usuario, bool>>>(), includeDeleted: true, Arg.Any<CancellationToken>())
            .Returns(new List<Usuario> { bloqueado });
        _passwordHasher.VerifyPassword("Clave2026!", "hash_soporte").Returns(true);

        // Act
        var act = () => _sut.LoginAsync(new LoginRequestDto { NombreUsuario = "soporte", Password = "Clave2026!" });

        // Assert
        (await act.Should().ThrowAsync<CuentaBloqueadaException>())
            .Which.RequiereDesbloqueoDeGerente.Should().BeTrue();
    }

    [Fact]
    public async Task LoginAsync_CuentaBloqueadaPorAutorizacionConPasswordCorrecta_NoInformaUnLoginAceptadoAlSp()
    {
        // Arrange: informarle al SP un login válido registraría "Aceptado" en la auditoría y reiniciaría su contador.
        var bloqueado = CrearAdministradorBloqueado();
        _usuarioRepository.FindAsync(Arg.Any<Expression<Func<Usuario, bool>>>(), includeDeleted: true, Arg.Any<CancellationToken>())
            .Returns(new List<Usuario> { bloqueado });
        _passwordHasher.VerifyPassword("Clave2026!", "hash_soporte").Returns(true);

        // Act
        var act = () => _sut.LoginAsync(new LoginRequestDto { NombreUsuario = "soporte", Password = "Clave2026!" });

        // Assert
        await act.Should().ThrowAsync<CuentaBloqueadaException>();
        await _registroAccesosService.DidNotReceive().RegistrarIntentoAsync(
            Arg.Any<string>(), Arg.Any<int?>(), true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_CuentaBloqueadaPorAutorizacionConPasswordCorrecta_RegistraEventoBloqueado()
    {
        // Arrange
        var bloqueado = CrearAdministradorBloqueado();
        _usuarioRepository.FindAsync(Arg.Any<Expression<Func<Usuario, bool>>>(), includeDeleted: true, Arg.Any<CancellationToken>())
            .Returns(new List<Usuario> { bloqueado });
        _passwordHasher.VerifyPassword("Clave2026!", "hash_soporte").Returns(true);

        // Act
        var act = () => _sut.LoginAsync(new LoginRequestDto { NombreUsuario = "soporte", Password = "Clave2026!" });

        // Assert: el rechazo deja fila de auditoría (antes no dejaba ninguna) y sin la contraseña.
        await act.Should().ThrowAsync<CuentaBloqueadaException>();
        await _registroAccesosService.Received(1).RegistrarEventoAsync(
            TipoEventoAccesoEnum.InicioSesion,
            ResultadoEventoAccesoEnum.Bloqueado,
            "soporte",
            2,
            "Bloqueada por autorizaciones fallidas (D-31)",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_CuentaBloqueadaConPasswordIncorrecta_LanzaCredencialesInvalidas()
    {
        // Arrange: quien no conoce la contraseña no debe enterarse de que la cuenta está bloqueada.
        var bloqueado = CrearAdministradorBloqueado();
        _usuarioRepository.FindAsync(Arg.Any<Expression<Func<Usuario, bool>>>(), includeDeleted: true, Arg.Any<CancellationToken>())
            .Returns(new List<Usuario> { bloqueado });
        _passwordHasher.VerifyPassword("otra", "hash_soporte").Returns(false);

        // Act
        var act = () => _sut.LoginAsync(new LoginRequestDto { NombreUsuario = "soporte", Password = "otra" });

        // Assert
        await act.Should().ThrowAsync<CredencialesInvalidasException>();
    }

    [Fact]
    public async Task VerificarCredencialAsync_CuentaBloqueadaConPasswordCorrecta_LanzaCuentaBloqueada()
    {
        // Arrange
        var bloqueado = CrearAdministradorBloqueado();
        _usuarioRepository.GetByIdAsync(2, includeDeleted: false, Arg.Any<CancellationToken>()).Returns(bloqueado);
        _passwordHasher.VerifyPassword("Clave2026!", "hash_soporte").Returns(true);

        // Act
        var act = async () => await _sut.VerificarCredencialAsync(2, "Clave2026!");

        // Assert
        await act.Should().ThrowAsync<CuentaBloqueadaException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public async Task VerificarCredencialAsync_UsuarioInexistenteOInactivo_LanzaCredencialesInvalidasSinConsultarElHash(int idUsuario)
    {
        // Arrange: el repositorio no devuelve usuarios dados de baja (includeDeleted: false).
        _usuarioRepository.GetByIdAsync(idUsuario, includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns((Usuario?)null);

        // Act
        var act = async () => await _sut.VerificarCredencialAsync(idUsuario, "Clave2026!");

        // Assert: misma respuesta que una contraseña incorrecta, para no revelar el estado de la cuenta.
        await act.Should().ThrowAsync<CredencialesInvalidasException>();
        _passwordHasher.DidNotReceive().VerifyPassword(Arg.Any<string>(), Arg.Any<string>());
    }
}

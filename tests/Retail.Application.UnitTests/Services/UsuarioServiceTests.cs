using System.Linq.Expressions;
using FluentAssertions;
using FluentValidation;
using NSubstitute;
using Retail.Application.DTOs.Usuarios;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Application.Validators.Usuarios;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Application.UnitTests.Services;

public class UsuarioServiceTests
{
    private readonly IRepository<Usuario> _usuarioRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly CrearUsuarioValidator _crearValidator;
    private readonly ModificarUsuarioValidator _modificarValidator;
    private readonly CambiarPasswordValidator _cambiarPasswordValidator;
    private readonly UsuarioService _sut;

    public UsuarioServiceTests()
    {
        _usuarioRepository = Substitute.For<IRepository<Usuario>>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _crearValidator = new CrearUsuarioValidator();
        _modificarValidator = new ModificarUsuarioValidator();
        _cambiarPasswordValidator = new CambiarPasswordValidator();

        _sut = new UsuarioService(
            _usuarioRepository,
            _unitOfWork,
            _passwordHasher,
            _crearValidator,
            _modificarValidator,
            _cambiarPasswordValidator);
    }

    [Fact]
    public async Task ListarUsuariosAsync_DebeLlamarListAllAsyncConFalseYRetornarDtos()
    {
        // Arrange
        var usuarios = new List<Usuario>
        {
            new() { Id = 1, NombreUsuario = "jperez", NombreCompleto = "Juan Perez", IdRol = (int)RolUsuarioEnum.Cajero },
            new() { Id = 2, NombreUsuario = "admin", NombreCompleto = "Ana Gerente", IdRol = (int)RolUsuarioEnum.Gerente }
        };

        _usuarioRepository.ListAllAsync(includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(usuarios);

        // Act
        var resultado = await _sut.ListarUsuariosAsync();

        // Assert
        resultado.Should().HaveCount(2);
        resultado[0].NombreCompleto.Should().Be("Ana Gerente"); // ordenado alfabéticamente
        resultado[1].NombreCompleto.Should().Be("Juan Perez");
        resultado[0].Rol.Should().Be(RolUsuarioEnum.Gerente);
        resultado[1].Rol.Should().Be(RolUsuarioEnum.Cajero);
        await _usuarioRepository.Received(1).ListAllAsync(includeDeleted: false, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ObtenerPorIdAsync_IdInvalido_LanzaArgumentOutOfRangeException(int idInvalido)
    {
        // Act
        var act = async () => await _sut.ObtenerPorIdAsync(idInvalido);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ObtenerPorIdAsync_UsuarioNoExiste_LanzaDomainException()
    {
        // Arrange
        _usuarioRepository.GetByIdAsync(99, includeDeleted: true, Arg.Any<CancellationToken>())
            .Returns((Usuario?)null);

        // Act
        var act = async () => await _sut.ObtenerPorIdAsync(99);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*99*");
    }

    [Fact]
    public async Task ObtenerPorIdAsync_UsuarioExiste_RetornaUsuarioDto()
    {
        // Arrange
        var usuario = new Usuario
        {
            Id = 5,
            NombreUsuario = "mlopez",
            NombreCompleto = "Maria Lopez",
            IdRol = (int)RolUsuarioEnum.Encargado
        };

        _usuarioRepository.GetByIdAsync(5, includeDeleted: true, Arg.Any<CancellationToken>())
            .Returns(usuario);

        // Act
        var dto = await _sut.ObtenerPorIdAsync(5);

        // Assert
        dto.IdUsuario.Should().Be(5);
        dto.NombreUsuario.Should().Be("mlopez");
        dto.NombreCompleto.Should().Be("Maria Lopez");
        dto.Rol.Should().Be(RolUsuarioEnum.Encargado);
        dto.Activo.Should().BeTrue();
    }

    [Fact]
    public async Task RegistrarUsuarioAsync_DatosInvalidos_LanzaValidationException()
    {
        // Arrange
        var dtoInvalido = new CrearUsuarioDto
        {
            NombreUsuario = "a", // muy corto
            NombreCompleto = "", // vacío
            Password = "123",    // muy corta
            Rol = (RolUsuarioEnum)99 // inválido
        };

        // Act
        var act = async () => await _sut.RegistrarUsuarioAsync(dtoInvalido);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        await _usuarioRepository.DidNotReceive().AddAsync(Arg.Any<Usuario>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarUsuarioAsync_NombreUsuarioDuplicado_LanzaDomainException()
    {
        // Arrange
        var dto = new CrearUsuarioDto
        {
            NombreUsuario = "Admin",
            NombreCompleto = "Nuevo Admin",
            Password = "PasswordSegura123!",
            Rol = RolUsuarioEnum.Gerente
        };

        _usuarioRepository.FindAsync(Arg.Any<Expression<Func<Usuario, bool>>>(), includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(new List<Usuario> { new() { NombreUsuario = "admin" } });

        // Act
        var act = async () => await _sut.RegistrarUsuarioAsync(dto);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*ya se encuentra registrado*");
        await _usuarioRepository.DidNotReceive().AddAsync(Arg.Any<Usuario>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarUsuarioAsync_UsuarioExistenteEliminado_CreaNuevoUsuarioConIdentidadPropia()
    {
        // Arrange
        var dto = new CrearUsuarioDto
        {
            NombreUsuario = "lucas",
            NombreCompleto = "Lucas Nuevo Empleado",
            Password = "NuevaPassword123!",
            Rol = RolUsuarioEnum.Cajero
        };

        // Al buscar usuarios activos (includeDeleted: false), no retorna ninguno porque el anterior está en soft-delete
        _usuarioRepository.FindAsync(Arg.Any<Expression<Func<Usuario, bool>>>(), includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(new List<Usuario>());

        _passwordHasher.HashPassword("NuevaPassword123!")
            .Returns("hash_nuevo_seguro");

        // Act
        var resultado = await _sut.RegistrarUsuarioAsync(dto);

        // Assert
        resultado.NombreUsuario.Should().Be("lucas");
        resultado.NombreCompleto.Should().Be("Lucas Nuevo Empleado");
        resultado.Rol.Should().Be(RolUsuarioEnum.Cajero);

        // Debe registrarse como una nueva entidad independiente (AddAsync), no modificar el registro histórico (UpdateAsync)
        await _usuarioRepository.Received(1).AddAsync(
            Arg.Is<Usuario>(u => u.NombreUsuario == "lucas" && u.NombreCompleto == "Lucas Nuevo Empleado" && u.PasswordHash == "hash_nuevo_seguro"),
            Arg.Any<CancellationToken>());
        await _usuarioRepository.DidNotReceive().UpdateAsync(Arg.Any<Usuario>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarUsuarioAsync_DatosValidos_HasheaPasswordYGuardaEnRepositorio()
    {
        // Arrange
        var dto = new CrearUsuarioDto
        {
            NombreUsuario = "nuevousuario",
            NombreCompleto = "Nuevo Usuario",
            Password = "PasswordSegura123!",
            Rol = RolUsuarioEnum.Cajero
        };

        _usuarioRepository.FindAsync(Arg.Any<Expression<Func<Usuario, bool>>>(), includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(new List<Usuario>());

        _passwordHasher.HashPassword("PasswordSegura123!")
            .Returns("hash_bcrypt_seguro");

        // Act
        var resultado = await _sut.RegistrarUsuarioAsync(dto);

        // Assert
        resultado.NombreUsuario.Should().Be("nuevousuario");
        resultado.NombreCompleto.Should().Be("Nuevo Usuario");
        resultado.Rol.Should().Be(RolUsuarioEnum.Cajero);

        await _usuarioRepository.Received(1).AddAsync(
            Arg.Is<Usuario>(u => u.NombreUsuario == "nuevousuario" && u.PasswordHash == "hash_bcrypt_seguro"),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ModificarUsuarioAsync_DatosInvalidos_LanzaValidationException()
    {
        // Arrange
        var dtoInvalido = new ModificarUsuarioDto
        {
            IdUsuario = 0,
            NombreCompleto = "",
            Rol = (RolUsuarioEnum)99
        };

        // Act
        var act = async () => await _sut.ModificarUsuarioAsync(dtoInvalido);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ModificarUsuarioAsync_UsuarioNoExiste_LanzaDomainException()
    {
        // Arrange
        var dto = new ModificarUsuarioDto
        {
            IdUsuario = 42,
            NombreCompleto = "Nombre Nuevo",
            Rol = RolUsuarioEnum.Cajero
        };

        _usuarioRepository.GetByIdAsync(42, includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns((Usuario?)null);

        // Act
        var act = async () => await _sut.ModificarUsuarioAsync(dto);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*42*");
    }

    [Fact]
    public async Task ModificarUsuarioAsync_CambiaRolDeUltimoGerente_LanzaUltimoGerenteException()
    {
        // Arrange
        var gerenteUnico = new Usuario
        {
            Id = 1,
            NombreUsuario = "admin",
            NombreCompleto = "Administrador",
            IdRol = (int)RolUsuarioEnum.Gerente
        };

        _usuarioRepository.GetByIdAsync(1, includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(gerenteUnico);

        // Al consultar otros gerentes activos, solo existe este mismo
        _usuarioRepository.FindAsync(Arg.Any<Expression<Func<Usuario, bool>>>(), includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(new List<Usuario> { gerenteUnico });

        var dto = new ModificarUsuarioDto
        {
            IdUsuario = 1,
            NombreCompleto = "Administrador Degradado",
            Rol = RolUsuarioEnum.Cajero // Intenta degradarse a Cajero
        };

        // Act
        var act = async () => await _sut.ModificarUsuarioAsync(dto);

        // Assert
        await act.Should().ThrowAsync<UltimoGerenteException>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ModificarUsuarioAsync_CambiaRolHabiendoOtroGerente_ActualizaExitosamente()
    {
        // Arrange
        var gerente1 = new Usuario { Id = 1, NombreUsuario = "gerente1", IdRol = (int)RolUsuarioEnum.Gerente };
        var gerente2 = new Usuario { Id = 2, NombreUsuario = "gerente2", IdRol = (int)RolUsuarioEnum.Gerente };

        _usuarioRepository.GetByIdAsync(1, includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(gerente1);

        _usuarioRepository.FindAsync(Arg.Any<Expression<Func<Usuario, bool>>>(), includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(new List<Usuario> { gerente1, gerente2 });

        var dto = new ModificarUsuarioDto
        {
            IdUsuario = 1,
            NombreCompleto = "Ex Gerente Ahora Encargado",
            Rol = RolUsuarioEnum.Encargado
        };

        // Act
        var resultado = await _sut.ModificarUsuarioAsync(dto);

        // Assert
        resultado.Rol.Should().Be(RolUsuarioEnum.Encargado);
        resultado.NombreCompleto.Should().Be("Ex Gerente Ahora Encargado");
        await _usuarioRepository.Received(1).UpdateAsync(gerente1, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BajaUsuarioAsync_EsUltimoGerente_LanzaUltimoGerenteException()
    {
        // Arrange
        var unicoGerente = new Usuario { Id = 1, IdRol = (int)RolUsuarioEnum.Gerente };

        _usuarioRepository.GetByIdAsync(1, includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(unicoGerente);

        _usuarioRepository.FindAsync(Arg.Any<Expression<Func<Usuario, bool>>>(), includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(new List<Usuario> { unicoGerente });

        // Act
        var act = async () => await _sut.BajaUsuarioAsync(1);

        // Assert
        await act.Should().ThrowAsync<UltimoGerenteException>();
        await _usuarioRepository.DidNotReceive().DeleteAsync(Arg.Any<Usuario>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BajaUsuarioAsync_UsuarioNormal_AplicaBajaYGuardaCambios()
    {
        // Arrange
        var cajero = new Usuario { Id = 5, IdRol = (int)RolUsuarioEnum.Cajero };

        _usuarioRepository.GetByIdAsync(5, includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(cajero);

        // Act
        await _sut.BajaUsuarioAsync(5);

        // Assert
        await _usuarioRepository.Received(1).DeleteAsync(cajero, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CambiarPasswordAsync_PasswordInvalida_LanzaValidationException()
    {
        // Arrange
        var dto = new CambiarPasswordDto
        {
            IdUsuario = 1,
            NuevaPassword = "123" // muy corta
        };

        // Act
        var act = async () => await _sut.CambiarPasswordAsync(dto);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _passwordHasher.DidNotReceive().HashPassword(Arg.Any<string>());
    }

    [Fact]
    public async Task CambiarPasswordAsync_UsuarioNoExiste_LanzaDomainException()
    {
        // Arrange
        var dto = new CambiarPasswordDto
        {
            IdUsuario = 99,
            NuevaPassword = "PasswordSegura123!"
        };

        _usuarioRepository.GetByIdAsync(99, includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns((Usuario?)null);

        // Act
        var act = async () => await _sut.CambiarPasswordAsync(dto);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*99*");
    }

    [Fact]
    public async Task CambiarPasswordAsync_PasswordValida_HasheaYActualizaUsuario()
    {
        // Arrange
        var usuario = new Usuario { Id = 3, NombreUsuario = "operador", PasswordHash = "hash_viejo" };

        _usuarioRepository.GetByIdAsync(3, includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(usuario);

        _passwordHasher.HashPassword("NuevaClave2026!")
            .Returns("nuevo_hash_bcrypt");

        var dto = new CambiarPasswordDto
        {
            IdUsuario = 3,
            NuevaPassword = "NuevaClave2026!"
        };

        // Act
        await _sut.CambiarPasswordAsync(dto);

        // Assert
        usuario.PasswordHash.Should().Be("nuevo_hash_bcrypt");
        await _usuarioRepository.Received(1).UpdateAsync(usuario, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

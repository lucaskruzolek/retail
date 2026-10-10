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

    private static Usuario CrearUsuario(
        int id,
        string nombreUsuario,
        RolUsuarioEnum rol,
        string nombre = "Juan",
        string apellido = "Pérez")
    {
        var usuario = Usuario.Crear(nombreUsuario, nombre, apellido, "hash_inicial", rol);
        usuario.Id = id;
        return usuario;
    }

    [Fact]
    public async Task ListarUsuariosAsync_DebeLlamarListAllAsyncConFalseYRetornarDtosOrdenadosPorApellido()
    {
        // Arrange
        var usuarios = new List<Usuario>
        {
            CrearUsuario(1, "jperez", RolUsuarioEnum.Cajero, "Juan", "Pérez"),
            CrearUsuario(2, "admin", RolUsuarioEnum.Gerente, "Ana", "Gómez")
        };

        _usuarioRepository.ListAllAsync(includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(usuarios);

        // Act
        var resultado = await _sut.ListarUsuariosAsync();

        // Assert
        resultado.Should().HaveCount(2);
        resultado[0].Apellido.Should().Be("Gómez"); // ordenado por apellido
        resultado[1].Apellido.Should().Be("Pérez");
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
        var usuario = CrearUsuario(5, "mlopez", RolUsuarioEnum.Encargado, "María", "López");

        _usuarioRepository.GetByIdAsync(5, includeDeleted: true, Arg.Any<CancellationToken>())
            .Returns(usuario);

        // Act
        var dto = await _sut.ObtenerPorIdAsync(5);

        // Assert
        dto.IdUsuario.Should().Be(5);
        dto.NombreUsuario.Should().Be("mlopez");
        dto.Nombre.Should().Be("María");
        dto.Apellido.Should().Be("López");
        dto.NombreCompleto.Should().Be("María López");
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
            Nombre = "",         // vacío
            Apellido = "P3rez",  // con dígitos
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
            Nombre = "Nuevo",
            Apellido = "Admin",
            Password = "PasswordSegura123!",
            Rol = RolUsuarioEnum.Gerente
        };

        _usuarioRepository.FindAsync(Arg.Any<Expression<Func<Usuario, bool>>>(), includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(new List<Usuario> { CrearUsuario(1, "admin", RolUsuarioEnum.Gerente) });

        // Act
        var act = async () => await _sut.RegistrarUsuarioAsync(dto);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*'admin'*ya se encuentra registrado*");
        await _usuarioRepository.DidNotReceive().AddAsync(Arg.Any<Usuario>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarUsuarioAsync_UsuarioExistenteEliminado_CreaNuevoUsuarioConIdentidadPropia()
    {
        // Arrange
        var dto = new CrearUsuarioDto
        {
            NombreUsuario = "lucas",
            Nombre = "Lucas",
            Apellido = "Empleado",
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
        resultado.NombreCompleto.Should().Be("Lucas Empleado");
        resultado.Rol.Should().Be(RolUsuarioEnum.Cajero);

        // Debe registrarse como una nueva entidad independiente (AddAsync), no modificar el registro histórico (UpdateAsync)
        await _usuarioRepository.Received(1).AddAsync(
            Arg.Is<Usuario>(u => u.NombreUsuario == "lucas" && u.Apellido == "Empleado" && u.PasswordHash == "hash_nuevo_seguro"),
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
            Nombre = "Nuevo",
            Apellido = "Usuario",
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
            Nombre = "",
            Apellido = "",
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
            Nombre = "Nombre",
            Apellido = "Nuevo",
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
        var gerenteUnico = CrearUsuario(1, "admin", RolUsuarioEnum.Gerente);

        _usuarioRepository.GetByIdAsync(1, includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(gerenteUnico);

        // Al consultar otros gerentes activos, solo existe este mismo
        _usuarioRepository.FindAsync(Arg.Any<Expression<Func<Usuario, bool>>>(), includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(new List<Usuario> { gerenteUnico });

        var dto = new ModificarUsuarioDto
        {
            IdUsuario = 1,
            Nombre = "Administrador",
            Apellido = "Degradado",
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
        var gerente1 = CrearUsuario(1, "gerente1", RolUsuarioEnum.Gerente);
        var gerente2 = CrearUsuario(2, "gerente2", RolUsuarioEnum.Gerente);

        _usuarioRepository.GetByIdAsync(1, includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(gerente1);

        _usuarioRepository.FindAsync(Arg.Any<Expression<Func<Usuario, bool>>>(), includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(new List<Usuario> { gerente1, gerente2 });

        var dto = new ModificarUsuarioDto
        {
            IdUsuario = 1,
            Nombre = "ex gerente",
            Apellido = "ahora encargado",
            Rol = RolUsuarioEnum.Encargado
        };

        // Act
        var resultado = await _sut.ModificarUsuarioAsync(dto);

        // Assert
        resultado.Rol.Should().Be(RolUsuarioEnum.Encargado);
        resultado.Nombre.Should().Be("Ex Gerente"); // llega normalizado desde el agregado
        resultado.Apellido.Should().Be("Ahora Encargado");
        await _usuarioRepository.Received(1).UpdateAsync(gerente1, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BajaUsuarioAsync_EsUltimoGerente_LanzaUltimoGerenteException()
    {
        // Arrange
        var unicoGerente = CrearUsuario(1, "admin", RolUsuarioEnum.Gerente);

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
        var cajero = CrearUsuario(5, "cajero", RolUsuarioEnum.Cajero);

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
        var usuario = CrearUsuario(3, "operador", RolUsuarioEnum.Cajero);

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

    [Fact]
    public async Task RegistrarUsuarioAsync_NombreUsuarioConMayusculas_LoBuscaYGuardaEnMinusculas()
    {
        // Arrange
        var dto = new CrearUsuarioDto
        {
            NombreUsuario = "  JPerez ",
            Nombre = "juan",
            Apellido = "PÉREZ",
            Password = "PasswordSegura123!",
            Rol = RolUsuarioEnum.Cajero
        };

        Expression<Func<Usuario, bool>>? filtroDuplicados = null;
        _usuarioRepository.FindAsync(
                Arg.Do<Expression<Func<Usuario, bool>>>(filtro => filtroDuplicados = filtro),
                includeDeleted: false,
                Arg.Any<CancellationToken>())
            .Returns(new List<Usuario>());
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("hash");

        // Act
        var resultado = await _sut.RegistrarUsuarioAsync(dto);

        // Assert: el duplicado se busca por la forma canónica, no por lo que escribió el usuario.
        var esDuplicado = filtroDuplicados!.Compile();
        esDuplicado(CrearUsuario(9, "jperez", RolUsuarioEnum.Cajero)).Should().BeTrue();
        resultado.NombreUsuario.Should().Be("jperez");
        resultado.Nombre.Should().Be("Juan");
        resultado.Apellido.Should().Be("Pérez");
    }

    [Theory]
    [InlineData("operador")]
    [InlineData("OPERADOR")]
    public async Task CambiarPasswordAsync_IgualAlNombreUsuario_LanzaValidationExceptionSinGuardar(string nuevaPassword)
    {
        // Arrange
        var usuario = CrearUsuario(3, "operador", RolUsuarioEnum.Cajero);
        _usuarioRepository.GetByIdAsync(3, includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(usuario);

        var dto = new CambiarPasswordDto { IdUsuario = 3, NuevaPassword = nuevaPassword };

        // Act
        var act = async () => await _sut.CambiarPasswordAsync(dto);

        // Assert
        await act.Should().ThrowAsync<ValidationException>().WithMessage("*igual al nombre de usuario*");
        _passwordHasher.DidNotReceive().HashPassword(Arg.Any<string>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

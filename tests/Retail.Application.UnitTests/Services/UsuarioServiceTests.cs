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
    // Actores de referencia: el servicio los lee del repositorio para conocer su rol vigente.
    private const int IdGerente = 100;
    private const int IdAdministrador = 200;
    private const int IdCajeroActor = 300;

    private readonly IRepository<Usuario> _usuarioRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly UsuarioService _sut;

    public UsuarioServiceTests()
    {
        _usuarioRepository = Substitute.For<IRepository<Usuario>>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _passwordHasher = Substitute.For<IPasswordHasher>();

        _sut = new UsuarioService(
            _usuarioRepository,
            _unitOfWork,
            _passwordHasher,
            new CrearUsuarioValidator(),
            new ModificarUsuarioValidator(),
            new CambiarPasswordValidator());

        RegistrarEnRepositorio(CrearUsuario(IdGerente, "gerente", RolUsuarioEnum.Gerente));
        RegistrarEnRepositorio(CrearUsuario(IdAdministrador, "soporte", RolUsuarioEnum.Administrador));
        RegistrarEnRepositorio(CrearUsuario(IdCajeroActor, "cajero.actor", RolUsuarioEnum.Cajero));
    }

    private static Usuario CrearUsuario(
        int id,
        string nombreUsuario,
        RolUsuarioEnum rol,
        string nombre = "Juan",
        string apellido = "Pérez")
    {
        var email = rol == RolUsuarioEnum.Gerente ? $"{nombreUsuario}@libreria.com" : null;
        var usuario = Usuario.Crear(nombreUsuario, nombre, apellido, "hash_inicial", rol, email);
        usuario.Id = id;
        return usuario;
    }

    private Usuario RegistrarEnRepositorio(Usuario usuario)
    {
        _usuarioRepository.GetByIdAsync(usuario.Id, includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(usuario);
        return usuario;
    }

    private static CrearUsuarioDto AltaDe(RolUsuarioEnum rol, string? email = null) => new()
    {
        NombreUsuario = "nuevousuario",
        Nombre = "Nuevo",
        Apellido = "Usuario",
        Password = "PasswordSegura123!",
        Rol = rol,
        EmailRecuperacion = email
    };

    private void SinDuplicados()
    {
        _usuarioRepository.FindAsync(Arg.Any<Expression<Func<Usuario, bool>>>(), includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(new List<Usuario>());
    }

    // ───────────────────────────── Lectura ─────────────────────────────

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
        var resultado = await _sut.ListarUsuariosAsync(IdGerente);

        // Assert
        resultado.Should().HaveCount(2);
        resultado[0].Apellido.Should().Be("Gómez"); // ordenado por apellido
        resultado[1].Apellido.Should().Be("Pérez");
        resultado[0].Rol.Should().Be(RolUsuarioEnum.Gerente);
        resultado[0].EmailRecuperacion.Should().Be("admin@libreria.com");
        resultado[1].Rol.Should().Be(RolUsuarioEnum.Cajero);
        resultado[1].DebeCambiarPassword.Should().BeTrue();
        await _usuarioRepository.Received(1).ListAllAsync(includeDeleted: false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListarUsuariosAsync_ActorAdministrador_PuedeConsultarLaLista()
    {
        _usuarioRepository.ListAllAsync(includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(new List<Usuario>());

        var act = async () => await _sut.ListarUsuariosAsync(IdAdministrador);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ListarUsuariosAsync_ActorCajero_LanzaOperacionNoAutorizadaSinConsultarUsuarios()
    {
        // Act
        var act = async () => await _sut.ListarUsuariosAsync(IdCajeroActor);

        // Assert
        await act.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _usuarioRepository.DidNotReceive().ListAllAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public async Task ListarUsuariosAsync_ActorInexistenteOInactivo_LanzaOperacionNoAutorizada(int idActor)
    {
        var act = async () => await _sut.ListarUsuariosAsync(idActor);

        await act.Should().ThrowAsync<OperacionNoAutorizadaException>();
    }

    [Fact]
    public async Task ObtenerRolesAsignablesAsync_ActorGerente_DevuelveLosCuatroRoles()
    {
        var roles = await _sut.ObtenerRolesAsignablesAsync(IdGerente);

        roles.Should().Equal(Enum.GetValues<RolUsuarioEnum>());
    }

    [Fact]
    public async Task ObtenerRolesAsignablesAsync_ActorAdministrador_DevuelveSoloCajeroYEncargado()
    {
        var roles = await _sut.ObtenerRolesAsignablesAsync(IdAdministrador);

        roles.Should().Equal(RolUsuarioEnum.Cajero, RolUsuarioEnum.Encargado);
    }

    [Fact]
    public async Task ObtenerRolesAsignablesAsync_ActorCajero_DevuelveListaVacia()
    {
        var roles = await _sut.ObtenerRolesAsignablesAsync(IdCajeroActor);

        roles.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ObtenerPorIdAsync_IdInvalido_LanzaArgumentOutOfRangeException(int idInvalido)
    {
        // Act
        var act = async () => await _sut.ObtenerPorIdAsync(IdGerente, idInvalido);

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
        var act = async () => await _sut.ObtenerPorIdAsync(IdGerente, 99);

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
        var dto = await _sut.ObtenerPorIdAsync(IdGerente, 5);

        // Assert
        dto.IdUsuario.Should().Be(5);
        dto.NombreUsuario.Should().Be("mlopez");
        dto.Nombre.Should().Be("María");
        dto.Apellido.Should().Be("López");
        dto.NombreCompleto.Should().Be("María López");
        dto.Rol.Should().Be(RolUsuarioEnum.Encargado);
        dto.Activo.Should().BeTrue();
    }

    // ───────────────────────────── Alta ─────────────────────────────

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
            Rol = (RolUsuarioEnum)99, // inválido
            EmailRecuperacion = null
        };

        // Act
        var act = async () => await _sut.RegistrarUsuarioAsync(IdGerente, dtoInvalido);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        await _usuarioRepository.DidNotReceive().AddAsync(Arg.Any<Usuario>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarUsuarioAsync_NombreUsuarioDuplicado_LanzaDomainException()
    {
        // Arrange
        var dto = AltaDe(RolUsuarioEnum.Gerente, "nuevo@libreria.com") with { NombreUsuario = "Admin" };

        _usuarioRepository.FindAsync(Arg.Any<Expression<Func<Usuario, bool>>>(), includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(new List<Usuario> { CrearUsuario(1, "admin", RolUsuarioEnum.Gerente) });

        // Act
        var act = async () => await _sut.RegistrarUsuarioAsync(IdGerente, dto);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*'admin'*ya se encuentra registrado*");
        await _usuarioRepository.DidNotReceive().AddAsync(Arg.Any<Usuario>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarUsuarioAsync_UsuarioExistenteEliminado_CreaNuevoUsuarioConIdentidadPropia()
    {
        // Arrange
        var dto = AltaDe(RolUsuarioEnum.Cajero) with
        {
            NombreUsuario = "lucas",
            Nombre = "Lucas",
            Apellido = "Empleado",
            Password = "NuevaPassword123!"
        };

        // Al buscar usuarios activos (includeDeleted: false), no retorna ninguno porque el anterior está en soft-delete
        SinDuplicados();
        _passwordHasher.HashPassword("NuevaPassword123!").Returns("hash_nuevo_seguro");

        // Act
        var resultado = await _sut.RegistrarUsuarioAsync(IdGerente, dto);

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
    public async Task RegistrarUsuarioAsync_DatosValidos_HasheaPasswordGuardaEmailYExigeCambioDePassword()
    {
        // Arrange
        SinDuplicados();
        _passwordHasher.HashPassword("PasswordSegura123!").Returns("hash_bcrypt_seguro");

        // Act
        var resultado = await _sut.RegistrarUsuarioAsync(IdGerente, AltaDe(RolUsuarioEnum.Cajero, "Nuevo@Libreria.com"));

        // Assert
        resultado.NombreUsuario.Should().Be("nuevousuario");
        resultado.NombreCompleto.Should().Be("Nuevo Usuario");
        resultado.Rol.Should().Be(RolUsuarioEnum.Cajero);
        resultado.EmailRecuperacion.Should().Be("nuevo@libreria.com");
        resultado.DebeCambiarPassword.Should().BeTrue();

        await _usuarioRepository.Received(1).AddAsync(
            Arg.Is<Usuario>(u => u.NombreUsuario == "nuevousuario" && u.PasswordHash == "hash_bcrypt_seguro"),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarUsuarioAsync_NombreUsuarioConMayusculas_LoBuscaYGuardaEnMinusculas()
    {
        // Arrange
        var dto = AltaDe(RolUsuarioEnum.Cajero) with { NombreUsuario = "  JPerez ", Nombre = "juan", Apellido = "PÉREZ" };

        Expression<Func<Usuario, bool>>? filtroDuplicados = null;
        _usuarioRepository.FindAsync(
                Arg.Do<Expression<Func<Usuario, bool>>>(filtro => filtroDuplicados = filtro),
                includeDeleted: false,
                Arg.Any<CancellationToken>())
            .Returns(new List<Usuario>());
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("hash");

        // Act
        var resultado = await _sut.RegistrarUsuarioAsync(IdGerente, dto);

        // Assert: el duplicado se busca por la forma canónica, no por lo que escribió el usuario.
        var esDuplicado = filtroDuplicados!.Compile();
        esDuplicado(CrearUsuario(9, "jperez", RolUsuarioEnum.Cajero)).Should().BeTrue();
        resultado.NombreUsuario.Should().Be("jperez");
        resultado.Nombre.Should().Be("Juan");
        resultado.Apellido.Should().Be("Pérez");
    }

    [Theory]
    [InlineData(RolUsuarioEnum.Cajero)]
    [InlineData(RolUsuarioEnum.Encargado)]
    public async Task RegistrarUsuarioAsync_AdministradorCreaRolOperativo_RegistraElUsuario(RolUsuarioEnum rol)
    {
        // Arrange
        SinDuplicados();
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("hash");

        // Act
        var resultado = await _sut.RegistrarUsuarioAsync(IdAdministrador, AltaDe(rol));

        // Assert
        resultado.Rol.Should().Be(rol);
        await _usuarioRepository.Received(1).AddAsync(Arg.Any<Usuario>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(RolUsuarioEnum.Gerente)]
    [InlineData(RolUsuarioEnum.Administrador)]
    public async Task RegistrarUsuarioAsync_AdministradorCreaRolConAutoridad_LanzaOperacionNoAutorizadaSinGuardar(RolUsuarioEnum rol)
    {
        // Act
        var act = async () => await _sut.RegistrarUsuarioAsync(IdAdministrador, AltaDe(rol, "nuevo@libreria.com"));

        // Assert: el Administrador no puede crear cuentas con más poder que la suya (escalada de privilegios).
        await act.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _usuarioRepository.DidNotReceive().AddAsync(Arg.Any<Usuario>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarUsuarioAsync_GerenteCreaAdministrador_RegistraElUsuario()
    {
        SinDuplicados();
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("hash");

        var resultado = await _sut.RegistrarUsuarioAsync(IdGerente, AltaDe(RolUsuarioEnum.Administrador));

        resultado.Rol.Should().Be(RolUsuarioEnum.Administrador);
    }

    [Fact]
    public async Task RegistrarUsuarioAsync_ActorCajero_LanzaOperacionNoAutorizada()
    {
        var act = async () => await _sut.RegistrarUsuarioAsync(IdCajeroActor, AltaDe(RolUsuarioEnum.Cajero));

        await act.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _usuarioRepository.DidNotReceive().AddAsync(Arg.Any<Usuario>(), Arg.Any<CancellationToken>());
    }

    // ───────────────────────────── Modificación ─────────────────────────────

    [Fact]
    public async Task ModificarUsuarioAsync_DatosInvalidos_LanzaValidationException()
    {
        // Arrange
        var dtoInvalido = new ModificarUsuarioDto
        {
            IdUsuario = 0,
            Nombre = "",
            Apellido = "",
            Rol = (RolUsuarioEnum)99,
            EmailRecuperacion = null
        };

        // Act
        var act = async () => await _sut.ModificarUsuarioAsync(IdGerente, dtoInvalido);

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
            Rol = RolUsuarioEnum.Cajero,
            EmailRecuperacion = null
        };

        _usuarioRepository.GetByIdAsync(42, includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns((Usuario?)null);

        // Act
        var act = async () => await _sut.ModificarUsuarioAsync(IdGerente, dto);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*42*");
    }

    [Fact]
    public async Task ModificarUsuarioAsync_CambiaRolDeUltimoGerente_LanzaUltimoGerenteException()
    {
        // Arrange: el único Gerente intenta degradarse a sí mismo.
        var gerenteUnico = RegistrarEnRepositorio(CrearUsuario(1, "admin", RolUsuarioEnum.Gerente));

        _usuarioRepository.FindAsync(Arg.Any<Expression<Func<Usuario, bool>>>(), includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(new List<Usuario> { gerenteUnico });

        var dto = new ModificarUsuarioDto
        {
            IdUsuario = 1,
            Nombre = "Administrador",
            Apellido = "Degradado",
            Rol = RolUsuarioEnum.Cajero,
            EmailRecuperacion = null
        };

        // Act
        var act = async () => await _sut.ModificarUsuarioAsync(1, dto);

        // Assert
        await act.Should().ThrowAsync<UltimoGerenteException>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ModificarUsuarioAsync_CambiaRolHabiendoOtroGerente_ActualizaExitosamente()
    {
        // Arrange
        var gerente1 = RegistrarEnRepositorio(CrearUsuario(1, "gerente1", RolUsuarioEnum.Gerente));
        var gerente2 = CrearUsuario(2, "gerente2", RolUsuarioEnum.Gerente);

        _usuarioRepository.FindAsync(Arg.Any<Expression<Func<Usuario, bool>>>(), includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(new List<Usuario> { gerente1, gerente2 });

        var dto = new ModificarUsuarioDto
        {
            IdUsuario = 1,
            Nombre = "ex gerente",
            Apellido = "ahora encargado",
            Rol = RolUsuarioEnum.Encargado,
            EmailRecuperacion = null
        };

        // Act
        var resultado = await _sut.ModificarUsuarioAsync(IdGerente, dto);

        // Assert
        resultado.Rol.Should().Be(RolUsuarioEnum.Encargado);
        resultado.Nombre.Should().Be("Ex Gerente"); // llega normalizado desde el agregado
        resultado.Apellido.Should().Be("Ahora Encargado");
        resultado.EmailRecuperacion.Should().BeNull();
        await _usuarioRepository.Received(1).UpdateAsync(gerente1, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ModificarUsuarioAsync_AdministradorEditaAUnGerente_LanzaOperacionNoAutorizada()
    {
        // Arrange
        RegistrarEnRepositorio(CrearUsuario(1, "otro.gerente", RolUsuarioEnum.Gerente));
        var dto = new ModificarUsuarioDto
        {
            IdUsuario = 1,
            Nombre = "Juan",
            Apellido = "Pérez",
            Rol = RolUsuarioEnum.Gerente,
            EmailRecuperacion = "otro@libreria.com"
        };

        // Act
        var act = async () => await _sut.ModificarUsuarioAsync(IdAdministrador, dto);

        // Assert
        await act.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ModificarUsuarioAsync_AdministradorAsciendeCajeroAGerente_LanzaOperacionNoAutorizadaSinModificar()
    {
        // Arrange
        var cajero = RegistrarEnRepositorio(CrearUsuario(5, "cajero", RolUsuarioEnum.Cajero));
        var dto = new ModificarUsuarioDto
        {
            IdUsuario = 5,
            Nombre = "Juan",
            Apellido = "Pérez",
            Rol = RolUsuarioEnum.Gerente,
            EmailRecuperacion = "cajero@libreria.com"
        };

        // Act
        var act = async () => await _sut.ModificarUsuarioAsync(IdAdministrador, dto);

        // Assert
        await act.Should().ThrowAsync<OperacionNoAutorizadaException>();
        cajero.IdRol.Should().Be((int)RolUsuarioEnum.Cajero);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ModificarUsuarioAsync_AdministradorEditaEncargado_ActualizaExitosamente()
    {
        // Arrange
        RegistrarEnRepositorio(CrearUsuario(5, "encargado", RolUsuarioEnum.Encargado));
        var dto = new ModificarUsuarioDto
        {
            IdUsuario = 5,
            Nombre = "Laura",
            Apellido = "Ríos",
            Rol = RolUsuarioEnum.Cajero,
            EmailRecuperacion = null
        };

        // Act
        var resultado = await _sut.ModificarUsuarioAsync(IdAdministrador, dto);

        // Assert
        resultado.Rol.Should().Be(RolUsuarioEnum.Cajero);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ───────────────────────────── Baja ─────────────────────────────

    [Fact]
    public async Task BajaUsuarioAsync_EsUltimoGerente_LanzaUltimoGerenteException()
    {
        // Arrange
        var unicoGerente = RegistrarEnRepositorio(CrearUsuario(1, "admin", RolUsuarioEnum.Gerente));

        _usuarioRepository.FindAsync(Arg.Any<Expression<Func<Usuario, bool>>>(), includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(new List<Usuario> { unicoGerente });

        // Act
        var act = async () => await _sut.BajaUsuarioAsync(1, 1);

        // Assert
        await act.Should().ThrowAsync<UltimoGerenteException>();
        await _usuarioRepository.DidNotReceive().DeleteAsync(Arg.Any<Usuario>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BajaUsuarioAsync_UsuarioNormal_AplicaBajaYGuardaCambios()
    {
        // Arrange
        var cajero = RegistrarEnRepositorio(CrearUsuario(5, "cajero", RolUsuarioEnum.Cajero));

        // Act
        await _sut.BajaUsuarioAsync(IdGerente, 5);

        // Assert
        await _usuarioRepository.Received(1).DeleteAsync(cajero, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BajaUsuarioAsync_UltimoAdministrador_AplicaBajaPorqueElRolEsOpcional()
    {
        // Arrange: no existe la invariante "último Administrador" (D-5).
        var administrador = RegistrarEnRepositorio(CrearUsuario(7, "unico.soporte", RolUsuarioEnum.Administrador));

        // Act
        await _sut.BajaUsuarioAsync(IdGerente, 7);

        // Assert
        await _usuarioRepository.Received(1).DeleteAsync(administrador, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BajaUsuarioAsync_AdministradorDaDeBajaAOtroAdministrador_LanzaOperacionNoAutorizada()
    {
        // Arrange
        RegistrarEnRepositorio(CrearUsuario(7, "otro.soporte", RolUsuarioEnum.Administrador));

        // Act
        var act = async () => await _sut.BajaUsuarioAsync(IdAdministrador, 7);

        // Assert
        await act.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _usuarioRepository.DidNotReceive().DeleteAsync(Arg.Any<Usuario>(), Arg.Any<CancellationToken>());
    }

    // ───────────────────────────── Desbloqueo (D-31) ─────────────────────────────

    private static Usuario Bloquear(Usuario administrador)
    {
        for (var i = 0; i < Usuario.MaximoIntentosAutorizacion; i++)
        {
            administrador.RegistrarIntentoDeAutorizacion();
        }

        return administrador;
    }

    [Fact]
    public async Task DesbloquearUsuarioAsync_Gerente_DesbloqueaAlAdministrador()
    {
        // Arrange
        var bloqueado = RegistrarEnRepositorio(Bloquear(CrearUsuario(7, "bloqueado", RolUsuarioEnum.Administrador)));

        // Act
        await _sut.DesbloquearUsuarioAsync(IdGerente, 7);

        // Assert
        bloqueado.IntentosAutorizacionFallidos.Should().Be(0);
        bloqueado.EstaBloqueada.Should().BeFalse();
        await _usuarioRepository.Received(1).UpdateAsync(bloqueado, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DesbloquearUsuarioAsync_Administrador_LanzaOperacionNoAutorizada()
    {
        // Arrange: un Administrador bloqueado no puede desbloquearse a sí mismo ni a otro.
        Bloquear(RegistrarEnRepositorio(CrearUsuario(7, "bloqueado", RolUsuarioEnum.Administrador)));

        // Act
        var act = async () => await _sut.DesbloquearUsuarioAsync(IdAdministrador, 7);

        // Assert: la autoridad se verifica antes de buscar al objetivo.
        await act.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _usuarioRepository.DidNotReceive().GetByIdAsync(7, Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DesbloquearUsuarioAsync_CuentaNoBloqueada_LanzaDomainException()
    {
        // Arrange
        RegistrarEnRepositorio(CrearUsuario(7, "libre", RolUsuarioEnum.Administrador));

        // Act
        var act = async () => await _sut.DesbloquearUsuarioAsync(IdGerente, 7);

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*no está bloqueada*");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ───────────────────────────── Contraseñas ─────────────────────────────

    [Fact]
    public async Task CambiarPasswordAsync_PasswordInvalida_LanzaValidationException()
    {
        // Arrange
        var dto = new CambiarPasswordDto { IdUsuario = 1, NuevaPassword = "123" }; // muy corta

        // Act
        var act = async () => await _sut.CambiarPasswordAsync(IdGerente, dto);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _passwordHasher.DidNotReceive().HashPassword(Arg.Any<string>());
    }

    [Fact]
    public async Task CambiarPasswordAsync_UsuarioNoExiste_LanzaDomainException()
    {
        // Arrange
        var dto = new CambiarPasswordDto { IdUsuario = 99, NuevaPassword = "PasswordSegura123!" };

        _usuarioRepository.GetByIdAsync(99, includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns((Usuario?)null);

        // Act
        var act = async () => await _sut.CambiarPasswordAsync(IdGerente, dto);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*99*");
    }

    [Fact]
    public async Task CambiarPasswordAsync_GerenteBlanqueaOtraCuenta_HasheaYExigeCambioAlTitular()
    {
        // Arrange
        var usuario = RegistrarEnRepositorio(CrearUsuario(3, "operador", RolUsuarioEnum.Cajero));
        usuario.CambiarPasswordPropia("hash_del_titular");
        _passwordHasher.HashPassword("NuevaClave2026!").Returns("nuevo_hash_bcrypt");

        // Act
        await _sut.CambiarPasswordAsync(IdGerente, new CambiarPasswordDto { IdUsuario = 3, NuevaPassword = "NuevaClave2026!" });

        // Assert
        usuario.PasswordHash.Should().Be("nuevo_hash_bcrypt");
        usuario.DebeCambiarPassword.Should().BeTrue();
        await _usuarioRepository.Received(1).UpdateAsync(usuario, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CambiarPasswordAsync_TitularCambiaSuPropiaPassword_LevantaLaObligacionSinRequerirRolDeGestion()
    {
        // Arrange: un Cajero no gestiona cuentas, pero sí puede cambiar la suya (login forzado, D-6).
        var cajero = RegistrarEnRepositorio(CrearUsuario(IdCajeroActor, "cajero.actor", RolUsuarioEnum.Cajero));
        _passwordHasher.VerifyPassword("Temporal2026!", "hash_inicial").Returns(true);
        _passwordHasher.HashPassword("MiClave2026!").Returns("hash_propio");

        // Act
        await _sut.CambiarPasswordAsync(
            IdCajeroActor,
            new CambiarPasswordDto { IdUsuario = IdCajeroActor, NuevaPassword = "MiClave2026!", PasswordActual = "Temporal2026!" });

        // Assert
        cajero.PasswordHash.Should().Be("hash_propio");
        cajero.DebeCambiarPassword.Should().BeFalse();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CambiarPasswordAsync_AdministradorBlanqueaAUnGerente_LanzaOperacionNoAutorizada()
    {
        // Arrange
        var gerente = RegistrarEnRepositorio(CrearUsuario(1, "otro.gerente", RolUsuarioEnum.Gerente));

        // Act
        var act = async () => await _sut.CambiarPasswordAsync(
            IdAdministrador, new CambiarPasswordDto { IdUsuario = 1, NuevaPassword = "NuevaClave2026!" });

        // Assert
        await act.Should().ThrowAsync<OperacionNoAutorizadaException>();
        gerente.PasswordHash.Should().Be("hash_inicial");
        _passwordHasher.DidNotReceive().HashPassword(Arg.Any<string>());
    }

    [Fact]
    public async Task CambiarPasswordAsync_CajeroBlanqueaOtraCuenta_LanzaOperacionNoAutorizada()
    {
        // Arrange
        RegistrarEnRepositorio(CrearUsuario(3, "operador", RolUsuarioEnum.Cajero));

        // Act
        var act = async () => await _sut.CambiarPasswordAsync(
            IdCajeroActor, new CambiarPasswordDto { IdUsuario = 3, NuevaPassword = "NuevaClave2026!" });

        // Assert
        await act.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("operador")]
    [InlineData("OPERADOR")]
    public async Task CambiarPasswordAsync_IgualAlNombreUsuario_LanzaValidationExceptionSinGuardar(string nuevaPassword)
    {
        // Arrange
        RegistrarEnRepositorio(CrearUsuario(3, "operador", RolUsuarioEnum.Cajero));

        var dto = new CambiarPasswordDto { IdUsuario = 3, NuevaPassword = nuevaPassword };

        // Act
        var act = async () => await _sut.CambiarPasswordAsync(IdGerente, dto);

        // Assert
        await act.Should().ThrowAsync<ValidationException>().WithMessage("*igual al nombre de usuario*");
        _passwordHasher.DidNotReceive().HashPassword(Arg.Any<string>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("incorrecta")]
    public async Task CambiarPasswordAsync_CambioPropioSinPasswordActualValida_LanzaValidationExceptionSinGuardar(string? passwordActual)
    {
        // Arrange: alguien usa una sesión abierta para cambiar la contraseña del titular sin conocer la actual.
        RegistrarEnRepositorio(CrearUsuario(IdGerente, "gerente", RolUsuarioEnum.Gerente));
        _passwordHasher.VerifyPassword("incorrecta", Arg.Any<string>()).Returns(false);

        var dto = new CambiarPasswordDto { IdUsuario = IdGerente, NuevaPassword = "Robada2026!", PasswordActual = passwordActual };

        // Act
        var act = async () => await _sut.CambiarPasswordAsync(IdGerente, dto);

        // Assert
        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CambiarPasswordDto.PasswordActual));
        _passwordHasher.DidNotReceive().HashPassword(Arg.Any<string>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CambiarPasswordAsync_CambioPropioConLaMismaPassword_LanzaValidationExceptionYMantieneLaObligacion()
    {
        // Arrange: en el cambio obligatorio, el titular intenta conservar la contraseña temporal que eligió otro.
        var cajero = RegistrarEnRepositorio(CrearUsuario(IdCajeroActor, "cajero.actor", RolUsuarioEnum.Cajero));
        _passwordHasher.VerifyPassword("Temporal2026!", "hash_inicial").Returns(true);

        var dto = new CambiarPasswordDto { IdUsuario = IdCajeroActor, NuevaPassword = "Temporal2026!", PasswordActual = "Temporal2026!" };

        // Act
        var act = async () => await _sut.CambiarPasswordAsync(IdCajeroActor, dto);

        // Assert
        await act.Should().ThrowAsync<ValidationException>().WithMessage("*distinta de la actual*");
        cajero.DebeCambiarPassword.Should().BeTrue();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CambiarPasswordAsync_CajeroSobreUnaCuentaInexistente_LanzaOperacionNoAutorizadaSinRevelarSiExiste()
    {
        // Act: si el servicio buscara primero al objetivo, el mensaje "no encontrado" revelaría qué IDs existen.
        var act = async () => await _sut.CambiarPasswordAsync(
            IdCajeroActor, new CambiarPasswordDto { IdUsuario = 999, NuevaPassword = "NuevaClave2026!" });

        // Assert
        await act.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _usuarioRepository.DidNotReceive().GetByIdAsync(999, Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListarUsuariosAsync_ActorAdministrador_OcultaElEmailDeLasCuentasQueNoGestiona()
    {
        // Arrange
        var gerente = CrearUsuario(1, "ana.gerente", RolUsuarioEnum.Gerente);
        var cajero = Usuario.Crear("juan.cajero", "Juan", "Pérez", "hash", RolUsuarioEnum.Cajero, "juan@libreria.com");
        cajero.Id = 2;
        _usuarioRepository.ListAllAsync(includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(new List<Usuario> { gerente, cajero });

        // Act
        var resultado = await _sut.ListarUsuariosAsync(IdAdministrador);

        // Assert: el email del Gerente es su canal de recuperación; el Administrador no tiene por qué conocerlo.
        resultado.Single(u => u.IdUsuario == 1).EmailRecuperacion.Should().BeNull();
        resultado.Single(u => u.IdUsuario == 2).EmailRecuperacion.Should().Be("juan@libreria.com");
    }
}

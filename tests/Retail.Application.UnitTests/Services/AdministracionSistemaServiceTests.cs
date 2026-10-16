using System.Linq.Expressions;
using FluentAssertions;
using FluentValidation;
using NSubstitute;
using Retail.Application.DTOs.Backups;
using Retail.Application.DTOs.Sistema;
using Retail.Application.DTOs.Usuarios;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Application.Services;
using Retail.Application.Validators.Usuarios;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Application.UnitTests.Services;

public class AdministracionSistemaServiceTests
{
    private readonly IEsquemaBaseDatos _esquema = Substitute.For<IEsquemaBaseDatos>();
    private readonly IBackupService _backupService = Substitute.For<IBackupService>();
    private readonly IRepository<Usuario> _usuarioRepository = Substitute.For<IRepository<Usuario>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly AdministracionSistemaService _sut;

    public AdministracionSistemaServiceTests()
    {
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns(callInfo => $"hash({callInfo.Arg<string>()})");

        _sut = new AdministracionSistemaService(
            _esquema,
            _backupService,
            _usuarioRepository,
            _unitOfWork,
            _passwordHasher,
            new CrearUsuarioValidator());
    }

    private static CrearUsuarioDto PrimerGerente(string? email = "lucas@libreria.com") => new()
    {
        NombreUsuario = "LKruzolek",
        Nombre = "lucas",
        Apellido = "kruzolek",
        Password = "Clave2026!",
        Rol = RolUsuarioEnum.Gerente,
        EmailRecuperacion = email
    };

    private void UsuariosEncontrados(params Usuario[] usuarios)
    {
        _usuarioRepository.FindAsync(Arg.Any<Expression<Func<Usuario, bool>>>(), includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(usuarios.ToList());
    }

    private static EstadoEsquemaDto Esquema(bool conecta = true, int pendientes = 0, int desconocidas = 0) => new()
    {
        PuedeConectar = conecta,
        MigracionesPendientes = Enumerable.Range(1, pendientes).Select(i => $"M{i}").ToList(),
        MigracionesDesconocidas = Enumerable.Range(1, desconocidas).Select(i => $"Futura{i}").ToList()
    };

    private static ResultadoBackupDto CopiaPrevia() => new()
    {
        Referencia = Guid.NewGuid(),
        RutaArchivo = @"C:\RetailData\Backups\pre.bak",
        TamanoBytes = 1024,
        HashSha256 = new string('a', 64),
        RegistradaEnHistorial = true
    };

    [Fact]
    public async Task MigrarAsync_BaseConDatosYMigracionesPendientes_CopiaAntesDeMigrar()
    {
        // Arrange
        _esquema.ObtenerEstadoAsync(Arg.Any<CancellationToken>()).Returns(Esquema(pendientes: 2));
        _backupService.CrearBackupDelSistemaAsync(TipoBackupEnum.PreMigracion, Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .Returns(CopiaPrevia());
        _esquema.AplicarMigracionesPendientesAsync(Arg.Any<CancellationToken>()).Returns(["M1", "M2"]);

        // Act
        var resultado = await _sut.MigrarAsync();

        // Assert: el orden importa, la copia es la red de seguridad de la migración (D-10).
        Received.InOrder(() =>
        {
            _backupService.CrearBackupDelSistemaAsync(TipoBackupEnum.PreMigracion, Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>());
            _esquema.AplicarMigracionesPendientesAsync(Arg.Any<CancellationToken>());
        });
        resultado.Aplicadas.Should().Equal("M1", "M2");
        resultado.CopiaPrevia.Should().NotBeNull();
    }

    [Fact]
    public async Task MigrarAsync_LaCopiaPreviaFalla_NoAplicaNingunaMigracion()
    {
        // Arrange
        _esquema.ObtenerEstadoAsync(Arg.Any<CancellationToken>()).Returns(Esquema(pendientes: 1));
        _backupService.CrearBackupDelSistemaAsync(Arg.Any<TipoBackupEnum>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .Returns<ResultadoBackupDto>(_ => throw new BackupFallidoException("Disco lleno"));

        // Act
        var act = async () => await _sut.MigrarAsync();

        // Assert: nunca se migra sin red de seguridad.
        await act.Should().ThrowAsync<BackupFallidoException>();
        await _esquema.DidNotReceive().AplicarMigracionesPendientesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MigrarAsync_BaseInexistente_MigraSinCopiaPrevia()
    {
        // Una base que no existe no tiene datos que respaldar.
        _esquema.ObtenerEstadoAsync(Arg.Any<CancellationToken>()).Returns(Esquema(conecta: false, pendientes: 15));
        _esquema.AplicarMigracionesPendientesAsync(Arg.Any<CancellationToken>()).Returns(["M1"]);

        var resultado = await _sut.MigrarAsync();

        resultado.CopiaPrevia.Should().BeNull();
        await _backupService.DidNotReceiveWithAnyArgs().CrearBackupDelSistemaAsync(default);
    }

    [Fact]
    public async Task MigrarAsync_BaseMasNuevaQueLaApp_LanzaDomainExceptionSinCopiarNiMigrar()
    {
        _esquema.ObtenerEstadoAsync(Arg.Any<CancellationToken>()).Returns(Esquema(pendientes: 1, desconocidas: 1));

        var act = async () => await _sut.MigrarAsync();

        await act.Should().ThrowAsync<DomainException>().WithMessage("*más nueva*");
        await _backupService.DidNotReceiveWithAnyArgs().CrearBackupDelSistemaAsync(default);
        await _esquema.DidNotReceive().AplicarMigracionesPendientesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrearPrimerGerenteAsync_SinGerentes_CreaLaCuentaSinExigirCambioDePassword()
    {
        // Arrange
        UsuariosEncontrados();

        // Act
        var gerente = await _sut.CrearPrimerGerenteAsync(PrimerGerente());

        // Assert: la contraseña la eligió el titular frente al equipo, nadie más la conoce.
        gerente.NombreUsuario.Should().Be("lkruzolek");
        gerente.NombreCompleto.Should().Be("Lucas Kruzolek");
        gerente.DebeCambiarPassword.Should().BeFalse();
        await _usuarioRepository.Received(1).AddAsync(
            Arg.Is<Usuario>(u => u.EsGerente() && u.PasswordHash == "hash(Clave2026!)" && u.EmailRecuperacion == "lucas@libreria.com"),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrearPrimerGerenteAsync_YaExisteUnGerente_LanzaOperacionNoAutorizadaSinCrear()
    {
        // Arrange: inicializar no debe servir para agregar Gerentes salteando la autorización de la aplicación.
        UsuariosEncontrados(Usuario.Crear("ana", "Ana", "Gómez", "hash", RolUsuarioEnum.Gerente, "ana@libreria.com"));

        // Act
        var act = async () => await _sut.CrearPrimerGerenteAsync(PrimerGerente());

        // Assert
        await act.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _usuarioRepository.DidNotReceive().AddAsync(Arg.Any<Usuario>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrearPrimerGerenteAsync_SinEmail_LanzaValidationException()
    {
        var act = async () => await _sut.CrearPrimerGerenteAsync(PrimerGerente(email: null));

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*email*");
    }

    [Fact]
    public async Task CrearPrimerGerenteAsync_RolDistintoDeGerente_LanzaDomainException()
    {
        var act = async () => await _sut.CrearPrimerGerenteAsync(PrimerGerente() with { Rol = RolUsuarioEnum.Administrador });

        await act.Should().ThrowAsync<DomainException>().WithMessage("*primer Gerente*");
    }
}

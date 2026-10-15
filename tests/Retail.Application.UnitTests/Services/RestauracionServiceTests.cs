using System.Linq.Expressions;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.Application.DTOs.Backups;
using Retail.Application.DTOs.Sistema;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Application.Services;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Application.UnitTests.Services;

public class RestauracionServiceTests
{
    private const int IdGerente = 1;
    private const int IdAdministrador = 2;
    private const int IdCajero = 3;
    private const string Password = "Clave2026!";
    private const string Migracion = "20261013083426_AgregarRegistrosBackup";
    private const string RutaBak = @"C:\RetailData\Backups\Retail_20261013_210000_Manual.bak";
    private const string BaseAnterior = "RetailDb_Anterior_20261013_220000";
    private const long Tamano = 2048;
    private static readonly string Hash = new('c', 64);
    private static readonly Guid ReferenciaCopiaPrevia = Guid.NewGuid();

    // Repositorios en memoria: los sustitutos evalúan los predicados reales que usa el servicio.
    private readonly List<RegistroBackup> _historial = [];
    private readonly List<TurnoCaja> _turnosAbiertos = [];
    private readonly List<RegistroBackup> _copiasEnCursoEnLaCopia = [];
    private readonly IRepository<Usuario> _usuarios = Substitute.For<IRepository<Usuario>>();
    private readonly IRepository<RegistroBackup> _registros = Substitute.For<IRepository<RegistroBackup>>();
    private readonly IRepository<RegistroRestauracion> _restauraciones = Substitute.For<IRepository<RegistroRestauracion>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly IBackupService _backupService = Substitute.For<IBackupService>();
    private readonly IRestauradorBaseDatos _restaurador = Substitute.For<IRestauradorBaseDatos>();
    private readonly IBloqueoRestauracion _bloqueo = Substitute.For<IBloqueoRestauracion>();
    private readonly IAsyncDisposable _exclusividad = Substitute.For<IAsyncDisposable>();
    private readonly IBaseRestaurada _temporal = Substitute.For<IBaseRestaurada>();
    private readonly IArchivosBackup _archivos = Substitute.For<IArchivosBackup>();
    private readonly IEsquemaBaseDatos _esquema = Substitute.For<IEsquemaBaseDatos>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly RestauracionService _sut;
    private RegistroRestauracion? _restauracionRegistrada;

    public RestauracionServiceTests()
    {
        _registros.FindAsync(Arg.Any<Expression<Func<RegistroBackup, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci => _historial.Where(ci.Arg<Expression<Func<RegistroBackup, bool>>>().Compile()).ToList());
        _restaurador.HayTurnoDeCajaAbiertoEnBaseVigenteAsync(Arg.Any<CancellationToken>())
            .Returns(_ => _turnosAbiertos.Count > 0);

        RegistrarUsuario(IdGerente, "gerente", RolUsuarioEnum.Gerente);
        RegistrarUsuario(IdAdministrador, "soporte", RolUsuarioEnum.Administrador);
        RegistrarUsuario(IdCajero, "cajero", RolUsuarioEnum.Cajero);

        _archivos.PrepararParaRestaurarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ci => ci.Arg<string>());
        _archivos.CalcularHuellaAsync(RutaBak, Arg.Any<CancellationToken>()).Returns(new ArchivoBackupDto(Tamano, Hash));
        _esquema.ObtenerEstadoAsync(Arg.Any<CancellationToken>()).Returns(Esquema());
        _esquema.ExisteHistorialDeBackupsAsync(Arg.Any<CancellationToken>()).Returns(true);
        _backupService.CrearBackupPreRestauracionAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoBackupDto
            {
                Referencia = ReferenciaCopiaPrevia,
                RutaArchivo = @"C:\RetailData\Backups\previa.bak",
                TamanoBytes = 1024,
                HashSha256 = Hash,
                RegistradaEnHistorial = true
            });

        _bloqueo.AdquirirExclusivoAsync(Arg.Any<CancellationToken>()).Returns(_exclusividad);
        _restaurador.ExisteBaseVigenteAsync(Arg.Any<CancellationToken>()).Returns(true);
        _restaurador.RestaurarEnBaseTemporalAsync(RutaBak, Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>()).Returns(_temporal);
        _restaurador.EliminarBasesAnterioresAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns([]);

        _temporal.NombreBaseAnterior.Returns(BaseAnterior);
        _temporal.ObtenerEstadoEsquemaAsync(Arg.Any<CancellationToken>()).Returns(Esquema());
        _temporal.ExisteGerenteActivoAsync(Arg.Any<CancellationToken>()).Returns(true);
        _temporal.ObtenerCopiasEnCursoAsync(Arg.Any<CancellationToken>()).Returns(_ => _copiasEnCursoEnLaCopia.ToList());
        _temporal.AgregarRestauracionAsync(Arg.Do<RegistroRestauracion>(r => _restauracionRegistrada = r), Arg.Any<CancellationToken>());

        _sut = new RestauracionService(
            _usuarios, _registros, _restauraciones, _unitOfWork, _authService, _backupService, _restaurador, _bloqueo, _archivos, _esquema, _passwordHasher);
    }

    private static EstadoEsquemaDto Esquema(
        IReadOnlyList<string>? pendientes = null,
        IReadOnlyList<string>? desconocidas = null,
        bool puedeConectar = true) => new()
        {
            PuedeConectar = puedeConectar,
            MigracionesPendientes = pendientes ?? [],
            MigracionesDesconocidas = desconocidas ?? []
        };

    private static Usuario CrearUsuario(int id, string nombreUsuario, RolUsuarioEnum rol, string hash = "hash")
    {
        var email = rol == RolUsuarioEnum.Gerente ? $"{nombreUsuario}@libreria.com" : null;
        var usuario = Usuario.Crear(nombreUsuario, "Juan", "Pérez", hash, rol, email);
        usuario.Id = id;
        return usuario;
    }

    private void RegistrarUsuario(int id, string nombreUsuario, RolUsuarioEnum rol)
    {
        _usuarios.GetByIdAsync(id, includeDeleted: false, Arg.Any<CancellationToken>()).Returns(CrearUsuario(id, nombreUsuario, rol));
    }

    private RegistroBackup CopiaEnHistorial(bool verificada = true)
    {
        var registro = RegistroBackup.Iniciar(TipoBackupEnum.Automatico, null, null, RutaBak, Migracion, "1.0.0");
        registro.RegistrarExito(Tamano, Hash);
        if (verificada)
        {
            registro.MarcarVerificado();
        }

        registro.Id = _historial.Count + 1;
        _historial.Add(registro);
        return registro;
    }

    /// <summary>
    /// Registro <c>EnCurso</c> con una referencia dada, como lo materializaría EF Core desde el <c>.bak</c> (R-18): el
    /// agregado solo genera referencias nuevas, así que se fija por reflexión.
    /// </summary>
    private static RegistroBackup EnCursoConReferencia(Guid referencia)
    {
        var registro = RegistroBackup.Iniciar(TipoBackupEnum.Manual, IdGerente, "gerente", RutaBak, Migracion, "1.0.0");
        typeof(RegistroBackup).GetProperty(nameof(RegistroBackup.Referencia))!.SetValue(registro, referencia);
        return registro;
    }

    private void ConManifiesto(Guid referencia)
    {
        _archivos.LeerManifiestoAsync(RutaBak, Arg.Any<CancellationToken>()).Returns(new ManifiestoBackupDto
        {
            Referencia = referencia,
            Tipo = nameof(TipoBackupEnum.Manual),
            FechaInicioUtc = DateTime.UtcNow,
            Operador = "gerente",
            ArchivoBak = Path.GetFileName(RutaBak),
            TamanoBytes = Tamano,
            HashSha256 = Hash,
            UltimaMigracion = Migracion,
            VersionApp = "1.0.0",
            RegistradaEnHistorial = true
        });
    }

    /// <summary>
    /// Gerente que existe dentro de la copia (D-18), con otro Id que en la base vigente.
    /// </summary>
    private Usuario GerenteEnLaCopia()
    {
        // Usuario.Crear deja la contraseña como provisoria; un Gerente que ya la eligió él mismo puede autorizar (C-7).
        var gerente = CrearUsuario(42, "gerente", RolUsuarioEnum.Gerente, hash: "hash-copia");
        gerente.CambiarPasswordPropia("hash-copia");
        _temporal.BuscarUsuarioActivoAsync("gerente", Arg.Any<CancellationToken>()).Returns(gerente);
        _passwordHasher.VerifyPassword(Password, "hash-copia").Returns(true);
        return gerente;
    }

    private async Task NoSeTocoLaBaseVigenteAsync()
    {
        await _temporal.DidNotReceiveWithAnyArgs().PromoverAVigenteAsync(default);
        await _restaurador.DidNotReceiveWithAnyArgs().EliminarBasesAnterioresAsync(default, default);
    }

    // ───────────────────── Autorización desde la aplicación ─────────────────────

    [Theory]
    [InlineData(IdAdministrador)]
    [InlineData(IdCajero)]
    [InlineData(999)]
    public async Task RestaurarDesdeHistorialAsync_ActorQueNoEsGerente_LanzaOperacionNoAutorizadaSinRestaurar(int idActor)
    {
        // Arrange
        var copia = CopiaEnHistorial();

        // Act
        var act = async () => await _sut.RestaurarDesdeHistorialAsync(idActor, Password, copia.Referencia);

        // Assert
        await act.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _restaurador.DidNotReceiveWithAnyArgs().RestaurarEnBaseTemporalAsync(default!, default);
    }

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_PasswordIncorrecta_PropagaCredencialesInvalidasSinRestaurar()
    {
        // Arrange
        var copia = CopiaEnHistorial();
        _authService.VerificarCredencialAsync(IdGerente, "incorrecta", Arg.Any<CancellationToken>())
            .ThrowsAsync(new CredencialesInvalidasException());

        // Act
        var act = async () => await _sut.RestaurarDesdeHistorialAsync(IdGerente, "incorrecta", copia.Referencia);

        // Assert: reautenticación antes de cualquier efecto (D-8).
        await act.Should().ThrowAsync<CredencialesInvalidasException>();
        await _bloqueo.DidNotReceiveWithAnyArgs().AdquirirExclusivoAsync(default);
    }

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_CopiaInexistente_LanzaRestauracionFallida()
    {
        var act = async () => await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, Guid.NewGuid());

        await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage("*no es restaurable*");
    }

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_CopiaNoVerificada_LanzaRestauracionFallida()
    {
        // Arrange
        var copia = CopiaEnHistorial(verificada: false);

        // Act
        var act = async () => await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia);

        // Assert
        await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage("*no es restaurable*");
        await _restaurador.DidNotReceiveWithAnyArgs().RestaurarEnBaseTemporalAsync(default!, default);
    }

    // ───────────────────── Exclusividad ─────────────────────

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_OtraRestauracionEnCurso_LanzaRestauracionFallidaSinRestaurar()
    {
        // Arrange
        var copia = CopiaEnHistorial();
        _bloqueo.AdquirirExclusivoAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new RestauracionFallidaException("Hay otra restauración o una copia en curso."));

        // Act
        var act = async () => await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia);

        // Assert (D-25): el error del bloqueo llega envuelto y legible.
        await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage("*otra restauración*");
        await _restaurador.DidNotReceiveWithAnyArgs().RestaurarEnBaseTemporalAsync(default!, default);
    }

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_TodoValido_LiberaLaExclusividad()
    {
        // Arrange
        var copia = CopiaEnHistorial();

        // Act
        await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia);

        // Assert
        await _exclusividad.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_FallaUnaValidacion_LiberaLaExclusividad()
    {
        // Arrange
        var copia = CopiaEnHistorial();
        _temporal.ExisteGerenteActivoAsync(Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var act = async () => await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia);

        // Assert: un bloqueo que no se libera impediría todas las copias y restauraciones siguientes.
        await act.Should().ThrowAsync<RestauracionFallidaException>();
        await _exclusividad.Received(1).DisposeAsync();
    }

    // ───────────────────── Validaciones previas a restaurar ─────────────────────

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_ArchivoModificado_LanzaRestauracionFallidaSinRestaurar()
    {
        // Arrange
        var copia = CopiaEnHistorial();
        _archivos.CalcularHuellaAsync(RutaBak, Arg.Any<CancellationToken>()).Returns(new ArchivoBackupDto(Tamano, new string('d', 64)));

        // Act
        var act = async () => await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia);

        // Assert
        await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage("*cambió*");
        await _restaurador.DidNotReceiveWithAnyArgs().RestaurarEnBaseTemporalAsync(default!, default);
    }

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_ArchivoIlegible_LanzaRestauracionFallida()
    {
        // Arrange
        var copia = CopiaEnHistorial();
        _archivos.CalcularHuellaAsync(RutaBak, Arg.Any<CancellationToken>()).ThrowsAsync(new FileNotFoundException("no existe"));

        // Act
        var act = async () => await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia);

        // Assert
        await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage("*No se puede leer*");
    }

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_TurnoDeCajaAbierto_LanzaRestauracionFallidaSinRestaurar()
    {
        // Arrange (R-11)
        var copia = CopiaEnHistorial();
        _turnosAbiertos.Add(TurnoCaja.Abrir(IdGerente, 1000m));

        // Act
        var act = async () => await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia);

        // Assert
        await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage("*turno de caja abierto*");
        await _restaurador.DidNotReceiveWithAnyArgs().RestaurarEnBaseTemporalAsync(default!, default);
    }

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_CopiaEnCursoHuerfanaEnLaVigente_LaCierraComoFallidaYContinua()
    {
        // Arrange (B-0): el proceso murió a mitad de la copia; con el exclusivo tomado nadie más puede estar copiando.
        var copia = CopiaEnHistorial();
        var huerfana = RegistroBackup.Iniciar(TipoBackupEnum.Automatico, null, null, @"C:\otra.bak", Migracion, "1.0.0");
        _historial.Add(huerfana);

        // Act
        var resultado = await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia);

        // Assert: se cierra y se guarda antes de la copia previa, que de lo contrario chocaría con la copia en curso.
        huerfana.Estado.Should().Be(EstadoBackupEnum.Fallido);
        huerfana.MensajeError.Should().Be(RegistroBackup.MensajeCopiaHuerfana);
        await _registros.Received(1).UpdateAsync(huerfana, Arg.Any<CancellationToken>());
        Received.InOrder(() =>
        {
            _ = _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            _ = _backupService.CrearBackupPreRestauracionAsync(IdGerente, "gerente", Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>());
        });
        resultado.Advertencias.Should().Contain("Se cerró 1 copia de seguridad interrumpida en la base vigente.");
        await _temporal.Received(1).PromoverAVigenteAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_SinCopiasEnCurso_NoGuardaNiAdvierte()
    {
        // Arrange
        var copia = CopiaEnHistorial();

        // Act
        var resultado = await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia);

        // Assert
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        resultado.Advertencias.Should().NotContain(a => a.Contains("interrumpidas"));
    }

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_SeAbreUnTurnoDuranteLaRestauracion_AbortaAntesDeLaCopiaPrevia()
    {
        // Arrange (C-2): el primer control pasa; mientras se restaura la temporal, alguien abre la caja.
        var copia = CopiaEnHistorial();
        _restaurador.RestaurarEnBaseTemporalAsync(RutaBak, Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                _turnosAbiertos.Add(TurnoCaja.Abrir(IdCajero, 500m));
                return _temporal;
            });

        // Act
        var act = async () => await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia);

        // Assert
        await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage("*turno de caja abierto*");
        await _backupService.DidNotReceiveWithAnyArgs().CrearBackupPreRestauracionAsync(default, default!);
        await NoSeTocoLaBaseVigenteAsync();
        await _temporal.Received(1).DisposeAsync();
    }

    // ───────────────────── Estados de la base vigente (D-26) ─────────────────────

    [Fact]
    public async Task RestaurarDesdeArchivoAsync_VigenteQueExistePeroNoResponde_AbortaSinRestaurar()
    {
        // Arrange: puede tener los únicos datos reales; no se reemplaza a ciegas.
        GerenteEnLaCopia();
        ConManifiesto(Guid.NewGuid());
        _esquema.ObtenerEstadoAsync(Arg.Any<CancellationToken>()).Returns(Esquema(puedeConectar: false));

        // Act
        var act = async () => await _sut.RestaurarDesdeArchivoAsync(RutaBak, "gerente", Password);

        // Assert
        await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage("*no responde*");
        await _restaurador.DidNotReceiveWithAnyArgs().RestaurarEnBaseTemporalAsync(default!, default);
    }

    [Fact]
    public async Task RestaurarDesdeArchivoAsync_VigenteDesactualizada_TomaCopiaPreviaYControlaPeroNoImportaElHistorial()
    {
        // Arrange: una base con esquema viejo tiene todos los datos reales y BACKUP puede copiarla igual.
        GerenteEnLaCopia();
        ConManifiesto(Guid.NewGuid());
        _esquema.ObtenerEstadoAsync(Arg.Any<CancellationToken>()).Returns(Esquema(pendientes: ["20261014000000_Nueva"]));

        // Act
        var resultado = await _sut.RestaurarDesdeArchivoAsync(RutaBak, "gerente", Password);

        // Assert
        await _restaurador.Received().HayTurnoDeCajaAbiertoEnBaseVigenteAsync(Arg.Any<CancellationToken>());
        await _backupService.Received(1).CrearBackupPreRestauracionAsync(42, "gerente", Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>());
        await _temporal.DidNotReceiveWithAnyArgs().ImportarHistorialDeLaBaseVigenteAsync(default);
        resultado.ReferenciaCopiaPrevia.Should().Be(ReferenciaCopiaPrevia);
        resultado.Advertencias.Should().Contain(a => a.Contains("historial de respaldos no se pasó"));
    }

    [Fact]
    public async Task RestaurarDesdeArchivoAsync_VigenteSinTablaDeHistorial_NoConsultaCopiasEnCurso()
    {
        // Arrange: base de una versión anterior al historial de copias.
        GerenteEnLaCopia();
        ConManifiesto(Guid.NewGuid());
        _esquema.ObtenerEstadoAsync(Arg.Any<CancellationToken>()).Returns(Esquema(pendientes: [Migracion]));
        _esquema.ExisteHistorialDeBackupsAsync(Arg.Any<CancellationToken>()).Returns(false);

        // Act
        await _sut.RestaurarDesdeArchivoAsync(RutaBak, "gerente", Password);

        // Assert
        await _registros.DidNotReceiveWithAnyArgs().FindAsync(default!, default, default);
        await _temporal.Received(1).PromoverAVigenteAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestaurarDesdeArchivoAsync_SinBaseVigente_RestauraSinCopiaPreviaNiHistorialYLoAdvierte()
    {
        // Arrange: PC nueva, la base vigente no existe.
        GerenteEnLaCopia();
        ConManifiesto(Guid.NewGuid());
        _restaurador.ExisteBaseVigenteAsync(Arg.Any<CancellationToken>()).Returns(false);
        _temporal.NombreBaseAnterior.Returns((string?)null);

        // Act
        var resultado = await _sut.RestaurarDesdeArchivoAsync(RutaBak, "gerente", Password);

        // Assert
        await _esquema.DidNotReceiveWithAnyArgs().ObtenerEstadoAsync(default);
        await _restaurador.DidNotReceive().HayTurnoDeCajaAbiertoEnBaseVigenteAsync(Arg.Any<CancellationToken>());
        await _backupService.DidNotReceiveWithAnyArgs().CrearBackupPreRestauracionAsync(default, default!);
        await _temporal.DidNotReceiveWithAnyArgs().ImportarHistorialDeLaBaseVigenteAsync(default);
        await _temporal.Received(1).PromoverAVigenteAsync(Arg.Any<CancellationToken>());
        _restauracionRegistrada!.ReferenciaCopiaPrevia.Should().BeNull();
        _restauracionRegistrada.BaseAnterior.Should().BeNull();
        resultado.Advertencias.Should().Contain(a => a.Contains("No existe una base vigente"));
    }

    // ───────────────────── Validaciones sobre la base temporal ─────────────────────

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_CopiaDeUnaVersionMasNueva_LanzaRestauracionFallidaYDescartaLaTemporal()
    {
        // Arrange (R-7)
        var copia = CopiaEnHistorial();
        _temporal.ObtenerEstadoEsquemaAsync(Arg.Any<CancellationToken>())
            .Returns(Esquema(desconocidas: ["20991231000000_MigracionDelFuturo"]));

        // Act
        var act = async () => await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia);

        // Assert: la temporal se libera sin promover, lo que la elimina (D-17).
        await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage("*MigracionDelFuturo*");
        await _temporal.DidNotReceiveWithAnyArgs().AplicarMigracionesPendientesAsync(default);
        await _backupService.DidNotReceiveWithAnyArgs().CrearBackupPreRestauracionAsync(default, default!);
        await NoSeTocoLaBaseVigenteAsync();
        await _temporal.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_CopiaSinGerenteActivo_LanzaRestauracionFallidaSinIntercambiar()
    {
        // Arrange
        var copia = CopiaEnHistorial();
        _temporal.ExisteGerenteActivoAsync(Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var act = async () => await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia);

        // Assert
        await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage("*ningún Gerente activo*");
        await _backupService.DidNotReceiveWithAnyArgs().CrearBackupPreRestauracionAsync(default, default!);
        await NoSeTocoLaBaseVigenteAsync();
    }

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_FallaLaCopiaPrevia_LanzaRestauracionFallidaSinIntercambiar()
    {
        // Arrange: desde la aplicación la vigente está sana; si no se puede copiar, hay un problema real.
        var copia = CopiaEnHistorial();
        _backupService.CrearBackupPreRestauracionAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new BackupFallidoException("Espacio insuficiente"));

        // Act
        var act = async () => await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia);

        // Assert (D-25)
        (await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage("*Espacio insuficiente*"))
            .WithInnerException<BackupFallidoException>();
        await NoSeTocoLaBaseVigenteAsync();
    }

    // ───────────────────── D-25: toda falla de un paso llega como RestauracionFallida ─────────────────────

    public static TheoryData<string> PasosQueFallan => new()
    {
        "restaurar en la temporal",
        "importar el historial",
        "guardar en la temporal",
        "promover a vigente"
    };

    [Theory]
    [MemberData(nameof(PasosQueFallan))]
    public async Task RestaurarDesdeHistorialAsync_FallaUnPasoDeInfraestructura_LanzaRestauracionFallidaYDescartaLaTemporal(string paso)
    {
        // Arrange
        var copia = CopiaEnHistorial();
        var falla = new InvalidOperationException($"Falla simulada al {paso}");
        switch (paso)
        {
            case "restaurar en la temporal":
                _restaurador.RestaurarEnBaseTemporalAsync(RutaBak, Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>()).ThrowsAsync(falla);
                break;
            case "importar el historial":
                _temporal.ImportarHistorialDeLaBaseVigenteAsync(Arg.Any<CancellationToken>()).ThrowsAsync(falla);
                break;
            case "guardar en la temporal":
                _temporal.GuardarCambiosAsync(Arg.Any<CancellationToken>()).ThrowsAsync(falla);
                break;
            default:
                _temporal.PromoverAVigenteAsync(Arg.Any<CancellationToken>()).ThrowsAsync(falla);
                break;
        }

        // Act
        var act = async () => await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia);

        // Assert
        // B-1: el operador no ve el texto técnico del motor; el detalle viaja en la InnerException, que va al log.
        var excepcion = (await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage("*La base vigente no cambió*")).Which;
        excepcion.Message.Should().NotContain("Falla simulada");
        excepcion.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be($"Falla simulada al {paso}");
        await _restaurador.DidNotReceiveWithAnyArgs().EliminarBasesAnterioresAsync(default, default);
        await _exclusividad.Received(1).DisposeAsync();

        if (paso != "restaurar en la temporal")
        {
            await _temporal.Received(1).DisposeAsync();
        }
    }

    // ───────────────────── Restauración completa desde la aplicación ─────────────────────

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_TodoValido_ValidaYPreparaLaTemporalAntesDeIntercambiar()
    {
        // Arrange
        var copia = CopiaEnHistorial();

        // Act
        await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia);

        // Assert: el intercambio es el último paso; antes de él, todo lo que puede fallar (D-17).
        Received.InOrder(() =>
        {
            _ = _bloqueo.AdquirirExclusivoAsync(Arg.Any<CancellationToken>());
            _ = _archivos.CalcularHuellaAsync(RutaBak, Arg.Any<CancellationToken>());
            _ = _restaurador.ExisteBaseVigenteAsync(Arg.Any<CancellationToken>());
            _ = _esquema.ObtenerEstadoAsync(Arg.Any<CancellationToken>());
            _ = _restaurador.RestaurarEnBaseTemporalAsync(RutaBak, Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>());
            _ = _temporal.ObtenerEstadoEsquemaAsync(Arg.Any<CancellationToken>());
            _ = _temporal.AplicarMigracionesPendientesAsync(Arg.Any<CancellationToken>());
            _ = _temporal.ExisteGerenteActivoAsync(Arg.Any<CancellationToken>());
            _ = _backupService.CrearBackupPreRestauracionAsync(IdGerente, "gerente", Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>());
            _ = _temporal.ImportarHistorialDeLaBaseVigenteAsync(Arg.Any<CancellationToken>());
            _ = _temporal.ObtenerCopiasEnCursoAsync(Arg.Any<CancellationToken>());
            _ = _temporal.AgregarRestauracionAsync(Arg.Any<RegistroRestauracion>(), Arg.Any<CancellationToken>());
            _ = _temporal.GuardarCambiosAsync(Arg.Any<CancellationToken>());
            _ = _temporal.PromoverAVigenteAsync(Arg.Any<CancellationToken>());
            _ = _restaurador.EliminarBasesAnterioresAsync(BaseAnterior, Arg.Any<CancellationToken>());
        });

        // Los controles de turno y copia en curso se hacen dos veces: al empezar y antes de la copia previa (C-2).
        await _restaurador.Received(2).HayTurnoDeCajaAbiertoEnBaseVigenteAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_TodoValido_RegistraLaRestauracionYDevuelveElResultado()
    {
        // Arrange
        var copia = CopiaEnHistorial();
        _temporal.ObtenerEstadoEsquemaAsync(Arg.Any<CancellationToken>()).Returns(Esquema(pendientes: ["20261014000000_Nueva"]));

        // Act
        var resultado = await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia);

        // Assert
        _restauracionRegistrada.Should().NotBeNull();
        _restauracionRegistrada!.Origen.Should().Be(OrigenRestauracionEnum.Aplicacion);
        _restauracionRegistrada.IdUsuario.Should().Be(IdGerente);
        _restauracionRegistrada.NombreUsuario.Should().Be("gerente");
        _restauracionRegistrada.ReferenciaCopia.Should().Be(copia.Referencia);
        _restauracionRegistrada.ReferenciaCopiaPrevia.Should().Be(ReferenciaCopiaPrevia);
        _restauracionRegistrada.BaseAnterior.Should().Be(BaseAnterior);

        resultado.ReferenciaRestauracion.Should().Be(_restauracionRegistrada.Referencia);
        resultado.ReferenciaCopia.Should().Be(copia.Referencia);
        resultado.ReferenciaCopiaPrevia.Should().Be(ReferenciaCopiaPrevia);
        resultado.BaseAnterior.Should().Be(BaseAnterior);
        resultado.MigracionesAplicadas.Should().Equal("20261014000000_Nueva");
    }

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_TodoValido_IncluyeSiempreElAvisoDeConciliacionConArca()
    {
        // Arrange (R-3)
        var copia = CopiaEnHistorial();

        // Act
        var resultado = await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia);

        // Assert
        resultado.Advertencias.Should().Equal(ResultadoRestauracionDto.AvisoConciliacionArca);
    }

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_IntercambioConTokenCancelable_PromueveSinCancelacion()
    {
        // Arrange
        var copia = CopiaEnHistorial();
        using var cancelacion = new CancellationTokenSource();

        // Act
        await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia, cancellationToken: cancelacion.Token);

        // Assert: después del punto de no retorno, cancelar dejaría el intercambio a medias.
        await _temporal.Received(1).PromoverAVigenteAsync(CancellationToken.None);
        await _restaurador.Received(1).EliminarBasesAnterioresAsync(BaseAnterior, CancellationToken.None);
    }

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_NoSePudoEliminarUnaBaseAnteriorVieja_LoInformaComoAdvertencia()
    {
        // Arrange
        var copia = CopiaEnHistorial();
        _restaurador.EliminarBasesAnterioresAsync(BaseAnterior, Arg.Any<CancellationToken>()).Returns(["RetailDb_Anterior_Vieja"]);

        // Act
        var resultado = await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia);

        // Assert
        resultado.Advertencias.Should().Contain(a => a.Contains("RetailDb_Anterior_Vieja"));
    }

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_LaLimpiezaDeBasesAnterioresLanza_TerminaBienConAdvertencia()
    {
        // Arrange (C-4): el intercambio ya ocurrió; una falla de limpieza no es una restauración fallida.
        var copia = CopiaEnHistorial();
        _restaurador.EliminarBasesAnterioresAsync(BaseAnterior, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("El servidor dejó de responder"));

        // Act
        var resultado = await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia);

        // Assert
        resultado.BaseAnterior.Should().Be(BaseAnterior);
        // B-1: la advertencia no repite el texto técnico (que va al log), pero dice qué pasará.
        resultado.Advertencias.Should().Contain(a => a.Contains("se reintentará en la próxima restauración"));
        resultado.Advertencias.Should().NotContain(a => a.Contains("dejó de responder"));
    }

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_CancelacionAntesDelIntercambio_NoIntercambia()
    {
        // Arrange
        var copia = CopiaEnHistorial();
        using var cancelacion = new CancellationTokenSource();
        _temporal.AplicarMigracionesPendientesAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cancelacion.Cancel();
                return Task.FromCanceled(cancelacion.Token);
            });

        // Act
        var act = async () => await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia, cancellationToken: cancelacion.Token);

        // Assert: la cancelación no se envuelve (D-25).
        await act.Should().ThrowAsync<OperationCanceledException>();
        await NoSeTocoLaBaseVigenteAsync();
        await _temporal.Received(1).DisposeAsync();
    }

    // ───────────────────── R-18: copias en curso dentro del .bak ─────────────────────

    [Fact]
    public async Task RestaurarDesdeArchivoAsync_CopiasEnCursoDentroDelBak_VerificaLaRestauradaYCierraLasDemasComoFallidas()
    {
        // Arrange
        GerenteEnLaCopia();
        var restaurada = RegistroBackup.Iniciar(TipoBackupEnum.Manual, 42, "gerente", RutaBak, Migracion, "1.0.0");
        var interrumpida = RegistroBackup.Iniciar(TipoBackupEnum.Automatico, null, null, @"C:\otra.bak", Migracion, "1.0.0");
        _copiasEnCursoEnLaCopia.AddRange([restaurada, interrumpida]);
        ConManifiesto(restaurada.Referencia);

        // Act
        await _sut.RestaurarDesdeArchivoAsync(RutaBak, "gerente", Password);

        // Assert: si quedaran EnCurso, el índice de copia única bloquearía todas las copias siguientes.
        restaurada.Estado.Should().Be(EstadoBackupEnum.Verificado);
        restaurada.TamanoBytes.Should().Be(Tamano);
        restaurada.HashSha256.Should().Be(Hash);
        interrumpida.Estado.Should().Be(EstadoBackupEnum.Fallido);
        interrumpida.MensajeError.Should().Contain("interrumpida");
        await _temporal.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_CopiaRestauradaEnCursoDentroDelBak_QuedaVerificada()
    {
        // Arrange: por la vía de la aplicación, la referencia sale del historial de la base vigente.
        var copia = CopiaEnHistorial();
        var dentroDelBak = EnCursoConReferencia(copia.Referencia);
        _copiasEnCursoEnLaCopia.Add(dentroDelBak);

        // Act
        await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia);

        // Assert
        dentroDelBak.Estado.Should().Be(EstadoBackupEnum.Verificado);
        dentroDelBak.HashSha256.Should().Be(Hash);
    }

    // ───────────────────── Recuperación ante desastres (consola, D-18) ─────────────────────

    [Fact]
    public async Task RestaurarDesdeArchivoAsync_CredencialDeUnGerenteDeLaCopia_RegistraAlGerenteDeLaCopia()
    {
        // Arrange
        var gerente = GerenteEnLaCopia();
        ConManifiesto(Guid.NewGuid());

        // Act: el nombre se normaliza igual que en el login ("Gerente" entra como "gerente").
        var resultado = await _sut.RestaurarDesdeArchivoAsync(RutaBak, "  Gerente ", Password);

        // Assert
        _restauracionRegistrada!.Origen.Should().Be(OrigenRestauracionEnum.Consola);
        _restauracionRegistrada.IdUsuario.Should().Be(gerente.Id);
        resultado.Advertencias.Should().StartWith(ResultadoRestauracionDto.AvisoConciliacionArca);
        await _backupService.Received(1).CrearBackupPreRestauracionAsync(gerente.Id, "gerente", Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>());
        await _temporal.Received(1).PromoverAVigenteAsync(Arg.Any<CancellationToken>());

        // La consola no consulta la base vigente para autorizar: puede no existir.
        await _authService.DidNotReceiveWithAnyArgs().VerificarCredencialAsync(default, default!);
        await _temporal.DidNotReceiveWithAnyArgs().ExisteGerenteActivoAsync(default);
    }

    [Theory]
    [InlineData("gerente", "incorrecta")]
    [InlineData("desconocido", Password)]
    [InlineData("cajero", Password)]
    [InlineData("gerente", "")]
    public async Task RestaurarDesdeArchivoAsync_CredencialInvalidaEnLaCopia_LanzaCredencialesInvalidasSinIntercambiar(
        string nombreUsuario,
        string password)
    {
        // Arrange: un cajero de la copia con la contraseña correcta recibe la misma respuesta que un desconocido.
        GerenteEnLaCopia();
        var cajero = CrearUsuario(43, "cajero", RolUsuarioEnum.Cajero, hash: "hash-cajero");
        _temporal.BuscarUsuarioActivoAsync("cajero", Arg.Any<CancellationToken>()).Returns(cajero);
        _passwordHasher.VerifyPassword(Password, "hash-cajero").Returns(true);
        ConManifiesto(Guid.NewGuid());

        // Act
        var act = async () => await _sut.RestaurarDesdeArchivoAsync(RutaBak, nombreUsuario, password);

        // Assert
        await act.Should().ThrowAsync<CredencialesInvalidasException>();
        await _backupService.DidNotReceiveWithAnyArgs().CrearBackupPreRestauracionAsync(default, default!);
        await NoSeTocoLaBaseVigenteAsync();
        await _temporal.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task RestaurarDesdeArchivoAsync_GerenteConCambioDePasswordPendiente_LanzaCredencialesInvalidas()
    {
        // Arrange (C-7): por ejemplo, el "admin" sembrado en bases viejas con una contraseña conocida.
        var gerente = CrearUsuario(44, "admin", RolUsuarioEnum.Gerente, hash: "hash-admin");
        gerente.DebeCambiarPassword.Should().BeTrue("Usuario.Crear deja la contraseña inicial como provisoria");
        _temporal.BuscarUsuarioActivoAsync("admin", Arg.Any<CancellationToken>()).Returns(gerente);
        _passwordHasher.VerifyPassword(Password, "hash-admin").Returns(true);
        ConManifiesto(Guid.NewGuid());

        // Act
        var act = async () => await _sut.RestaurarDesdeArchivoAsync(RutaBak, "admin", Password);

        // Assert
        await act.Should().ThrowAsync<CredencialesInvalidasException>();
        await NoSeTocoLaBaseVigenteAsync();
    }

    [Fact]
    public async Task RestaurarDesdeArchivoAsync_FallaLaCopiaPrevia_ContinuaYLoAdvierte()
    {
        // Arrange: en un desastre la vigente puede estar dañada; la base anterior igual se conserva (D-17).
        GerenteEnLaCopia();
        ConManifiesto(Guid.NewGuid());
        _backupService.CrearBackupPreRestauracionAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new BackupFallidoException("Página dañada"));

        // Act
        var resultado = await _sut.RestaurarDesdeArchivoAsync(RutaBak, "gerente", Password);

        // Assert
        resultado.ReferenciaCopiaPrevia.Should().BeNull();
        resultado.Advertencias.Should().Contain(a => a.Contains("Página dañada"));
        await _temporal.Received(1).PromoverAVigenteAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestaurarDesdeArchivoAsync_ArchivoFueraDelDirectorio_UsaLaRutaPreparadaParaTodo()
    {
        // Arrange (B-3): el .bak estaba en Descargas; el puerto lo trajo al directorio de respaldos con otro nombre.
        const string rutaOriginal = @"C:\Users\gerente\Downloads\Retail_20261013_210000_Manual.bak";
        const string rutaCopiada = @"C:\RetailData\Backups\Retail_20261013_210000_Manual_2.bak";
        GerenteEnLaCopia();
        _archivos.PrepararParaRestaurarAsync(rutaOriginal, Arg.Any<CancellationToken>()).Returns(rutaCopiada);
        _archivos.LeerManifiestoAsync(rutaCopiada, Arg.Any<CancellationToken>()).Returns(new ManifiestoBackupDto
        {
            Referencia = Guid.NewGuid(),
            Tipo = nameof(TipoBackupEnum.Manual),
            FechaInicioUtc = DateTime.UtcNow,
            Operador = "gerente",
            ArchivoBak = Path.GetFileName(rutaCopiada),
            TamanoBytes = Tamano,
            HashSha256 = Hash,
            UltimaMigracion = Migracion,
            VersionApp = "1.0.0",
            RegistradaEnHistorial = true
        });
        _archivos.CalcularHuellaAsync(rutaCopiada, Arg.Any<CancellationToken>()).Returns(new ArchivoBackupDto(Tamano, Hash));
        _restaurador.RestaurarEnBaseTemporalAsync(rutaCopiada, Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>()).Returns(_temporal);

        // Act
        await _sut.RestaurarDesdeArchivoAsync(rutaOriginal, "gerente", Password);

        // Assert: el motor, el manifiesto y la auditoría usan la copia, no el original que el motor no puede leer.
        await _restaurador.Received(1).RestaurarEnBaseTemporalAsync(rutaCopiada, Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>());
        await _restaurador.DidNotReceive().RestaurarEnBaseTemporalAsync(rutaOriginal, Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>());
        await _archivos.DidNotReceive().LeerManifiestoAsync(rutaOriginal, Arg.Any<CancellationToken>());
        _restauracionRegistrada!.RutaArchivo.Should().Be(rutaCopiada);
    }

    [Fact]
    public async Task RestaurarDesdeArchivoAsync_FallaLaPreparacionPorEntradaSalida_LanzaRestauracionFallidaConLaCausa()
    {
        // Arrange
        var causa = new IOException("disco lleno");
        _archivos.PrepararParaRestaurarAsync(RutaBak, Arg.Any<CancellationToken>()).ThrowsAsync(causa);

        // Act
        var act = async () => await _sut.RestaurarDesdeArchivoAsync(RutaBak, "gerente", Password);

        // Assert
        var excepcion = await act.Should().ThrowAsync<RestauracionFallidaException>();
        excepcion.Which.Message.Should().Contain("preparar la copia").And.Contain("disco lleno");
        excepcion.Which.InnerException.Should().BeSameAs(causa);
        await _bloqueo.DidNotReceiveWithAnyArgs().AdquirirExclusivoAsync(default);
    }

    [Fact]
    public async Task RestaurarDesdeArchivoAsync_FallaInesperadaAlPrepararLaCopia_OcultaElDetalleTecnicoYConservaLaCausa()
    {
        // Arrange (D-25)
        var causa = new InvalidOperationException("detalle interno");
        _archivos.PrepararParaRestaurarAsync(RutaBak, Arg.Any<CancellationToken>()).ThrowsAsync(causa);

        // Act
        var act = async () => await _sut.RestaurarDesdeArchivoAsync(RutaBak, "gerente", Password);

        // Assert
        var excepcion = await act.Should().ThrowAsync<RestauracionFallidaException>();
        excepcion.Which.Message.Should().Contain("preparar la copia").And.NotContain("detalle interno");
        excepcion.Which.InnerException.Should().BeSameAs(causa);
    }

    [Fact]
    public async Task RestaurarDesdeArchivoAsync_ElPuertoRechazaLaCopia_PropagaSuMensajeSinEnvolver()
    {
        // Arrange
        var rechazo = new RestauracionFallidaException("El manifiesto .json pertenece a otra copia.");
        _archivos.PrepararParaRestaurarAsync(RutaBak, Arg.Any<CancellationToken>()).ThrowsAsync(rechazo);

        // Act
        var act = async () => await _sut.RestaurarDesdeArchivoAsync(RutaBak, "gerente", Password);

        // Assert
        (await act.Should().ThrowAsync<RestauracionFallidaException>()).Which.Should().BeSameAs(rechazo);
    }

    [Fact]
    public async Task RestaurarDesdeArchivoAsync_ManifiestoIlegible_LanzaRestauracionFallida()
    {
        // Arrange
        _archivos.LeerManifiestoAsync(RutaBak, Arg.Any<CancellationToken>()).ThrowsAsync(new FileNotFoundException("falta el .json"));

        // Act
        var act = async () => await _sut.RestaurarDesdeArchivoAsync(RutaBak, "gerente", Password);

        // Assert
        await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage("*manifiesto*");
        await _bloqueo.DidNotReceiveWithAnyArgs().AdquirirExclusivoAsync(default);
    }

    [Fact]
    public async Task RestaurarDesdeArchivoAsync_ArchivoQueNoCoincideConSuManifiesto_LanzaRestauracionFallida()
    {
        // Arrange
        ConManifiesto(Guid.NewGuid());
        _archivos.CalcularHuellaAsync(RutaBak, Arg.Any<CancellationToken>()).Returns(new ArchivoBackupDto(Tamano + 1, Hash));

        // Act
        var act = async () => await _sut.RestaurarDesdeArchivoAsync(RutaBak, "gerente", Password);

        // Assert
        await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage("*cambió*");
        await _restaurador.DidNotReceiveWithAnyArgs().RestaurarEnBaseTemporalAsync(default!, default);
    }

    // ───────────────────── Última restauración (R-20) ─────────────────────

    private static RegistroRestauracion Restauracion(int id, string gerente, OrigenRestauracionEnum origen)
    {
        var registro = RegistroRestauracion.Registrar(
            origen, IdGerente, gerente, Guid.NewGuid(), RutaBak, Migracion, ReferenciaCopiaPrevia, BaseAnterior);
        registro.Id = id;
        return registro;
    }

    [Theory]
    [InlineData(IdGerente)]
    [InlineData(IdAdministrador)]
    public async Task ObtenerUltimaRestauracionAsync_GerenteOAdministrador_DevuelveLaMasReciente(int idActor)
    {
        // Arrange
        var antigua = Restauracion(1, "gerente", OrigenRestauracionEnum.Consola);
        var reciente = Restauracion(2, "otro.gerente", OrigenRestauracionEnum.Aplicacion);
        _restauraciones.ListAllAsync(false, Arg.Any<CancellationToken>()).Returns([antigua, reciente]);

        // Act
        var ultima = await _sut.ObtenerUltimaRestauracionAsync(idActor);

        // Assert
        ultima.Should().NotBeNull();
        ultima!.Referencia.Should().Be(reciente.Referencia);
        ultima.NombreUsuario.Should().Be("otro.gerente");
        ultima.Origen.Should().Be(OrigenRestauracionEnum.Aplicacion);
        ultima.ReferenciaCopia.Should().Be(reciente.ReferenciaCopia);
        ultima.RutaArchivo.Should().Be(RutaBak);
        ultima.BaseAnterior.Should().Be(BaseAnterior);
        ultima.Fecha.Should().Be(reciente.Fecha);
    }

    [Fact]
    public async Task ObtenerUltimaRestauracionAsync_SinRestauraciones_DevuelveNulo()
    {
        // Arrange
        _restauraciones.ListAllAsync(false, Arg.Any<CancellationToken>()).Returns([]);

        // Act
        var ultima = await _sut.ObtenerUltimaRestauracionAsync(IdGerente);

        // Assert
        ultima.Should().BeNull();
    }

    [Theory]
    [InlineData(IdCajero)]
    [InlineData(999)]
    [InlineData(0)]
    public async Task ObtenerUltimaRestauracionAsync_ActorSinPermiso_LanzaOperacionNoAutorizadaSinLeer(int idActor)
    {
        // Act
        var act = async () => await _sut.ObtenerUltimaRestauracionAsync(idActor);

        // Assert
        await act.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _restauraciones.DidNotReceiveWithAnyArgs().ListAllAsync(default, default);
    }

    [Fact]
    public async Task RestaurarDesdeArchivoAsync_RutaMasLargaQueLaColumna_LanzaRestauracionFallidaSinLeerNiRestaurar()
    {
        // Arrange: B-3, una ruta que no entraría en el registro de restauración se rechaza antes de cualquier trabajo.
        var ruta = @"C:\" + new string('x', RegistroBackup.LongitudMaximaRuta) + ".bak";

        // Act
        var act = async () => await _sut.RestaurarDesdeArchivoAsync(ruta, "gerente", "Clave2026!");

        // Assert
        await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage($"*{RegistroBackup.LongitudMaximaRuta} caracteres*");
        await _archivos.DidNotReceiveWithAnyArgs().LeerManifiestoAsync(default!, default);
        await _restaurador.DidNotReceiveWithAnyArgs().RestaurarEnBaseTemporalAsync(default!, default, default);
    }

    [Fact]
    public async Task RestaurarDesdeArchivoAsync_OrigenMuyLargoPeroPreparadoCorto_RestauraDesdeLaRutaPreparada()
    {
        // Arrange: la ruta de origen supera la columna, pero PrepararParaRestaurarAsync la trae a una carpeta corta: eso es
        // justamente lo que resuelve, así que el límite de longitud se aplica sobre la ruta preparada y no sobre el origen.
        var rutaOriginal = @"C:\" + new string('x', RegistroBackup.LongitudMaximaRuta) + ".bak";
        const string rutaCopiada = @"C:\RetailData\Backups\Retail_20261013_210000_Manual.bak";
        GerenteEnLaCopia();
        _archivos.PrepararParaRestaurarAsync(rutaOriginal, Arg.Any<CancellationToken>()).Returns(rutaCopiada);
        _archivos.LeerManifiestoAsync(rutaCopiada, Arg.Any<CancellationToken>()).Returns(new ManifiestoBackupDto
        {
            Referencia = Guid.NewGuid(),
            Tipo = nameof(TipoBackupEnum.Manual),
            FechaInicioUtc = DateTime.UtcNow,
            Operador = "gerente",
            ArchivoBak = Path.GetFileName(rutaCopiada),
            TamanoBytes = Tamano,
            HashSha256 = Hash,
            UltimaMigracion = Migracion,
            VersionApp = "1.0.0",
            RegistradaEnHistorial = true
        });
        _archivos.CalcularHuellaAsync(rutaCopiada, Arg.Any<CancellationToken>()).Returns(new ArchivoBackupDto(Tamano, Hash));
        _restaurador.RestaurarEnBaseTemporalAsync(rutaCopiada, Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>()).Returns(_temporal);

        // Act
        await _sut.RestaurarDesdeArchivoAsync(rutaOriginal, "gerente", Password);

        // Assert
        _restauracionRegistrada!.RutaArchivo.Should().Be(rutaCopiada);
        await _temporal.Received(1).PromoverAVigenteAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_CopiaExitosaSinVerificarEnLaVigente_LaCierraComoFallidaYBorraSusRestos()
    {
        // Arrange (B-0): el proceso murió verificando; el registro quedó Exitoso, no EnCurso, y ocuparía un lugar de la retención.
        var copia = CopiaEnHistorial();
        var huerfana = CopiaEnHistorial(verificada: false);
        const string rutaHuerfana = @"C:\RetailData\Backups\parcial.bak";
        typeof(RegistroBackup).GetProperty(nameof(RegistroBackup.RutaArchivo))!.SetValue(huerfana, rutaHuerfana);

        // Act
        var resultado = await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia);

        // Assert
        huerfana.Estado.Should().Be(EstadoBackupEnum.Fallido);
        huerfana.MensajeError.Should().Be(RegistroBackup.MensajeCopiaHuerfana);
        copia.Estado.Should().Be(EstadoBackupEnum.Verificado, "una copia verificada nunca es huérfana");
        _archivos.Received(1).EliminarCopia(rutaHuerfana);
        resultado.Advertencias.Should().Contain("Se cerró 1 copia de seguridad interrumpida en la base vigente.");
    }

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_VariasCopiasHuerfanasEnLaVigente_AdvierteEnPlural()
    {
        // Arrange
        var copia = CopiaEnHistorial();
        _historial.Add(RegistroBackup.Iniciar(TipoBackupEnum.Automatico, null, null, @"C:\a.bak", Migracion, "1.0.0"));
        _historial.Add(RegistroBackup.Iniciar(TipoBackupEnum.Manual, IdGerente, "gerente", @"C:\b.bak", Migracion, "1.0.0"));

        // Act
        var resultado = await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia);

        // Assert
        resultado.Advertencias.Should().Contain("Se cerraron 2 copias de seguridad interrumpidas en la base vigente.");
    }

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_NoSePuedeBorrarElRestoDeUnaHuerfana_IgualRestaura()
    {
        // Arrange: el archivo parcial sigue abierto por el motor; no es motivo para abortar la restauración.
        var copia = CopiaEnHistorial();
        _historial.Add(RegistroBackup.Iniciar(TipoBackupEnum.Automatico, null, null, @"C:\a.bak", Migracion, "1.0.0"));
        _archivos.When(a => a.EliminarCopia(@"C:\a.bak")).Do(_ => throw new IOException("en uso"));

        // Act
        await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia);

        // Assert
        await _temporal.Received(1).PromoverAVigenteAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public async Task RestaurarDesdeHistorialAsync_UsuarioDeLaTareaProgramada_AdvierteSoloSiLaVigenteLoTeniaYLaCopiaNo(
        bool vigenteLoTiene, bool copiaLoTiene, bool debeAdvertir)
    {
        // Arrange: el usuario de base de datos de la copia automática y sus permisos viven dentro de la base.
        var copia = CopiaEnHistorial();
        _restaurador.VigenteTieneUsuarioDeLaTareaProgramadaAsync(Arg.Any<CancellationToken>()).Returns(vigenteLoTiene);
        _temporal.TieneUsuarioDeLaTareaProgramadaAsync(Arg.Any<CancellationToken>()).Returns(copiaLoTiene);

        // Act
        var resultado = await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia);

        // Assert
        if (debeAdvertir)
        {
            resultado.Advertencias.Should().Contain(RestauracionService.AvisoPermisosDeLaCopiaAutomatica);
            RestauracionService.AvisoPermisosDeLaCopiaAutomatica.Should().Contain(@"scripts\registrar-tarea-backup.ps1");
        }
        else
        {
            resultado.Advertencias.Should().NotContain(RestauracionService.AvisoPermisosDeLaCopiaAutomatica);
        }
    }

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_SinBaseVigente_NoConsultaElUsuarioDeLaTareaProgramada()
    {
        // Arrange: PC nueva, no hay nada que comparar.
        var copia = CopiaEnHistorial();
        _restaurador.ExisteBaseVigenteAsync(Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var resultado = await _sut.RestaurarDesdeHistorialAsync(IdGerente, Password, copia.Referencia);

        // Assert
        resultado.Advertencias.Should().NotContain(RestauracionService.AvisoPermisosDeLaCopiaAutomatica);
        await _restaurador.DidNotReceiveWithAnyArgs().VigenteTieneUsuarioDeLaTareaProgramadaAsync(default);
    }
}

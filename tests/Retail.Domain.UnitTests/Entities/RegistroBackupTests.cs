using FluentAssertions;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

public class RegistroBackupTests
{
    private const string Ruta = @"C:\RetailData\Backups\RetailDb_20261013_210000_Manual.bak";
    private const string Migracion = "20261013071257_AgregarRolAdministradorYCuentasDeUsuario";
    private static readonly string HashValido = new('a', 64);

    private static RegistroBackup Manual() =>
        RegistroBackup.Iniciar(TipoBackupEnum.Manual, 1, "gerente", Ruta, Migracion, "1.0.0");

    private static RegistroBackup Automatico() =>
        RegistroBackup.Iniciar(TipoBackupEnum.Automatico, null, null, Ruta, Migracion, "1.0.0");

    private static RegistroBackup Verificado()
    {
        var registro = Manual();
        registro.RegistrarExito(1024, HashValido);
        registro.MarcarVerificado();
        return registro;
    }

    // ───────────────────── Inicio ─────────────────────

    [Fact]
    public void Iniciar_DatosValidos_QuedaEnCursoConReferenciaYDatosDelEsquema()
    {
        // Act
        var registro = RegistroBackup.Iniciar(TipoBackupEnum.Manual, 7, "  gerente ", $"  {Ruta} ", Migracion, "1.0.0");

        // Assert
        registro.Estado.Should().Be(EstadoBackupEnum.EnCurso);
        registro.Referencia.Should().NotBe(Guid.Empty);
        registro.IdUsuario.Should().Be(7);
        registro.NombreUsuario.Should().Be("gerente");
        registro.RutaArchivo.Should().Be(Ruta);
        registro.UltimaMigracion.Should().Be(Migracion);
        registro.FechaInicio.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        registro.FechaFin.Should().BeNull();
        registro.EsRestaurable.Should().BeFalse();
    }

    [Fact]
    public void Iniciar_DosCopias_TienenReferenciasDistintas()
    {
        Manual().Referencia.Should().NotBe(Manual().Referencia);
    }

    [Theory]
    [InlineData(TipoBackupEnum.Manual)]
    [InlineData(TipoBackupEnum.PreRestauracion)]
    public void Iniciar_TipoPedidoPorUnaPersonaSinOperador_LanzaDomainException(TipoBackupEnum tipo)
    {
        var act = () => RegistroBackup.Iniciar(tipo, null, null, Ruta, Migracion, "1.0.0");

        act.Should().Throw<DomainException>().WithMessage("*operador*");
    }

    [Theory]
    [InlineData(TipoBackupEnum.Automatico)]
    [InlineData(TipoBackupEnum.PreMigracion)]
    public void Iniciar_TipoDelSistemaConOperador_LanzaDomainException(TipoBackupEnum tipo)
    {
        // Un registro no puede mentir sobre su origen: una copia automática no la pidió nadie.
        var act = () => RegistroBackup.Iniciar(tipo, 1, "gerente", Ruta, Migracion, "1.0.0");

        act.Should().Throw<DomainException>().WithMessage("*sistema*");
    }

    [Theory]
    [InlineData(TipoBackupEnum.Automatico)]
    [InlineData(TipoBackupEnum.PreMigracion)]
    public void Iniciar_TipoDelSistemaSinOperador_QuedaComoCopiaDelSistema(TipoBackupEnum tipo)
    {
        var registro = RegistroBackup.Iniciar(tipo, null, null, Ruta, Migracion, "1.0.0");

        registro.EsDelSistema.Should().BeTrue();
    }

    [Theory]
    [InlineData("", Migracion, "1.0.0")]
    [InlineData(Ruta, " ", "1.0.0")]
    [InlineData(Ruta, Migracion, "")]
    public void Iniciar_DatoObligatorioVacio_LanzaDomainException(string ruta, string migracion, string version)
    {
        var act = () => RegistroBackup.Iniciar(TipoBackupEnum.Automatico, null, null, ruta, migracion, version);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Iniciar_RutaDemasiadoLarga_LanzaDomainException()
    {
        var ruta = @"C:\" + new string('x', RegistroBackup.LongitudMaximaRuta);

        var act = () => RegistroBackup.Iniciar(TipoBackupEnum.Automatico, null, null, ruta, Migracion, "1.0.0");

        act.Should().Throw<DomainException>().WithMessage("*400*");
    }

    [Fact]
    public void Iniciar_TipoInexistente_LanzaDomainException()
    {
        var act = () => RegistroBackup.Iniciar((TipoBackupEnum)99, null, null, Ruta, Migracion, "1.0.0");

        act.Should().Throw<DomainException>();
    }

    // ───────────────────── Ciclo feliz ─────────────────────

    [Fact]
    public void RegistrarExito_EnCurso_GuardaTamanoYHashNormalizadoYPasaAExitoso()
    {
        // Arrange
        var registro = Manual();

        // Act
        registro.RegistrarExito(2048, "  " + new string('A', 64) + " ");

        // Assert
        registro.Estado.Should().Be(EstadoBackupEnum.Exitoso);
        registro.TamanoBytes.Should().Be(2048);
        registro.HashSha256.Should().Be(new string('a', 64));
        registro.FechaFin.Should().NotBeNull();
        registro.EsRestaurable.Should().BeFalse("todavía no se verificó que el archivo sea legible");
    }

    [Fact]
    public void MarcarVerificado_Exitoso_QuedaRestaurable()
    {
        var registro = Verificado();

        registro.Estado.Should().Be(EstadoBackupEnum.Verificado);
        registro.EsRestaurable.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RegistrarExito_TamanoNoPositivo_LanzaDomainExceptionYSigueEnCurso(long tamano)
    {
        var registro = Manual();

        var act = () => registro.RegistrarExito(tamano, HashValido);

        act.Should().Throw<DomainException>();
        registro.Estado.Should().Be(EstadoBackupEnum.EnCurso);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void RegistrarExito_HashInvalido_LanzaDomainExceptionYSigueEnCurso(string hash)
    {
        var registro = Manual();

        var act = () => registro.RegistrarExito(1024, hash);

        act.Should().Throw<DomainException>().WithMessage("*SHA-256*");
        registro.Estado.Should().Be(EstadoBackupEnum.EnCurso);
        registro.HashSha256.Should().BeNull();
    }

    // ───────────────────── Fallos ─────────────────────

    [Fact]
    public void RegistrarFallo_EnCurso_PasaAFallidoConElMotivo()
    {
        var registro = Automatico();

        registro.RegistrarFallo("  Disco lleno  ");

        registro.Estado.Should().Be(EstadoBackupEnum.Fallido);
        registro.MensajeError.Should().Be("Disco lleno");
        registro.FechaFin.Should().NotBeNull();
    }

    [Fact]
    public void RegistrarFallo_ExitosoQueNoPasaLaVerificacion_PasaAFallido()
    {
        // Arrange: el archivo se escribió, pero RESTORE VERIFYONLY lo rechazó.
        var registro = Manual();
        registro.RegistrarExito(1024, HashValido);

        // Act
        registro.RegistrarFallo("La verificación del archivo falló.");

        // Assert
        registro.Estado.Should().Be(EstadoBackupEnum.Fallido);
        registro.EsRestaurable.Should().BeFalse();
    }

    [Fact]
    public void RegistrarFallo_MensajeMuyLargo_LoRecortaAlMaximoPersistible()
    {
        var registro = Automatico();

        registro.RegistrarFallo(new string('x', RegistroBackup.LongitudMaximaMensajeError + 50));

        registro.MensajeError.Should().HaveLength(RegistroBackup.LongitudMaximaMensajeError);
    }

    // ───────────────────── Retención ─────────────────────

    [Fact]
    public void MarcarArchivoPurgado_Verificado_DejaDeSerRestaurable()
    {
        var registro = Verificado();

        registro.MarcarArchivoPurgado();

        registro.Estado.Should().Be(EstadoBackupEnum.ArchivoPurgado);
        registro.EsRestaurable.Should().BeFalse();
    }

    // ───────────────────── Transiciones inválidas ─────────────────────

    [Fact]
    public void MarcarVerificado_EnCurso_LanzaTransicionBackupInvalidaException()
    {
        var registro = Manual();

        var act = () => registro.MarcarVerificado();

        act.Should().Throw<TransicionBackupInvalidaException>()
            .Which.EstadoActual.Should().Be(EstadoBackupEnum.EnCurso);
    }

    [Fact]
    public void MarcarVerificado_Fallido_LanzaTransicionBackupInvalidaException()
    {
        // Una copia fallida nunca puede volverse restaurable.
        var registro = Manual();
        registro.RegistrarFallo("Error de E/S");

        var act = () => registro.MarcarVerificado();

        act.Should().Throw<TransicionBackupInvalidaException>();
        registro.EsRestaurable.Should().BeFalse();
    }

    [Fact]
    public void RegistrarExito_YaVerificado_LanzaTransicionBackupInvalidaException()
    {
        var registro = Verificado();

        var act = () => registro.RegistrarExito(1, HashValido);

        act.Should().Throw<TransicionBackupInvalidaException>();
    }

    [Fact]
    public void RegistrarFallo_Verificado_LanzaTransicionBackupInvalidaException()
    {
        // Una copia verificada ya probó ser legible: no se reescribe su historia.
        var registro = Verificado();

        var act = () => registro.RegistrarFallo("otro error");

        act.Should().Throw<TransicionBackupInvalidaException>();
        registro.Estado.Should().Be(EstadoBackupEnum.Verificado);
    }

    [Fact]
    public void MarcarArchivoPurgado_EnCurso_LanzaTransicionBackupInvalidaException()
    {
        // No se puede borrar el archivo que el motor todavía está escribiendo.
        var registro = Manual();

        var act = () => registro.MarcarArchivoPurgado();

        act.Should().Throw<TransicionBackupInvalidaException>();
    }

    [Fact]
    public void MarcarArchivoPurgado_YaPurgado_LanzaTransicionBackupInvalidaException()
    {
        var registro = Verificado();
        registro.MarcarArchivoPurgado();

        var act = () => registro.MarcarArchivoPurgado();

        act.Should().Throw<TransicionBackupInvalidaException>();
    }
}

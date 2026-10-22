using FluentAssertions;
using Retail.Domain.Common;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

public class RegistroRestauracionTests
{
    private const string Ruta = @"C:\RetailData\Backups\RetailDb_20261013_210000_Manual.bak";
    private const string Migracion = "20261013083426_AgregarRegistrosBackup";
    private static readonly Guid Copia = Guid.NewGuid();
    private static readonly Guid CopiaPrevia = Guid.NewGuid();

    private static RegistroRestauracion Registrar(
        OrigenRestauracionEnum origen = OrigenRestauracionEnum.Aplicacion,
        int idUsuario = 3,
        string nombreUsuario = "gerente",
        Guid? referenciaCopia = null,
        string rutaArchivo = Ruta,
        string migracionCopia = Migracion,
        Guid? referenciaCopiaPrevia = null,
        string? baseAnterior = "RetailDb_Anterior_20261013_220000",
        int? idOperador = null,
        string? nombreOperador = null) =>
        RegistroRestauracion.Registrar(
            origen,
            idUsuario,
            nombreUsuario,
            referenciaCopia ?? Copia,
            rutaArchivo,
            migracionCopia,
            referenciaCopiaPrevia,
            baseAnterior,
            idOperador,
            nombreOperador);

    [Fact]
    public void Registrar_DatosValidos_ConservaLaFotoDelOperadorYLaRedDeSeguridad()
    {
        // Act
        var registro = RegistroRestauracion.Registrar(
            OrigenRestauracionEnum.Consola, 7, "  gerente ", Copia, $" {Ruta} ", Migracion, CopiaPrevia, " RetailDb_Anterior_X ");

        // Assert
        registro.Referencia.Should().NotBe(Guid.Empty);
        registro.Origen.Should().Be(OrigenRestauracionEnum.Consola);
        registro.IdUsuario.Should().Be(7);
        registro.NombreUsuario.Should().Be("gerente");
        registro.ReferenciaCopia.Should().Be(Copia);
        registro.RutaArchivo.Should().Be(Ruta);
        registro.MigracionCopia.Should().Be(Migracion);
        registro.ReferenciaCopiaPrevia.Should().Be(CopiaPrevia);
        registro.BaseAnterior.Should().Be("RetailDb_Anterior_X");
        registro.Fecha.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Registrar_SinBaseVigenteNiCopiaPrevia_AdmiteAmbosNulos()
    {
        // Una PC nueva no tiene base que reemplazar ni de la cual tomar una copia previa (D-20).
        var registro = Registrar(referenciaCopiaPrevia: null, baseAnterior: "  ");

        registro.ReferenciaCopiaPrevia.Should().BeNull();
        registro.BaseAnterior.Should().BeNull();
    }

    [Fact]
    public void Registrar_DosRestauraciones_TienenReferenciasDistintas()
    {
        Registrar().Referencia.Should().NotBe(Registrar().Referencia);
    }

    [Fact]
    public void Registrar_OrigenInexistente_LanzaDomainException()
    {
        var act = () => Registrar(origen: (OrigenRestauracionEnum)99);

        act.Should().Throw<DomainException>().WithMessage("*origen*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Registrar_SinGerente_LanzaDomainException(int idUsuario)
    {
        var act = () => Registrar(idUsuario: idUsuario);

        act.Should().Throw<DomainException>().WithMessage("*Gerente*");
    }

    [Fact]
    public void Registrar_CopiaSinReferencia_LanzaDomainException()
    {
        var act = () => Registrar(referenciaCopia: Guid.Empty);

        act.Should().Throw<DomainException>().WithMessage("*copia restaurada*");
    }

    [Fact]
    public void Registrar_CopiaPreviaConReferenciaVacia_LanzaDomainException()
    {
        var act = () => Registrar(referenciaCopiaPrevia: Guid.Empty);

        act.Should().Throw<DomainException>().WithMessage("*copia previa*");
    }

    [Theory]
    [InlineData(OrigenRestauracionEnum.Aplicacion)]
    [InlineData(OrigenRestauracionEnum.Consola)]
    public void Registrar_CadaOrigen_LoConserva(OrigenRestauracionEnum origen)
    {
        Registrar(origen: origen).Origen.Should().Be(origen);
    }

    [Fact]
    public void Registrar_DatosValidos_RegistraLaFechaEnUtc()
    {
        // La fecha se compara entre bases y procesos: siempre en UTC, como RegistroBackup.
        Registrar().Fecha.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData("", Ruta, Migracion, "el nombre del Gerente")]
    [InlineData("gerente", " ", Migracion, "la ruta del archivo restaurado")]
    [InlineData("gerente", Ruta, "", "la migración de la copia")]
    public void Registrar_TextoObligatorioVacio_LanzaDomainExceptionNombrandoElCampo(
        string nombreUsuario,
        string ruta,
        string migracion,
        string campo)
    {
        var act = () => Registrar(nombreUsuario: nombreUsuario, rutaArchivo: ruta, migracionCopia: migracion);

        act.Should().Throw<DomainException>().WithMessage($"Debe indicarse {campo}*");
    }

    [Fact]
    public void Registrar_NombreDeGerenteDemasiadoLargo_LanzaDomainExceptionNombrandoElCampo()
    {
        var act = () => Registrar(nombreUsuario: new string('g', ReglasTexto.LongitudMaximaNombreUsuario + 1));

        act.Should().Throw<DomainException>().WithMessage($"El nombre del Gerente supera los {ReglasTexto.LongitudMaximaNombreUsuario}*");
    }

    [Fact]
    public void Registrar_RutaDemasiadoLarga_LanzaDomainExceptionNombrandoElCampo()
    {
        var act = () => Registrar(rutaArchivo: @"C:\" + new string('r', RegistroBackup.LongitudMaximaRuta));

        act.Should().Throw<DomainException>().WithMessage($"La ruta del archivo restaurado supera los {RegistroBackup.LongitudMaximaRuta}*");
    }

    [Fact]
    public void Registrar_MigracionDemasiadoLarga_LanzaDomainExceptionNombrandoElCampo()
    {
        var act = () => Registrar(migracionCopia: new string('m', RegistroBackup.LongitudMaximaMigracion + 1));

        act.Should().Throw<DomainException>().WithMessage($"La migración de la copia supera los {RegistroBackup.LongitudMaximaMigracion}*");
    }

    [Fact]
    public void Registrar_NombreDeBaseDemasiadoLargo_LanzaDomainExceptionNombrandoElCampo()
    {
        var act = () => Registrar(baseAnterior: new string('x', RegistroRestauracion.LongitudMaximaNombreBase + 1));

        act.Should().Throw<DomainException>().WithMessage($"El nombre de la base anterior supera los {RegistroRestauracion.LongitudMaximaNombreBase}*");
    }

    [Fact]
    public void Registrar_ConOperadorDistintoDelAutorizante_GuardaAmbos()
    {
        var registro = Registrar(idUsuario: 3, idOperador: 9, nombreOperador: "  admin ");

        registro.IdUsuario.Should().Be(3);
        registro.IdOperador.Should().Be(9);
        registro.NombreOperador.Should().Be("admin");
    }

    [Fact]
    public void Registrar_SinOperador_DejaOperadorNulo()
    {
        var registro = Registrar();

        registro.IdOperador.Should().BeNull();
        registro.NombreOperador.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Registrar_OperadorSinNombre_LanzaDomainException(string? nombreOperador)
    {
        var act = () => Registrar(idOperador: 9, nombreOperador: nombreOperador);

        act.Should().Throw<DomainException>().WithMessage("Deben indicarse el Id y el nombre del operador*");
    }

    [Fact]
    public void Registrar_NombreOperadorSinId_LanzaDomainException()
    {
        var act = () => Registrar(idOperador: null, nombreOperador: "admin");

        act.Should().Throw<DomainException>().WithMessage("Deben indicarse el Id y el nombre del operador*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Registrar_OperadorConIdInvalido_LanzaDomainException(int idOperador)
    {
        var act = () => Registrar(idOperador: idOperador, nombreOperador: "admin");

        act.Should().Throw<DomainException>().WithMessage("El Id del operador*");
    }

    [Fact]
    public void Registrar_OperadorIgualAlAutorizante_LanzaDomainException()
    {
        var act = () => Registrar(idUsuario: 3, idOperador: 3, nombreOperador: "gerente");

        act.Should().Throw<DomainException>().WithMessage("El operador debe ser distinto del Gerente*");
    }

    [Fact]
    public void Registrar_NombreOperadorDemasiadoLargo_LanzaDomainException()
    {
        var act = () => Registrar(
            idOperador: 9,
            nombreOperador: new string('a', ReglasTexto.LongitudMaximaNombreUsuario + 1));

        act.Should().Throw<DomainException>().WithMessage($"El nombre del operador supera los {ReglasTexto.LongitudMaximaNombreUsuario}*");
    }

    [Fact]
    public void Registrar_TextosEnElLimiteDeLongitud_LosAcepta()
    {
        var act = () => Registrar(
            nombreUsuario: new string('g', ReglasTexto.LongitudMaximaNombreUsuario),
            rutaArchivo: new string('r', RegistroBackup.LongitudMaximaRuta),
            migracionCopia: new string('m', RegistroBackup.LongitudMaximaMigracion),
            baseAnterior: new string('x', RegistroRestauracion.LongitudMaximaNombreBase));

        act.Should().NotThrow();
    }
}

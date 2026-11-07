using FluentAssertions;
using Retail.App.UnitTests.Helpers;
using Retail.App.ViewModels.Comun;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class FiltroFechaColumnaViewModelTests
{
    // Miércoles 28/10/2026, 09:00 en Argentina (12:00 UTC)
    private static readonly RelojDePrueba Reloj = new(new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero));

    private static FiltroFechaColumnaViewModel Crear()
    {
        return new FiltroFechaColumnaViewModel("Fecha", Reloj);
    }

    [Theory]
    [InlineData(AtajoFecha.Hoy, "2026-10-28", "2026-10-28")]
    [InlineData(AtajoFecha.Ayer, "2026-10-27", "2026-10-27")]
    [InlineData(AtajoFecha.EstaSemana, "2026-10-26", "2026-11-01")]
    [InlineData(AtajoFecha.SemanaPasada, "2026-10-19", "2026-10-25")]
    [InlineData(AtajoFecha.EsteMes, "2026-10-01", "2026-10-31")]
    [InlineData(AtajoFecha.MesPasado, "2026-09-01", "2026-09-30")]
    [InlineData(AtajoFecha.EsteAnio, "2026-01-01", "2026-12-31")]
    [InlineData(AtajoFecha.AnioPasado, "2025-01-01", "2025-12-31")]
    public void ElegirAtajo_CadaAtajo_ArmaElBorradorConSuRango(AtajoFecha atajo, string desde, string hasta)
    {
        var filtro = Crear();

        filtro.ElegirAtajoCommand.Execute(atajo);

        filtro.BorradorDesde.Should().Be(DateTime.Parse(desde, System.Globalization.CultureInfo.InvariantCulture));
        filtro.BorradorHasta.Should().Be(DateTime.Parse(hasta, System.Globalization.CultureInfo.InvariantCulture));
        filtro.HayFiltroActivo.Should().BeFalse("el atajo solo arma el borrador: hace falta Aplicar");
    }

    [Fact]
    public void Aplicar_ConRangoNuevo_GuardaAvisaYCierra()
    {
        var filtro = Crear();
        int avisos = 0;
        filtro.Aplicado += (_, _) => avisos++;
        filtro.EstaAbierto = true;
        filtro.ElegirAtajoCommand.Execute(AtajoFecha.EstaSemana);

        filtro.AplicarCommand.Execute(null);

        filtro.DesdeAplicado.Should().Be(new DateTime(2026, 10, 26));
        filtro.HastaAplicado.Should().Be(new DateTime(2026, 11, 1));
        filtro.HayFiltroActivo.Should().BeTrue();
        filtro.EstaAbierto.Should().BeFalse();
        avisos.Should().Be(1);
    }

    [Fact]
    public void Aplicar_ConElRangoAlReves_LoOrdenaDelMenorAlMayor()
    {
        var filtro = Crear();
        filtro.BorradorDesde = new DateTime(2026, 10, 20);
        filtro.BorradorHasta = new DateTime(2026, 10, 10);

        filtro.AplicarCommand.Execute(null);

        filtro.DesdeAplicado.Should().Be(new DateTime(2026, 10, 10));
        filtro.HastaAplicado.Should().Be(new DateTime(2026, 10, 20));
    }

    [Fact]
    public void Aplicar_SinCambiosRespectoDeLoAplicado_NoAvisa()
    {
        var filtro = Crear();
        int avisos = 0;
        filtro.Aplicado += (_, _) => avisos++;

        filtro.AplicarCommand.Execute(null);

        avisos.Should().Be(0);
        filtro.HayFiltroActivo.Should().BeFalse();
    }

    [Fact]
    public void Cancelar_DescartaElBorrador_ElPanelReabreDesdeLoAplicado()
    {
        var filtro = Crear();
        filtro.EstaAbierto = true;
        filtro.ElegirAtajoCommand.Execute(AtajoFecha.Hoy);

        filtro.CancelarCommand.Execute(null);
        filtro.EstaAbierto = true;

        filtro.EstaAbierto.Should().BeTrue();
        filtro.BorradorDesde.Should().BeNull("lo aplicado seguía vacío");
        filtro.HayFiltroActivo.Should().BeFalse();
    }

    [Fact]
    public void RangoUtc_ConZonaArgentina_ConvierteDiasLocalesAUnIntervaloSemiabierto()
    {
        var filtro = Crear();
        filtro.BorradorDesde = new DateTime(2026, 10, 26);
        filtro.BorradorHasta = new DateTime(2026, 10, 28);
        filtro.AplicarCommand.Execute(null);

        var (desdeUtc, hastaUtcExclusivo) = filtro.RangoUtc();

        desdeUtc.Should().Be(new DateTime(2026, 10, 26, 3, 0, 0), "la medianoche local de Argentina es 03:00 UTC");
        hastaUtcExclusivo.Should().Be(new DateTime(2026, 10, 29, 3, 0, 0), "el tope es el inicio del día siguiente al último");
    }

    [Fact]
    public void RangoUtc_SinFiltro_DevuelveExtremosNulos()
    {
        var (desdeUtc, hastaUtcExclusivo) = Crear().RangoUtc();

        desdeUtc.Should().BeNull();
        hastaUtcExclusivo.Should().BeNull();
    }

    [Fact]
    public void RangoUtc_SoloDesde_NoPoneTope()
    {
        var filtro = Crear();
        filtro.BorradorDesde = new DateTime(2026, 10, 26);
        filtro.AplicarCommand.Execute(null);

        var (_, hastaUtcExclusivo) = filtro.RangoUtc();

        hastaUtcExclusivo.Should().BeNull();
        filtro.TextoAplicado.Should().Be("Desde 26/10/2026");
    }

    [Fact]
    public void Contiene_ConRangoAplicado_IncluyeLosDosExtremosCompletos()
    {
        var filtro = Crear();
        filtro.BorradorDesde = new DateTime(2026, 10, 26);
        filtro.BorradorHasta = new DateTime(2026, 10, 28);
        filtro.AplicarCommand.Execute(null);

        filtro.Contiene(new DateTime(2026, 10, 25, 23, 59, 0)).Should().BeFalse();
        filtro.Contiene(new DateTime(2026, 10, 26, 0, 0, 0)).Should().BeTrue();
        filtro.Contiene(new DateTime(2026, 10, 28, 23, 59, 0)).Should().BeTrue("el día final entra completo");
        filtro.Contiene(new DateTime(2026, 10, 29, 0, 0, 0)).Should().BeFalse();
    }

    [Fact]
    public void ElegirAtajo_ConLimites_RecortaElRangoAlIntervaloElegible()
    {
        var filtro = Crear();
        filtro.FechaMinima = new DateTime(2026, 10, 10);
        filtro.FechaMaxima = new DateTime(2026, 10, 28);

        filtro.ElegirAtajoCommand.Execute(AtajoFecha.EsteMes);

        filtro.BorradorDesde.Should().Be(new DateTime(2026, 10, 10), "el mes empieza antes de la primera venta");
        filtro.BorradorHasta.Should().Be(new DateTime(2026, 10, 28));
    }

    [Fact]
    public void ElegirAtajo_ConLimites_UnAtajoQueCaeAfueraSeDeshabilita()
    {
        var filtro = Crear();
        filtro.FechaMinima = new DateTime(2026, 10, 10);
        filtro.FechaMaxima = new DateTime(2026, 10, 28);

        filtro.ElegirAtajoCommand.CanExecute(AtajoFecha.MesPasado).Should().BeFalse("septiembre es anterior a la primera venta");
        filtro.ElegirAtajoCommand.CanExecute(AtajoFecha.AnioPasado).Should().BeFalse();
        filtro.ElegirAtajoCommand.CanExecute(AtajoFecha.EstaSemana).Should().BeTrue();
        filtro.ElegirAtajoCommand.CanExecute(AtajoFecha.Hoy).Should().BeTrue();
    }

    [Fact]
    public void ElegirAtajo_SinLimites_SiempreEstaDisponible()
    {
        var filtro = Crear();

        filtro.ElegirAtajoCommand.CanExecute(AtajoFecha.AnioPasado).Should().BeTrue();
    }

    [Fact]
    public void Aplicar_ConRangoCompletoExigido_SeHabilitaSoloConLosDosExtremos()
    {
        var filtro = new FiltroFechaColumnaViewModel("Período", Reloj, exigeRangoCompleto: true);

        filtro.AplicarCommand.CanExecute(null).Should().BeFalse("no hay ningún extremo");

        filtro.BorradorDesde = new DateTime(2026, 10, 26);
        filtro.AplicarCommand.CanExecute(null).Should().BeFalse("falta el extremo final");

        filtro.BorradorHasta = new DateTime(2026, 10, 28);
        filtro.AplicarCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void Aplicar_SinExigirRangoCompleto_SiempreEstaHabilitado()
    {
        Crear().AplicarCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void EstablecerRango_FijaLoAplicadoSinAvisar()
    {
        var filtro = Crear();
        int avisos = 0;
        filtro.Aplicado += (_, _) => avisos++;

        filtro.EstablecerRango(new DateTime(2026, 10, 1, 15, 30, 0), new DateTime(2026, 10, 28));

        filtro.DesdeAplicado.Should().Be(new DateTime(2026, 10, 1), "se guarda el día, sin hora");
        filtro.HastaAplicado.Should().Be(new DateTime(2026, 10, 28));
        filtro.TextoAplicado.Should().Be("01/10/2026 – 28/10/2026");
        avisos.Should().Be(0);
    }

    [Fact]
    public void QuitarFiltro_ConRangoAplicado_VuelveASinFiltroSinAvisar()
    {
        var filtro = Crear();
        filtro.BorradorDesde = new DateTime(2026, 10, 26);
        filtro.AplicarCommand.Execute(null);
        int avisos = 0;
        filtro.Aplicado += (_, _) => avisos++;

        filtro.QuitarFiltro();

        filtro.HayFiltroActivo.Should().BeFalse();
        filtro.Contiene(new DateTime(2020, 1, 1)).Should().BeTrue();
        avisos.Should().Be(0, "\"Limpiar filtros\" consulta una sola vez, al final");
    }
}

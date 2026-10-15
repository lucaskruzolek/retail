using FluentAssertions;
using FluentValidation;
using NSubstitute;
using Retail.Application.DTOs.Reportes;
using Retail.Application.DTOs.Reportes.Consultas;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Application.UnitTests.TestData;
using Retail.Application.Validators.Reportes;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Application.UnitTests.Services;

public class ReporteServiceTests
{
    private const int IdGerente = 1;
    private const int IdCajero = 2;
    private const int IdInexistente = 99;

    private readonly IReporteQueryService _consultas = Substitute.For<IReporteQueryService>();
    private readonly IRepository<Usuario> _usuarios = Substitute.For<IRepository<Usuario>>();

    public ReporteServiceTests()
    {
        RegistrarUsuario(IdGerente, RolUsuarioEnum.Gerente);
        RegistrarUsuario(IdCajero, RolUsuarioEnum.Cajero);

        // Respuestas vacías por defecto: cada prueba pisa solo lo que le interesa.
        _consultas.ObtenerResumenVentasAsync(Arg.Any<RangoUtcDto>(), Arg.Any<CancellationToken>())
            .Returns(new ResumenVentasFila(0, 0m, 0m, 0m));
        _consultas.ObtenerVentasPorPeriodoAsync(Arg.Any<RangoUtcDto>(), Arg.Any<AgrupacionTemporalEnum>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new List<VentasPeriodoFila>());
        _consultas.ObtenerMasVendidosAsync(Arg.Any<RangoUtcDto>(), Arg.Any<FiltroRankingDto>(), Arg.Any<CancellationToken>())
            .Returns(new List<RankingFila>());
        _consultas.ObtenerMenosVendidosAsync(Arg.Any<RangoUtcDto>(), Arg.Any<FiltroRankingDto>(), Arg.Any<CancellationToken>())
            .Returns(new List<RankingFila>());
        _consultas.ObtenerRentabilidadAsync(Arg.Any<RangoUtcDto>(), Arg.Any<FiltroRentabilidadDto>(), Arg.Any<CancellationToken>())
            .Returns(new List<RentabilidadFila>());
        _consultas.ObtenerResumenInventarioAsync(Arg.Any<FiltroInventarioDto>(), Arg.Any<CancellationToken>())
            .Returns(new ResumenInventarioFila(0, 0, 0m, 0m));
        _consultas.ObtenerStockCriticoAsync(Arg.Any<FiltroInventarioDto>(), Arg.Any<CancellationToken>())
            .Returns(new List<StockCriticoFila>());
        _consultas.ObtenerSinMovimientoAsync(Arg.Any<FiltroInventarioDto>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<SinMovimientoFila>());
    }

    private static DateOnly Dia(int anio, int mes, int dia) => new(anio, mes, dia);

    private static DateTime Utc(int anio, int mes, int dia, int hora = 0) => new(anio, mes, dia, hora, 0, 0, DateTimeKind.Utc);

    private void RegistrarUsuario(int id, RolUsuarioEnum rol)
    {
        var nombre = rol == RolUsuarioEnum.Gerente ? "gerente" : "cajero";
        var email = rol == RolUsuarioEnum.Gerente ? "gerente@libreria.com" : null;
        var usuario = Usuario.Crear(nombre, "Juan", "Pérez", "hash", rol, email);
        usuario.Id = id;
        _usuarios.GetByIdAsync(id, includeDeleted: false, Arg.Any<CancellationToken>()).Returns(usuario);
    }

    /// <summary>Servicio con el reloj en el mediodía local del 15/10/2026, salvo que se indique otro instante.</summary>
    private ReporteService CrearServicio(DateTimeOffset? ahoraUtc = null)
    {
        var reloj = ahoraUtc is null
            ? RelojDePrueba.EnQuinceDeOctubre()
            : new RelojDePrueba(ahoraUtc.Value);

        return new ReporteService(
            _consultas,
            _usuarios,
            reloj,
            new FiltroPeriodoValidator(reloj),
            new FiltroVentasPorPeriodoValidator(reloj),
            new FiltroRankingValidator(reloj),
            new FiltroRentabilidadValidator(reloj),
            new FiltroInventarioValidator(),
            new FiltroSinMovimientoValidator());
    }

    // ---------- Autorización y validación ----------

    [Fact]
    public async Task ObtenerTablero_ConActorCajero_LanzaOperacionNoAutorizadaYNoConsulta()
    {
        var accion = () => CrearServicio().ObtenerTableroAsync(
            IdCajero, new FiltroPeriodoDto { Desde = Dia(2026, 10, 1), Hasta = Dia(2026, 10, 15) });

        await accion.Should().ThrowAsync<OperacionNoAutorizadaException>()
            .WithMessage("Solo un Gerente puede consultar los reportes gerenciales.");
        _consultas.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task ObtenerRanking_ConActorInexistente_LanzaOperacionNoAutorizadaYNoConsulta()
    {
        var accion = () => CrearServicio().ObtenerRankingAsync(
            IdInexistente, new FiltroRankingDto { Desde = Dia(2026, 10, 1), Hasta = Dia(2026, 10, 15) });

        await accion.Should().ThrowAsync<OperacionNoAutorizadaException>();
        _consultas.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task ObtenerStockCritico_ConActorCajero_LanzaOperacionNoAutorizada()
    {
        var accion = () => CrearServicio().ObtenerStockCriticoAsync(IdCajero, new FiltroInventarioDto());

        await accion.Should().ThrowAsync<OperacionNoAutorizadaException>();
        _consultas.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task ObtenerVentasPorPeriodo_ConFiltroInvalido_LanzaValidationExceptionYNoConsulta()
    {
        var accion = () => CrearServicio().ObtenerVentasPorPeriodoAsync(
            IdGerente, new FiltroVentasPorPeriodoDto { Desde = Dia(2026, 10, 15), Hasta = Dia(2026, 10, 1) });

        await accion.Should().ThrowAsync<ValidationException>();
        _consultas.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task ObtenerSinMovimiento_ConDiasCero_LanzaValidationExceptionYNoConsulta()
    {
        var accion = () => CrearServicio().ObtenerSinMovimientoAsync(IdGerente, new FiltroSinMovimientoDto { Dias = 0 });

        await accion.Should().ThrowAsync<ValidationException>();
        _consultas.ReceivedCalls().Should().BeEmpty();
    }

    // ---------- Conversión del rango local a UTC (D-9) ----------

    [Fact]
    public async Task ObtenerVentasPorPeriodo_ConUnDiaLocal_ConvierteElRangoAUtcConDesfaseDeArgentina()
    {
        var dia = Dia(2026, 10, 15);

        await CrearServicio().ObtenerVentasPorPeriodoAsync(
            IdGerente, new FiltroVentasPorPeriodoDto { Desde = dia, Hasta = dia });

        await _consultas.Received(1).ObtenerVentasPorPeriodoAsync(
            Arg.Is<RangoUtcDto>(r =>
                r.DesdeUtc == Utc(2026, 10, 15, 3) &&
                r.HastaExclusivoUtc == Utc(2026, 10, 16, 3) &&
                r.DesfaseMinutos == -180),
            AgrupacionTemporalEnum.Dia,
            null,
            Arg.Any<CancellationToken>());
    }

    // ---------- Tablero ----------

    [Fact]
    public async Task ObtenerTablero_ConUnMesDe31Dias_ConsultaElPeriodoAnteriorDeIgualDuracion()
    {
        var servicio = CrearServicio(new DateTimeOffset(2026, 11, 2, 15, 0, 0, TimeSpan.Zero));

        var tablero = await servicio.ObtenerTableroAsync(
            IdGerente, new FiltroPeriodoDto { Desde = Dia(2026, 10, 1), Hasta = Dia(2026, 10, 31) });

        tablero.DesdeAnterior.Should().Be(Dia(2026, 8, 31));
        tablero.HastaAnterior.Should().Be(Dia(2026, 9, 30));
        await _consultas.Received(1).ObtenerResumenVentasAsync(
            Arg.Is<RangoUtcDto>(r => r.DesdeUtc == Utc(2026, 8, 31, 3) && r.HastaExclusivoUtc == Utc(2026, 10, 1, 3)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ObtenerTablero_ConPeriodoAnteriorSinVentas_LaVariacionEsNula()
    {
        _consultas.ObtenerResumenVentasAsync(Arg.Any<RangoUtcDto>(), Arg.Any<CancellationToken>())
            .Returns(new ResumenVentasFila(5, 1000m, 0m, 600m), new ResumenVentasFila(0, 0m, 0m, 0m));

        var tablero = await CrearServicio().ObtenerTableroAsync(
            IdGerente, new FiltroPeriodoDto { Desde = Dia(2026, 10, 1), Hasta = Dia(2026, 10, 15) });

        tablero.TotalVendido.Valor.Should().Be(1000m);
        tablero.TotalVendido.VariacionPorcentual.Should().BeNull();
        tablero.CantidadVentas.VariacionPorcentual.Should().BeNull();
        tablero.MargenPorcentualAnterior.Should().BeNull();
        tablero.VariacionMargenPuntos.Should().BeNull();
    }

    [Fact]
    public async Task ObtenerTablero_ConVentasEnAmbosPeriodos_CalculaMargenYVariaciones()
    {
        _consultas.ObtenerResumenVentasAsync(Arg.Any<RangoUtcDto>(), Arg.Any<CancellationToken>())
            .Returns(new ResumenVentasFila(10, 1000m, 50m, 600m), new ResumenVentasFila(8, 500m, 0m, 350m));

        var tablero = await CrearServicio().ObtenerTableroAsync(
            IdGerente, new FiltroPeriodoDto { Desde = Dia(2026, 10, 1), Hasta = Dia(2026, 10, 15) });

        tablero.TotalVendido.VariacionPorcentual.Should().Be(100m);
        tablero.CantidadVentas.VariacionPorcentual.Should().Be(25m);
        tablero.MargenBruto.Valor.Should().Be(400m);
        tablero.MargenBruto.ValorAnterior.Should().Be(150m);
        tablero.MargenBruto.VariacionPorcentual.Should().Be(166.67m);
        tablero.MargenPorcentual.Should().Be(40m);
        tablero.MargenPorcentualAnterior.Should().Be(30m);
        tablero.VariacionMargenPuntos.Should().Be(10m);
    }

    [Fact]
    public async Task ObtenerTablero_SinVentasEnElPeriodo_ElMargenPorcentualEsNulo()
    {
        var tablero = await CrearServicio().ObtenerTableroAsync(
            IdGerente, new FiltroPeriodoDto { Desde = Dia(2026, 10, 1), Hasta = Dia(2026, 10, 15) });

        tablero.MargenPorcentual.Should().BeNull();
        tablero.VariacionMargenPuntos.Should().BeNull();
    }

    [Theory]
    [InlineData(2026, 10, 1, 2026, 10, 15, AgrupacionTemporalEnum.Dia)]
    [InlineData(2026, 8, 15, 2026, 10, 15, AgrupacionTemporalEnum.Dia)]
    [InlineData(2026, 8, 14, 2026, 10, 15, AgrupacionTemporalEnum.Semana)]
    [InlineData(2025, 10, 15, 2026, 10, 15, AgrupacionTemporalEnum.Semana)]
    [InlineData(2025, 10, 14, 2026, 10, 15, AgrupacionTemporalEnum.Mes)]
    public async Task ObtenerTablero_SegunLosDiasDelPeriodo_ElegiLaAgrupacionDeLaEvolucion(
        int anioDesde, int mesDesde, int diaDesde, int anioHasta, int mesHasta, int diaHasta, AgrupacionTemporalEnum esperada)
    {
        var tablero = await CrearServicio().ObtenerTableroAsync(
            IdGerente,
            new FiltroPeriodoDto { Desde = Dia(anioDesde, mesDesde, diaDesde), Hasta = Dia(anioHasta, mesHasta, diaHasta) });

        tablero.AgrupacionEvolucion.Should().Be(esperada);
        await _consultas.Received(1).ObtenerVentasPorPeriodoAsync(
            Arg.Any<RangoUtcDto>(), esperada, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ObtenerTablero_ConInventarioYCategorias_CompletaLosIndicadoresRestantes()
    {
        _consultas.ObtenerResumenInventarioAsync(Arg.Any<FiltroInventarioDto>(), Arg.Any<CancellationToken>())
            .Returns(new ResumenInventarioFila(120, 7, 15000.456m, 25000m));
        _consultas.ObtenerRentabilidadAsync(Arg.Any<RangoUtcDto>(), Arg.Any<FiltroRentabilidadDto>(), Arg.Any<CancellationToken>())
            .Returns(new List<RentabilidadFila>
            {
                new(1, "Libros", 10, 100m, 80m),
                new(2, "Papelería", 5, 100m, 40m)
            });

        var tablero = await CrearServicio().ObtenerTableroAsync(
            IdGerente, new FiltroPeriodoDto { Desde = Dia(2026, 10, 1), Hasta = Dia(2026, 10, 15) });

        tablero.ArticulosBajoStockMinimo.Should().Be(7);
        tablero.ValorizacionInventarioCosto.Should().Be(15000.46m);
        tablero.MargenPorCategoria.Select(f => f.Grupo).Should().Equal("Papelería", "Libros");
        await _consultas.Received(1).ObtenerRentabilidadAsync(
            Arg.Any<RangoUtcDto>(),
            Arg.Is<FiltroRentabilidadDto>(f => f.AgruparPor == AgrupacionRentabilidadEnum.Categoria),
            Arg.Any<CancellationToken>());
    }

    // ---------- Ventas por período ----------

    [Fact]
    public async Task ObtenerVentasPorPeriodo_PorDiaConHuecos_CompletaConCeros()
    {
        _consultas.ObtenerVentasPorPeriodoAsync(Arg.Any<RangoUtcDto>(), Arg.Any<AgrupacionTemporalEnum>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new List<VentasPeriodoFila>
            {
                new(Dia(2026, 10, 11), 2, 300.005m, 10m),
                new(Dia(2026, 10, 14), 1, 100m, 0m)
            });

        var resultado = await CrearServicio().ObtenerVentasPorPeriodoAsync(
            IdGerente, new FiltroVentasPorPeriodoDto { Desde = Dia(2026, 10, 10), Hasta = Dia(2026, 10, 15) });

        resultado.Puntos.Select(p => p.InicioPeriodo.Day).Should().Equal(10, 11, 12, 13, 14, 15);
        resultado.Puntos.Select(p => p.CantidadVentas).Should().Equal(0, 2, 0, 0, 1, 0);
        resultado.Puntos[1].TotalVendido.Should().Be(300.01m);
        resultado.CantidadVentas.Should().Be(3);
        resultado.TotalVendido.Should().Be(400.01m);
        resultado.Descuentos.Should().Be(10m);
    }

    [Fact]
    public async Task ObtenerVentasPorPeriodo_PorSemana_EmpiezaElLunesDeLaSemanaDeDesde()
    {
        // 01/10/2026 es jueves: la primera semana arranca el lunes 28/09.
        var resultado = await CrearServicio().ObtenerVentasPorPeriodoAsync(
            IdGerente,
            new FiltroVentasPorPeriodoDto
            {
                Desde = Dia(2026, 10, 1),
                Hasta = Dia(2026, 10, 15),
                Agrupacion = AgrupacionTemporalEnum.Semana
            });

        resultado.Puntos.Select(p => p.InicioPeriodo).Should().Equal(Dia(2026, 9, 28), Dia(2026, 10, 5), Dia(2026, 10, 12));
        resultado.Puntos.Should().OnlyContain(p => p.TotalVendido == 0m && p.CantidadVentas == 0);
    }

    [Fact]
    public async Task ObtenerVentasPorPeriodo_PorMes_EmpiezaElDiaUnoDelMesDeDesde()
    {
        var resultado = await CrearServicio().ObtenerVentasPorPeriodoAsync(
            IdGerente,
            new FiltroVentasPorPeriodoDto
            {
                Desde = Dia(2026, 9, 20),
                Hasta = Dia(2026, 10, 15),
                Agrupacion = AgrupacionTemporalEnum.Mes
            });

        resultado.Puntos.Select(p => p.InicioPeriodo).Should().Equal(Dia(2026, 9, 1), Dia(2026, 10, 1));
    }

    // ---------- Ranking ----------

    [Fact]
    public async Task ObtenerRanking_ConResultados_ConservaElOrdenDelPuertoYRedondeaElImporte()
    {
        _consultas.ObtenerMasVendidosAsync(Arg.Any<RangoUtcDto>(), Arg.Any<FiltroRankingDto>(), Arg.Any<CancellationToken>())
            .Returns(new List<RankingFila>
            {
                new(2, "Cuaderno", "779", "Papelería", "Rivadavia", 30, 1500.004m),
                new(1, "Lápiz", null, null, null, 10, 100m)
            });
        _consultas.ObtenerMenosVendidosAsync(Arg.Any<RangoUtcDto>(), Arg.Any<FiltroRankingDto>(), Arg.Any<CancellationToken>())
            .Returns(new List<RankingFila> { new(3, "Regla", null, null, null, 0, 0m) });

        var ranking = await CrearServicio().ObtenerRankingAsync(
            IdGerente, new FiltroRankingDto { Desde = Dia(2026, 10, 1), Hasta = Dia(2026, 10, 15) });

        ranking.MasVendidos.Select(a => a.IdArticulo).Should().Equal(2, 1);
        ranking.MasVendidos[0].IngresoNeto.Should().Be(1500m);
        ranking.MenosVendidos.Should().ContainSingle().Which.Unidades.Should().Be(0);
    }

    // ---------- Rentabilidad ----------

    [Fact]
    public async Task ObtenerRentabilidad_ConGrupoNulo_UsaElNombreSinCategoria()
    {
        _consultas.ObtenerRentabilidadAsync(Arg.Any<RangoUtcDto>(), Arg.Any<FiltroRentabilidadDto>(), Arg.Any<CancellationToken>())
            .Returns(new List<RentabilidadFila> { new(null, null, 3, 100m, 60m) });

        var resultado = await CrearServicio().ObtenerRentabilidadAsync(
            IdGerente, new FiltroRentabilidadDto { Desde = Dia(2026, 10, 1), Hasta = Dia(2026, 10, 15) });

        resultado.Filas.Should().ContainSingle().Which.Grupo.Should().Be("Sin categoría");
    }

    [Fact]
    public async Task ObtenerRentabilidad_AgrupadaPorMarcaConGrupoNulo_UsaElNombreSinMarca()
    {
        _consultas.ObtenerRentabilidadAsync(Arg.Any<RangoUtcDto>(), Arg.Any<FiltroRentabilidadDto>(), Arg.Any<CancellationToken>())
            .Returns(new List<RentabilidadFila> { new(null, null, 3, 100m, 60m) });

        var resultado = await CrearServicio().ObtenerRentabilidadAsync(
            IdGerente,
            new FiltroRentabilidadDto
            {
                Desde = Dia(2026, 10, 1),
                Hasta = Dia(2026, 10, 15),
                AgruparPor = AgrupacionRentabilidadEnum.Marca
            });

        resultado.Filas.Should().ContainSingle().Which.Grupo.Should().Be("Sin marca");
    }

    [Fact]
    public async Task ObtenerRentabilidad_ConVariasFilas_OrdenaPorMargenDescendenteYCalculaElMargenPorcentual()
    {
        _consultas.ObtenerRentabilidadAsync(Arg.Any<RangoUtcDto>(), Arg.Any<FiltroRentabilidadDto>(), Arg.Any<CancellationToken>())
            .Returns(new List<RentabilidadFila>
            {
                new(1, "Libros", 10, 200m, 150m),
                new(2, "Papelería", 20, 300m, 100m)
            });

        var resultado = await CrearServicio().ObtenerRentabilidadAsync(
            IdGerente, new FiltroRentabilidadDto { Desde = Dia(2026, 10, 1), Hasta = Dia(2026, 10, 15) });

        resultado.Filas.Select(f => f.Grupo).Should().Equal("Papelería", "Libros");
        resultado.Filas[0].MargenBruto.Should().Be(200m);
        resultado.Filas[0].MargenPorcentual.Should().BeApproximately(66.67m, 0.001m);
        resultado.Filas[1].MargenPorcentual.Should().Be(25m);
    }

    [Fact]
    public async Task ObtenerRentabilidad_ConImportesConFraccion_ElTotalSumaLasFilasCrudasAntesDeRedondear()
    {
        _consultas.ObtenerRentabilidadAsync(Arg.Any<RangoUtcDto>(), Arg.Any<FiltroRentabilidadDto>(), Arg.Any<CancellationToken>())
            .Returns(new List<RentabilidadFila>
            {
                new(1, "A", 1, 0.004m, 0m),
                new(2, "B", 2, 0.004m, 0m)
            });

        var resultado = await CrearServicio().ObtenerRentabilidadAsync(
            IdGerente, new FiltroRentabilidadDto { Desde = Dia(2026, 10, 1), Hasta = Dia(2026, 10, 15) });

        resultado.Filas.Should().OnlyContain(f => f.IngresoNeto == 0m);
        resultado.Total.Grupo.Should().Be("Total");
        resultado.Total.IdGrupo.Should().BeNull();
        resultado.Total.Unidades.Should().Be(3);
        resultado.Total.IngresoNeto.Should().Be(0.01m);
    }

    [Fact]
    public async Task ObtenerRentabilidad_ConIngresoCero_ElMargenPorcentualEsNulo()
    {
        _consultas.ObtenerRentabilidadAsync(Arg.Any<RangoUtcDto>(), Arg.Any<FiltroRentabilidadDto>(), Arg.Any<CancellationToken>())
            .Returns(new List<RentabilidadFila> { new(1, "Regalos", 2, 0m, 20m) });

        var resultado = await CrearServicio().ObtenerRentabilidadAsync(
            IdGerente, new FiltroRentabilidadDto { Desde = Dia(2026, 10, 1), Hasta = Dia(2026, 10, 15) });

        resultado.Filas[0].MargenBruto.Should().Be(-20m);
        resultado.Filas[0].MargenPorcentual.Should().BeNull();
    }

    // ---------- Inventario ----------

    [Fact]
    public async Task ObtenerStockCritico_ConArticulosBajoMinimo_CalculaElFaltanteYOrdenaPorFaltanteDescendente()
    {
        _consultas.ObtenerResumenInventarioAsync(Arg.Any<FiltroInventarioDto>(), Arg.Any<CancellationToken>())
            .Returns(new ResumenInventarioFila(50, 2, 1234.567m, 2000m));
        _consultas.ObtenerStockCriticoAsync(Arg.Any<FiltroInventarioDto>(), Arg.Any<CancellationToken>())
            .Returns(new List<StockCriticoFila>
            {
                new(1, "Lápiz", null, null, null, 4, 5, 10m, 15m),
                new(2, "Cuaderno", null, null, null, 0, 6, 100m, 150m)
            });

        var resultado = await CrearServicio().ObtenerStockCriticoAsync(IdGerente, new FiltroInventarioDto());

        resultado.Filas.Select(f => f.Descripcion).Should().Equal("Cuaderno", "Lápiz");
        resultado.Filas.Select(f => f.Faltante).Should().Equal(6, 1);
        resultado.ArticulosConsiderados.Should().Be(50);
        resultado.ValorizacionCosto.Should().Be(1234.57m);
        resultado.ValorizacionVenta.Should().Be(2000m);
    }

    [Fact]
    public async Task ObtenerSinMovimiento_ConDias_CalculaLaFechaDeCorteLocalYLaConvierteAUtc()
    {
        var resultado = await CrearServicio().ObtenerSinMovimientoAsync(
            IdGerente, new FiltroSinMovimientoDto { Dias = 90 });

        var corte = Dia(2026, 10, 15).AddDays(-90);
        resultado.SinVentasDesde.Should().Be(corte);
        await _consultas.Received(1).ObtenerSinMovimientoAsync(
            Arg.Any<FiltroInventarioDto>(),
            Arg.Is<DateTime>(d => d == Utc(corte.Year, corte.Month, corte.Day, 3)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ObtenerSinMovimiento_ConArticulos_ConvierteLaUltimaVentaAHoraLocalYCalculaElCapital()
    {
        _consultas.ObtenerSinMovimientoAsync(Arg.Any<FiltroInventarioDto>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<SinMovimientoFila>
            {
                new(1, "Agenda", null, null, null, 4, 10.50m, Utc(2026, 5, 10, 2)),
                new(2, "Carpeta", null, null, null, 100, 20m, null)
            });

        var resultado = await CrearServicio().ObtenerSinMovimientoAsync(
            IdGerente, new FiltroSinMovimientoDto { Dias = 90 });

        resultado.Filas.Select(f => f.Descripcion).Should().Equal("Carpeta", "Agenda");
        resultado.Filas[0].UltimaVenta.Should().BeNull();
        resultado.Filas[0].CapitalInmovilizado.Should().Be(2000m);
        resultado.Filas[1].UltimaVenta.Should().Be(new DateTime(2026, 5, 9, 23, 0, 0));
        resultado.Filas[1].CapitalInmovilizado.Should().Be(42m);
        resultado.CapitalInmovilizadoTotal.Should().Be(2042m);
    }
}

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

    // Una primera venta muy antigua para que el límite inferior (D-32) no interfiera con las pruebas que no lo tratan.
    private static readonly DateTime? PrimeraVentaPorDefecto = new DateTime(2020, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IReporteQueryService _consultas = Substitute.For<IReporteQueryService>();
    private readonly IRepository<Usuario> _usuarios = Substitute.For<IRepository<Usuario>>();

    public ReporteServiceTests()
    {
        RegistrarUsuario(IdGerente, RolUsuarioEnum.Gerente);
        RegistrarUsuario(IdCajero, RolUsuarioEnum.Cajero);

        // Respuestas vacías por defecto: cada prueba pisa solo lo que le interesa.
        _consultas.ObtenerFechaPrimeraVentaAsync(Arg.Any<CancellationToken>()).Returns(PrimeraVentaPorDefecto);
        _consultas.ObtenerResumenVentasAsync(Arg.Any<RangoUtcDto>(), Arg.Any<CancellationToken>())
            .Returns(new ResumenVentasFila(0, 0m, 0m, 0m));
        _consultas.ObtenerVentasPorPeriodoAsync(Arg.Any<RangoUtcDto>(), Arg.Any<AgrupacionTemporalEnum>(), Arg.Any<int?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
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
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ObtenerVentasPorPeriodo_PideElDesglosePorMedioDePago()
    {
        await CrearServicio().ObtenerVentasPorPeriodoAsync(
            IdGerente, new FiltroVentasPorPeriodoDto { Desde = Dia(2026, 10, 10), Hasta = Dia(2026, 10, 12), IdUsuario = 2 });

        await _consultas.Received(1).ObtenerVentasPorPeriodoAsync(
            Arg.Any<RangoUtcDto>(), Arg.Any<AgrupacionTemporalEnum>(), 2, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ObtenerTablero_NoPideElDesglosePorMedioDePago()
    {
        await CrearServicio().ObtenerTableroAsync(
            IdGerente, new FiltroPeriodoDto { Desde = Dia(2026, 10, 1), Hasta = Dia(2026, 10, 15) });

        await _consultas.Received(1).ObtenerVentasPorPeriodoAsync(
            Arg.Any<RangoUtcDto>(), Arg.Any<AgrupacionTemporalEnum>(), null, false, Arg.Any<CancellationToken>());
        await _consultas.DidNotReceive().ObtenerVentasPorPeriodoAsync(
            Arg.Any<RangoUtcDto>(), Arg.Any<AgrupacionTemporalEnum>(), Arg.Any<int?>(), true, Arg.Any<CancellationToken>());
    }

    // ---------- Tablero ----------

    [Fact]
    public async Task ObtenerTablero_ConOctubreCompleto_ConsultaSeptiembreCompletoComoPeriodoAnterior()
    {
        var servicio = CrearServicio(new DateTimeOffset(2026, 11, 2, 15, 0, 0, TimeSpan.Zero));

        var tablero = await servicio.ObtenerTableroAsync(
            IdGerente, new FiltroPeriodoDto { Desde = Dia(2026, 10, 1), Hasta = Dia(2026, 10, 31) });

        tablero.DesdeAnterior.Should().Be(Dia(2026, 9, 1));
        tablero.HastaAnterior.Should().Be(Dia(2026, 9, 30));
        await _consultas.Received(1).ObtenerResumenVentasAsync(
            Arg.Is<RangoUtcDto>(r => r.DesdeUtc == Utc(2026, 9, 1, 3) && r.HastaExclusivoUtc == Utc(2026, 10, 1, 3)),
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
            Arg.Any<RangoUtcDto>(), esperada, null, false, Arg.Any<CancellationToken>());
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
        _consultas.ObtenerVentasPorPeriodoAsync(Arg.Any<RangoUtcDto>(), Arg.Any<AgrupacionTemporalEnum>(), Arg.Any<int?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
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
    public async Task ObtenerVentasPorPeriodo_ConDesglosePorMedio_RedondeaCadaImporteYLaSumaCoincideConElTotal()
    {
        // 300,00 = 100,004 + 100,004 + 50 + 25 + 24,992 (cada medio se redondea solo; los totales suman crudo y redondean al final)
        _consultas.ObtenerVentasPorPeriodoAsync(Arg.Any<RangoUtcDto>(), Arg.Any<AgrupacionTemporalEnum>(), Arg.Any<int?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new List<VentasPeriodoFila>
            {
                new(Dia(2026, 10, 11), 2, 300m, 0m, Efectivo: 100.004m, TarjetaDebito: 100.004m, TarjetaCredito: 50m, TransferenciaQr: 25m, CuentaCorriente: 24.992m),
                new(Dia(2026, 10, 12), 1, 100.01m, 0m, Efectivo: 100.01m)
            });

        var resultado = await CrearServicio().ObtenerVentasPorPeriodoAsync(
            IdGerente, new FiltroVentasPorPeriodoDto { Desde = Dia(2026, 10, 10), Hasta = Dia(2026, 10, 13) });

        var punto = resultado.Puntos[1];
        punto.Efectivo.Should().Be(100m);
        punto.TarjetaDebito.Should().Be(100m);
        punto.TarjetaCredito.Should().Be(50m);
        punto.TransferenciaQr.Should().Be(25m);
        punto.CuentaCorriente.Should().Be(24.99m);
        resultado.Puntos[2].Efectivo.Should().Be(100.01m);
        resultado.Efectivo.Should().Be(200.01m, "suma cruda 200,014 y recién después redondeo");
        resultado.TarjetaDebito.Should().Be(100m);
        resultado.TarjetaCredito.Should().Be(50m);
        resultado.TransferenciaQr.Should().Be(25m);
        resultado.CuentaCorriente.Should().Be(24.99m);
    }

    [Fact]
    public async Task ObtenerVentasPorPeriodo_ConDatosCoherentes_LaSumaDeLosMediosEsElTotalEnCadaPuntoYEnLosTotales()
    {
        _consultas.ObtenerVentasPorPeriodoAsync(Arg.Any<RangoUtcDto>(), Arg.Any<AgrupacionTemporalEnum>(), Arg.Any<int?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new List<VentasPeriodoFila>
            {
                new(Dia(2026, 10, 11), 2, 1500m, 0m, Efectivo: 600m, TarjetaDebito: 400m, TarjetaCredito: 500m),
                new(Dia(2026, 10, 13), 1, 800m, 0m, TransferenciaQr: 300m, CuentaCorriente: 500m)
            });

        var resultado = await CrearServicio().ObtenerVentasPorPeriodoAsync(
            IdGerente, new FiltroVentasPorPeriodoDto { Desde = Dia(2026, 10, 10), Hasta = Dia(2026, 10, 14) });

        foreach (var p in resultado.Puntos)
        {
            (p.Efectivo + p.TarjetaDebito + p.TarjetaCredito + p.TransferenciaQr + p.CuentaCorriente).Should().Be(p.TotalVendido);
        }

        (resultado.Efectivo + resultado.TarjetaDebito + resultado.TarjetaCredito + resultado.TransferenciaQr + resultado.CuentaCorriente)
            .Should().Be(resultado.TotalVendido).And.Be(2300m);
    }

    [Fact]
    public async Task ObtenerVentasPorPeriodo_ConPeriodosSinVentas_RellenaLosMediosConCero()
    {
        _consultas.ObtenerVentasPorPeriodoAsync(Arg.Any<RangoUtcDto>(), Arg.Any<AgrupacionTemporalEnum>(), Arg.Any<int?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new List<VentasPeriodoFila> { new(Dia(2026, 10, 11), 1, 100m, 0m, Efectivo: 100m) });

        var resultado = await CrearServicio().ObtenerVentasPorPeriodoAsync(
            IdGerente, new FiltroVentasPorPeriodoDto { Desde = Dia(2026, 10, 10), Hasta = Dia(2026, 10, 12) });

        var vacio = resultado.Puntos[0];
        vacio.Efectivo.Should().Be(0m);
        vacio.TarjetaDebito.Should().Be(0m);
        vacio.TarjetaCredito.Should().Be(0m);
        vacio.TransferenciaQr.Should().Be(0m);
        vacio.CuentaCorriente.Should().Be(0m);
        resultado.Puntos[2].Efectivo.Should().Be(0m);
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

    // ---------- Período de comparación calendario (D-31, RF-24) ----------

    [Theory]
    [InlineData(2026, 10, 1, 2026, 10, 15, 2026, 9, 1, 2026, 9, 15)]    // tramo de un mes
    [InlineData(2026, 9, 1, 2026, 9, 30, 2026, 8, 1, 2026, 8, 31)]      // mes completo de 30 vs. 31 días
    [InlineData(2026, 3, 1, 2026, 3, 31, 2026, 2, 1, 2026, 2, 28)]      // marzo vs. febrero
    [InlineData(2028, 3, 1, 2028, 3, 31, 2028, 2, 1, 2028, 2, 29)]      // marzo vs. febrero bisiesto
    [InlineData(2026, 3, 1, 2026, 3, 30, 2026, 2, 1, 2026, 2, 28)]      // 30/03 - 1 mes se recorta a 28/02
    [InlineData(2028, 3, 1, 2028, 3, 30, 2028, 2, 1, 2028, 2, 29)]      // ... y a 29/02 si es bisiesto
    [InlineData(2026, 1, 1, 2026, 1, 31, 2025, 12, 1, 2025, 12, 31)]    // enero es regla (a): contra diciembre
    [InlineData(2026, 1, 1, 2026, 10, 15, 2025, 1, 1, 2025, 10, 15)]    // año hasta la fecha, interanual
    [InlineData(2026, 1, 1, 2026, 12, 31, 2025, 1, 1, 2025, 12, 31)]    // año completo
    [InlineData(2028, 1, 1, 2028, 2, 29, 2027, 1, 1, 2027, 2, 28)]      // 29/02 - 1 año se recorta a 28/02
    [InlineData(2026, 9, 2, 2026, 10, 10, 2026, 7, 25, 2026, 9, 1)]     // cruza meses y no empieza el 1: igual duración
    [InlineData(2026, 10, 10, 2026, 10, 15, 2026, 10, 4, 2026, 10, 9)]  // dentro de un mes pero no desde el día 1
    public void CalcularPeriodoAnterior_SegunLasReglasCalendario_DevuelveElPeriodoEsperado(
        int aD, int mD, int dD, int aH, int mH, int dH, int aAD, int mAD, int dAD, int aAH, int mAH, int dAH)
    {
        var (desde, hasta) = ReporteService.CalcularPeriodoAnterior(Dia(aD, mD, dD), Dia(aH, mH, dH));

        desde.Should().Be(Dia(aAD, mAD, dAD));
        hasta.Should().Be(Dia(aAH, mAH, dAH));
    }

    [Fact]
    public async Task ObtenerTablero_ConMesCompleto_UsaElPeriodoCalendarioAnteriorEnElDtoYEnLaConsulta()
    {
        var resultado = await CrearServicio().ObtenerTableroAsync(
            IdGerente, new FiltroPeriodoDto { Desde = Dia(2026, 9, 1), Hasta = Dia(2026, 9, 30) });

        resultado.DesdeAnterior.Should().Be(Dia(2026, 8, 1));
        resultado.HastaAnterior.Should().Be(Dia(2026, 8, 31));
        await _consultas.Received(1).ObtenerResumenVentasAsync(
            Arg.Is<RangoUtcDto>(r => r.DesdeUtc == Utc(2026, 8, 1, 3) && r.HastaExclusivoUtc == Utc(2026, 9, 1, 3)),
            Arg.Any<CancellationToken>());
    }

    // ---------- Límites del período (D-32) ----------

    [Fact]
    public async Task ObtenerFechaMinima_ConVentas_DevuelveElDiaLocalDeLaPrimeraVenta()
    {
        // 02:00 UTC del 10/03 son las 23:00 del 09/03 en Argentina.
        _consultas.ObtenerFechaPrimeraVentaAsync(Arg.Any<CancellationToken>()).Returns(Utc(2026, 3, 10, 2));

        var minima = await CrearServicio().ObtenerFechaMinimaAsync(IdGerente);

        minima.Should().Be(Dia(2026, 3, 9));
    }

    [Fact]
    public async Task ObtenerFechaMinima_SinVentas_DevuelveElDiaUnoDelMesEnCurso()
    {
        _consultas.ObtenerFechaPrimeraVentaAsync(Arg.Any<CancellationToken>()).Returns((DateTime?)null);

        var minima = await CrearServicio().ObtenerFechaMinimaAsync(IdGerente);

        minima.Should().Be(Dia(2026, 10, 1));
    }

    [Fact]
    public async Task ObtenerFechaMinima_ConActorCajero_LanzaOperacionNoAutorizadaYNoConsulta()
    {
        var accion = () => CrearServicio().ObtenerFechaMinimaAsync(IdCajero);

        await accion.Should().ThrowAsync<OperacionNoAutorizadaException>();
        _consultas.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task ObtenerTablero_ConDesdeAnteriorALaPrimeraVenta_LanzaValidationExceptionYNoConsultaDatos()
    {
        _consultas.ObtenerFechaPrimeraVentaAsync(Arg.Any<CancellationToken>()).Returns(Utc(2026, 9, 10, 15));

        var accion = () => CrearServicio().ObtenerTableroAsync(
            IdGerente, new FiltroPeriodoDto { Desde = Dia(2026, 9, 9), Hasta = Dia(2026, 10, 15) });

        await accion.Should().ThrowAsync<ValidationException>()
            .WithMessage("*La fecha desde no puede ser anterior al 10/09/2026, la primera venta registrada.*");
        await _consultas.DidNotReceive().ObtenerResumenVentasAsync(Arg.Any<RangoUtcDto>(), Arg.Any<CancellationToken>());
        await _consultas.Received(1).ObtenerFechaPrimeraVentaAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ObtenerTablero_ConDesdeIgualALaPrimeraVenta_NoLanza()
    {
        _consultas.ObtenerFechaPrimeraVentaAsync(Arg.Any<CancellationToken>()).Returns(Utc(2026, 9, 10, 15));

        var accion = () => CrearServicio().ObtenerTableroAsync(
            IdGerente, new FiltroPeriodoDto { Desde = Dia(2026, 9, 10), Hasta = Dia(2026, 10, 15) });

        await accion.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ObtenerVentasPorPeriodo_ConDesdeAnteriorALaPrimeraVenta_LanzaValidationExceptionYNoConsultaDatos()
    {
        _consultas.ObtenerFechaPrimeraVentaAsync(Arg.Any<CancellationToken>()).Returns(Utc(2026, 9, 10, 15));

        var accion = () => CrearServicio().ObtenerVentasPorPeriodoAsync(
            IdGerente, new FiltroVentasPorPeriodoDto { Desde = Dia(2026, 9, 1), Hasta = Dia(2026, 9, 30) });

        await accion.Should().ThrowAsync<ValidationException>();
        await _consultas.DidNotReceive().ObtenerVentasPorPeriodoAsync(
            Arg.Any<RangoUtcDto>(), Arg.Any<AgrupacionTemporalEnum>(), Arg.Any<int?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ObtenerRanking_ConDesdeAnteriorALaPrimeraVenta_LanzaValidationExceptionYNoConsultaDatos()
    {
        _consultas.ObtenerFechaPrimeraVentaAsync(Arg.Any<CancellationToken>()).Returns(Utc(2026, 9, 10, 15));

        var accion = () => CrearServicio().ObtenerRankingAsync(
            IdGerente, new FiltroRankingDto { Desde = Dia(2026, 9, 1), Hasta = Dia(2026, 9, 30) });

        await accion.Should().ThrowAsync<ValidationException>();
        await _consultas.DidNotReceive().ObtenerMasVendidosAsync(
            Arg.Any<RangoUtcDto>(), Arg.Any<FiltroRankingDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ObtenerRentabilidad_ConDesdeAnteriorALaPrimeraVenta_LanzaValidationExceptionYNoConsultaDatos()
    {
        _consultas.ObtenerFechaPrimeraVentaAsync(Arg.Any<CancellationToken>()).Returns(Utc(2026, 9, 10, 15));

        var accion = () => CrearServicio().ObtenerRentabilidadAsync(
            IdGerente, new FiltroRentabilidadDto { Desde = Dia(2026, 9, 1), Hasta = Dia(2026, 9, 30) });

        await accion.Should().ThrowAsync<ValidationException>();
        await _consultas.DidNotReceive().ObtenerRentabilidadAsync(
            Arg.Any<RangoUtcDto>(), Arg.Any<FiltroRentabilidadDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ObtenerRanking_SinVentasYDesdeAnteriorAlMesEnCurso_LanzaConMensajeSinReferenciaAVentas()
    {
        _consultas.ObtenerFechaPrimeraVentaAsync(Arg.Any<CancellationToken>()).Returns((DateTime?)null);

        var accion = () => CrearServicio().ObtenerRankingAsync(
            IdGerente, new FiltroRankingDto { Desde = Dia(2026, 9, 30), Hasta = Dia(2026, 10, 15) });

        var excepcion = await accion.Should().ThrowAsync<ValidationException>();
        excepcion.Which.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("La fecha desde no puede ser anterior al 01/10/2026.");
    }
}

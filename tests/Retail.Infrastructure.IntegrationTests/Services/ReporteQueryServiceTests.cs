using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Reportes;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.IntegrationTests.TestData;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Services;
using Xunit;
using Xunit.Abstractions;

namespace Retail.Infrastructure.IntegrationTests.Services;

/// <summary>
/// Consultas de los reportes gerenciales (RF-24, RF-25, RNF-09) contra SQL Server Express. Verifican lo que un test
/// unitario no puede: que EF traduzca el prorrateo, las agrupaciones por fecha local y los LEFT JOIN, y que el
/// resultado sea correcto con ventas, ítems y artículos con baja lógica.
/// </summary>
public class ReporteQueryServiceTests : IAsyncLifetime, IDisposable
{
    private const int DesfaseArgentina = -180;

    private readonly ITestOutputHelper _salida;
    private readonly string _dbName = $"RetailDb_Test_Reportes_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;
    private ReporteQueryService _sut = null!;
    private int _idCajero1;
    private int _idCajero2;
    private int _idTurno;

    public ReporteQueryServiceTests(ITestOutputHelper salida)
    {
        _salida = salida;
    }

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(ServidorDePruebas.CadenaDeConexion(_dbName))
            .Options;
        _context = new RetailDbContext(options);
        await _context.Database.MigrateAsync();
        _sut = new ReporteQueryService(_context);

        _context.Roles.Add(new Rol { NombreRol = "Cajero", Descripcion = "Atiende el mostrador" });
        await _context.SaveChangesAsync();

        var cajero1 = Usuario.Crear("cajero1", "Cajero", "Uno", "hash", RolUsuarioEnum.Cajero, null);
        var cajero2 = Usuario.Crear("cajero2", "Cajero", "Dos", "hash", RolUsuarioEnum.Cajero, null);
        _context.Usuarios.AddRange(cajero1, cajero2);
        await _context.SaveChangesAsync();

        // Solo puede haber un turno abierto (índice único): las ventas de ambos cajeros comparten el turno.
        var turno = TurnoCaja.Abrir(cajero1.Id, 0m);
        _context.TurnosCaja.Add(turno);
        await _context.SaveChangesAsync();

        _idCajero1 = cajero1.Id;
        _idCajero2 = cajero2.Id;
        _idTurno = turno.Id;
        _context.ChangeTracker.Clear();
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    public void Dispose()
    {
        _context?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ItemsVendidos_DescuentoGlobal_ProrrateaElIngresoNetoSegunElSubtotalDeCadaItem()
    {
        // Arrange: subtotales 300 y 700, descuento 100 => 270 y 630
        var a = await ArticuloAsync("Cuaderno", precio: 150m, costo: 100m);
        var b = await ArticuloAsync("Mochila", precio: 700m, costo: 400m);
        int idVenta = await VenderAsync(Utc(2026, 10, 14, 15), _idCajero1, [(a, 2), (b, 1)], descuento: 100m);
        var rango = RangoLocal(new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 14));

        // Act
        var porArticulo = await _sut.ObtenerRentabilidadAsync(rango, FiltroRent(AgrupacionRentabilidadEnum.Articulo));
        var resumen = await _sut.ObtenerResumenVentasAsync(rango);

        // Assert
        porArticulo.Single(f => f.Grupo == "Cuaderno").IngresoNeto.Should().Be(270m);
        porArticulo.Single(f => f.Grupo == "Mochila").IngresoNeto.Should().Be(630m);
        porArticulo.Sum(f => f.IngresoNeto).Should().Be(900m, "la suma de los ítems coincide con Venta.Total");
        resumen.TotalVendido.Should().Be(900m);
        resumen.Descuentos.Should().Be(100m);
        resumen.CostoTotal.Should().Be(2 * 100m + 400m);
        idVenta.Should().BePositive();
    }

    [Fact]
    public async Task Consultas_ArticuloDadoDeBajaDespuesDeVender_SigueEnMasVendidosYRentabilidadPeroNoEnMenosVendidos()
    {
        // Arrange
        var viejo = await ArticuloAsync("Agenda discontinuada", precio: 500m, costo: 200m);
        var vigente = await ArticuloAsync("Lapicera", precio: 100m, costo: 50m);
        await VenderAsync(Utc(2026, 10, 14, 15), _idCajero1, [(viejo, 3), (vigente, 1)]);
        await DarDeBajaArticuloAsync(viejo.Id);
        var rango = RangoLocal(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));
        var filtro = new FiltroRankingDto { Cantidad = 10 };

        // Act
        var mas = await _sut.ObtenerMasVendidosAsync(rango, filtro);
        var menos = await _sut.ObtenerMenosVendidosAsync(rango, filtro);
        var rentabilidad = await _sut.ObtenerRentabilidadAsync(rango, FiltroRent(AgrupacionRentabilidadEnum.Articulo));

        // Assert (R-1)
        mas.Select(f => f.Descripcion).Should().Equal("Agenda discontinuada", "Lapicera");
        menos.Select(f => f.IdArticulo).Should().NotContain(viejo.Id);
        menos.Select(f => f.IdArticulo).Should().Contain(vigente.Id);
        rentabilidad.Select(f => f.Grupo).Should().Contain("Agenda discontinuada");
    }

    [Fact]
    public async Task Consultas_VentaConBajaLogica_NoSumaEnResumenPeriodoRankingNiRentabilidad()
    {
        // Arrange
        var a = await ArticuloAsync("Resma", precio: 1000m, costo: 600m);
        await VenderAsync(Utc(2026, 10, 14, 15), _idCajero1, [(a, 1)]);
        int idBorrada = await VenderAsync(Utc(2026, 10, 14, 16), _idCajero1, [(a, 5)]);
        await DarDeBajaVentaAsync(idBorrada);
        var rango = RangoLocal(new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 14));

        // Act
        var resumen = await _sut.ObtenerResumenVentasAsync(rango);
        var periodo = await _sut.ObtenerVentasPorPeriodoAsync(rango, AgrupacionTemporalEnum.Dia, null, incluirMediosDePago: true);
        var ranking = await _sut.ObtenerMasVendidosAsync(rango, new FiltroRankingDto());
        var rentabilidad = await _sut.ObtenerRentabilidadAsync(rango, FiltroRent(AgrupacionRentabilidadEnum.Articulo));

        // Assert
        resumen.CantidadVentas.Should().Be(1);
        resumen.TotalVendido.Should().Be(1000m);
        resumen.CostoTotal.Should().Be(600m);
        periodo.Single().CantidadVentas.Should().Be(1);
        ranking.Single().Unidades.Should().Be(1);
        rentabilidad.Single().Unidades.Should().Be(1);
    }

    [Fact]
    public async Task ObtenerVentasPorPeriodo_VentaCercaDeLaMedianocheUtc_CaeEnElDiaLocalYRespetaElRango()
    {
        // Arrange: 02:30 UTC del 16/10 son las 23:30 del 15/10 en Argentina
        var a = await ArticuloAsync("Regla", precio: 100m, costo: 40m);
        await VenderAsync(Utc(2026, 10, 16, 2, 30), _idCajero1, [(a, 1)]);

        // Act
        var dia15 = await _sut.ObtenerVentasPorPeriodoAsync(
            RangoLocal(new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 15)), AgrupacionTemporalEnum.Dia, null, incluirMediosDePago: true);
        var resumen16 = await _sut.ObtenerResumenVentasAsync(
            RangoLocal(new DateOnly(2026, 10, 16), new DateOnly(2026, 10, 16)));

        // Assert
        dia15.Should().ContainSingle().Which.InicioPeriodo.Should().Be(new DateOnly(2026, 10, 15));
        resumen16.CantidadVentas.Should().Be(0);
    }

    [Fact]
    public async Task ObtenerVentasPorPeriodo_SemanaYMes_AgrupaPorLunesYPorMesLocales()
    {
        // Arrange: domingo 18/10, lunes 19/10, sábado 31/10 y domingo 01/11 (todas al mediodía local)
        var a = await ArticuloAsync("Goma", precio: 50m, costo: 20m);
        await VenderAsync(Utc(2026, 10, 18, 15), _idCajero1, [(a, 1)]);
        await VenderAsync(Utc(2026, 10, 19, 15), _idCajero1, [(a, 1)]);
        await VenderAsync(Utc(2026, 10, 31, 15), _idCajero1, [(a, 1)]);
        await VenderAsync(Utc(2026, 11, 1, 15), _idCajero1, [(a, 1)]);
        var rango = RangoLocal(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 30));

        // Act
        var semanas = await _sut.ObtenerVentasPorPeriodoAsync(rango, AgrupacionTemporalEnum.Semana, null, incluirMediosDePago: true);
        var meses = await _sut.ObtenerVentasPorPeriodoAsync(rango, AgrupacionTemporalEnum.Mes, null, incluirMediosDePago: true);

        // Assert (D-10: la semana empieza el lunes)
        semanas.Select(s => s.InicioPeriodo).Should().Equal(
            new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 26));
        semanas.Select(s => s.CantidadVentas).Should().Equal(1, 1, 2);
        meses.Select(m => m.InicioPeriodo).Should().Equal(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1));
        meses.Select(m => m.CantidadVentas).Should().Equal(3, 1);
    }

    [Fact]
    public async Task ObtenerVentasPorPeriodo_FiltroDeCajero_SoloCuentaSusVentas()
    {
        // Arrange
        var a = await ArticuloAsync("Block", precio: 200m, costo: 80m);
        await VenderAsync(Utc(2026, 10, 14, 15), _idCajero1, [(a, 1)]);
        await VenderAsync(Utc(2026, 10, 14, 16), _idCajero2, [(a, 2)]);
        var rango = RangoLocal(new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 14));

        // Act
        var delCajero2 = await _sut.ObtenerVentasPorPeriodoAsync(rango, AgrupacionTemporalEnum.Dia, _idCajero2, incluirMediosDePago: true);
        var todos = await _sut.ObtenerVentasPorPeriodoAsync(rango, AgrupacionTemporalEnum.Dia, null, incluirMediosDePago: true);

        // Assert
        delCajero2.Single().CantidadVentas.Should().Be(1);
        delCajero2.Single().TotalVendido.Should().Be(400m);
        todos.Single().CantidadVentas.Should().Be(2);
    }

    [Fact]
    public async Task ObtenerMenosVendidos_ArticuloSinVentas_AparecePrimeroConCeroYLosFiltrosAplicanEnRankingYRentabilidad()
    {
        // Arrange
        var libros = await CategoriaAsync("Libros");
        var papeleria = await CategoriaAsync("Papelería");
        var marcaA = await MarcaAsync("Marca A");
        var marcaB = await MarcaAsync("Marca B");
        var novela = await ArticuloAsync("Novela", 300m, 100m, categoria: libros, marca: marcaA);
        var cuaderno = await ArticuloAsync("Cuaderno", 100m, 40m, categoria: papeleria, marca: marcaB);
        var sinVentas = await ArticuloAsync("Atlas", 900m, 500m, categoria: libros, marca: marcaA);
        await VenderAsync(Utc(2026, 10, 14, 15), _idCajero1, [(novela, 2), (cuaderno, 5)]);
        var rango = RangoLocal(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        // Act
        var menos = await _sut.ObtenerMenosVendidosAsync(rango, new FiltroRankingDto { IdCategoria = libros });
        var masPapeleria = await _sut.ObtenerMasVendidosAsync(rango, new FiltroRankingDto { IdCategoria = papeleria });
        var masMarcaA = await _sut.ObtenerMasVendidosAsync(rango, new FiltroRankingDto { IdMarca = marcaA });
        var rentPapeleria = await _sut.ObtenerRentabilidadAsync(
            rango, FiltroRent(AgrupacionRentabilidadEnum.Articulo) with { IdCategoria = papeleria });
        var rentMarcaA = await _sut.ObtenerRentabilidadAsync(
            rango, FiltroRent(AgrupacionRentabilidadEnum.Articulo) with { IdMarca = marcaA });

        // Assert
        menos.Select(f => (f.IdArticulo, f.Unidades)).Should().Equal((sinVentas.Id, 0), (novela.Id, 2));
        menos[0].Categoria.Should().Be("Libros");
        menos[0].Marca.Should().Be("Marca A");
        masPapeleria.Select(f => f.Descripcion).Should().Equal("Cuaderno");
        masMarcaA.Select(f => f.Descripcion).Should().Equal("Novela");
        rentPapeleria.Select(f => f.Grupo).Should().Equal("Cuaderno");
        rentMarcaA.Select(f => f.Grupo).Should().Equal("Novela");
    }

    [Fact]
    public async Task ObtenerMasVendidos_CriterioImporte_OrdenaPorIngresoNetoYNoPorUnidades()
    {
        // Arrange
        var barato = await ArticuloAsync("Lápiz", 10m, 4m);
        var caro = await ArticuloAsync("Calculadora", 5000m, 3000m);
        await VenderAsync(Utc(2026, 10, 14, 15), _idCajero1, [(barato, 50), (caro, 1)]);
        var rango = RangoLocal(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        // Act
        var porUnidades = await _sut.ObtenerMasVendidosAsync(rango, new FiltroRankingDto { Criterio = CriterioRankingEnum.Unidades });
        var porImporte = await _sut.ObtenerMasVendidosAsync(rango, new FiltroRankingDto { Criterio = CriterioRankingEnum.Importe });
        var primeroSolo = await _sut.ObtenerMasVendidosAsync(rango, new FiltroRankingDto { Criterio = CriterioRankingEnum.Importe, Cantidad = 1 });

        // Assert
        porUnidades[0].Descripcion.Should().Be("Lápiz");
        porImporte[0].Descripcion.Should().Be("Calculadora");
        primeroSolo.Should().ContainSingle();
    }

    [Fact]
    public async Task ObtenerRentabilidad_PorCategoriaConArticuloSinCategoria_DevuelveFilaConGrupoNulo()
    {
        // Arrange: la categoría se da de baja después de vender: su nombre debe seguir saliendo
        var libros = await CategoriaAsync("Libros");
        var conCategoria = await ArticuloAsync("Novela", 300m, 100m, categoria: libros);
        var sinCategoria = await ArticuloAsync("Servicio de anillado", 80m, 20m);
        await VenderAsync(Utc(2026, 10, 14, 15), _idCajero1, [(conCategoria, 1), (sinCategoria, 2)]);
        await _context.Categorias.Where(c => c.Id == libros).ExecuteUpdateAsync(s => s.SetProperty(c => c.DeletedAt, DateTime.UtcNow));
        var rango = RangoLocal(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        // Act
        var filas = await _sut.ObtenerRentabilidadAsync(rango, FiltroRent(AgrupacionRentabilidadEnum.Categoria));
        var porMarca = await _sut.ObtenerRentabilidadAsync(rango, FiltroRent(AgrupacionRentabilidadEnum.Marca));

        // Assert
        filas.Should().HaveCount(2);
        var sin = filas.Single(f => f.IdGrupo is null);
        sin.Grupo.Should().BeNull();
        sin.Unidades.Should().Be(2);
        sin.IngresoNeto.Should().Be(160m);
        sin.Costo.Should().Be(40m);
        filas.Single(f => f.IdGrupo == libros).Grupo.Should().Be("Libros");
        porMarca.Should().ContainSingle().Which.IdGrupo.Should().BeNull();
    }

    [Fact]
    public async Task ObtenerInventario_ConServiciosYStockEnElMinimo_ExcluyeServiciosYCuentaElMinimoComoCritico()
    {
        // Arrange
        var libros = await CategoriaAsync("Libros");
        var enElMinimo = await ArticuloAsync("En el mínimo", 20m, 10m, stock: 5, minimo: 5);
        await ArticuloAsync("Holgado", 8m, 4m, stock: 10, minimo: 2);
        await ArticuloAsync("Anillado", 80m, 20m, stock: 0, minimo: 1, servicio: true);
        await ArticuloAsync("Libro", 100m, 50m, stock: 1, minimo: 3, categoria: libros);

        // Act
        var resumen = await _sut.ObtenerResumenInventarioAsync(new FiltroInventarioDto());
        var critico = await _sut.ObtenerStockCriticoAsync(new FiltroInventarioDto());
        var resumenLibros = await _sut.ObtenerResumenInventarioAsync(new FiltroInventarioDto { IdCategoria = libros });

        // Assert
        resumen.ArticulosConsiderados.Should().Be(3);
        resumen.ArticulosBajoMinimo.Should().Be(2);
        resumen.ValorizacionCosto.Should().Be(5 * 10m + 10 * 4m + 1 * 50m);
        resumen.ValorizacionVenta.Should().Be(5 * 20m + 10 * 8m + 1 * 100m);
        critico.Select(f => f.Descripcion).Should().Equal("Libro", "En el mínimo");
        resumenLibros.ArticulosConsiderados.Should().Be(1);
        resumenLibros.ValorizacionCosto.Should().Be(50m);
    }

    [Fact]
    public async Task ObtenerSinMovimiento_SegunLaUltimaVenta_DevuelveSoloLosInmovilizadosConSuUltimaVenta()
    {
        // Arrange
        var corte = Utc(2026, 9, 1, 0);
        var vendioAntes = await ArticuloAsync("Vendió antes", 100m, 50m, stock: 4);
        var vendioDespues = await ArticuloAsync("Vendió después", 100m, 50m, stock: 4);
        var sinStock = await ArticuloAsync("Sin stock", 100m, 50m, stock: 0);
        var nuncaVendido = await ArticuloAsync("Nunca vendido", 100m, 80m, stock: 3);
        await ArticuloAsync("Servicio", 100m, 0m, stock: 0, servicio: true);
        await VenderAsync(Utc(2026, 8, 1, 15), _idCajero1, [(vendioAntes, 1), (vendioDespues, 1), (sinStock, 1)]);
        await VenderAsync(Utc(2026, 9, 15, 15), _idCajero1, [(vendioDespues, 1)]);

        // Act
        var filas = await _sut.ObtenerSinMovimientoAsync(new FiltroInventarioDto(), corte);

        // Assert: orden por capital inmovilizado (stock x costo): nunca vendido 240, vendió antes 200
        filas.Select(f => f.Descripcion).Should().Equal("Nunca vendido", "Vendió antes");
        filas[0].UltimaVentaUtc.Should().BeNull();
        filas[0].IdArticulo.Should().Be(nuncaVendido.Id);
        filas[1].UltimaVentaUtc.Should().Be(Utc(2026, 8, 1, 15));
    }

    [Fact]
    public async Task Consultas_RangoSinVentas_DevuelveCerosYListasVacias()
    {
        // Arrange
        await ArticuloAsync("Sin ventas", 100m, 50m, stock: 0, minimo: 0);
        var rango = RangoLocal(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        // Act
        var resumen = await _sut.ObtenerResumenVentasAsync(rango);
        var periodos = await _sut.ObtenerVentasPorPeriodoAsync(rango, AgrupacionTemporalEnum.Semana, null, incluirMediosDePago: true);
        var mas = await _sut.ObtenerMasVendidosAsync(rango, new FiltroRankingDto());
        var rentabilidad = await _sut.ObtenerRentabilidadAsync(rango, FiltroRent(AgrupacionRentabilidadEnum.Categoria));
        var inventarioVacio = await _sut.ObtenerResumenInventarioAsync(new FiltroInventarioDto { IdCategoria = 999 });

        // Assert
        resumen.Should().Be(new Retail.Application.DTOs.Reportes.Consultas.ResumenVentasFila(0, 0m, 0m, 0m));
        periodos.Should().BeEmpty();
        mas.Should().BeEmpty();
        rentabilidad.Should().BeEmpty();
        inventarioVacio.ArticulosConsiderados.Should().Be(0);
        inventarioVacio.ValorizacionCosto.Should().Be(0m);
    }

    [Fact]
    public void Consultas_RentabilidadYSemana_SeTraducenASqlConGroupByYDateDiff()
    {
        // Arrange
        var rango = RangoLocal(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));
        var filtro = FiltroRent(AgrupacionRentabilidadEnum.Categoria);

        // Act
        string sqlRentabilidad = _sut.RentabilidadItems(rango, filtro)
            .GroupBy(i => i.IdCategoria)
            .Select(g => new { g.Key, Unidades = g.Sum(i => i.Cantidad), Ingreso = g.Sum(i => i.IngresoNeto), Costo = g.Sum(i => i.Costo) })
            .ToQueryString();
        string sqlSemana = _sut.ConsultaVentasPorSemana(rango);

        _salida.WriteLine("--- RENTABILIDAD (ItemsVendidos + GROUP BY) ---");
        _salida.WriteLine(sqlRentabilidad);
        _salida.WriteLine("--- VENTAS POR SEMANA ---");
        _salida.WriteLine(sqlSemana);

        // Assert
        sqlRentabilidad.Should().Contain("GROUP BY").And.Contain("SUM(");
        sqlRentabilidad.Should().NotContain("[a].[deleted_at]", "el artículo no se filtra por baja (R-1)");
        sqlSemana.Should().Contain("DATEDIFF").And.Contain("DATEADD").And.Contain("GROUP BY");
    }

    private static DateTime Utc(int anio, int mes, int dia, int hora, int minuto = 0)
    {
        return new DateTime(anio, mes, dia, hora, minuto, 0, DateTimeKind.Utc);
    }

    /// <summary>Arma el rango UTC de un período de días locales (UTC-3), como lo hará ReporteService.</summary>
    private static RangoUtcDto RangoLocal(DateOnly desde, DateOnly hasta)
    {
        DateTime desdeUtc = desde.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddMinutes(-DesfaseArgentina);
        DateTime hastaUtc = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddMinutes(-DesfaseArgentina);
        return new RangoUtcDto(desdeUtc, hastaUtc, DesfaseArgentina);
    }

    private static FiltroRentabilidadDto FiltroRent(AgrupacionRentabilidadEnum agruparPor)
    {
        return new FiltroRentabilidadDto { AgruparPor = agruparPor };
    }

    private async Task<int> CategoriaAsync(string nombre)
    {
        var rubro = Rubro.Crear($"Rubro de {nombre}");
        _context.Rubros.Add(rubro);
        await _context.SaveChangesAsync();

        var categoria = Categoria.Crear(nombre, rubro.Id);
        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return categoria.Id;
    }

    private async Task<int> MarcaAsync(string nombre)
    {
        var marca = Marca.Crear(nombre);
        _context.Marcas.Add(marca);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return marca.Id;
    }

    private async Task<Articulo> ArticuloAsync(
        string descripcion,
        decimal precio,
        decimal costo,
        int stock = 10,
        int minimo = 1,
        int? categoria = null,
        int? marca = null,
        bool servicio = false)
    {
        var articulo = new Articulo
        {
            Descripcion = descripcion,
            CostoReposicion = costo,
            PorcentajeGanancia = 100m,
            PrecioVenta = precio,
            StockActual = stock,
            StockMinimo = minimo,
            IdCategoria = categoria,
            IdMarca = marca,
            EsServicio = servicio
        };
        _context.Articulos.Add(articulo);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return articulo;
    }

    /// <summary>Registra una venta con el Dominio y fija su fecha por SQL (FechaHora tiene setter privado).</summary>
    private async Task<int> VenderAsync(
        DateTime fechaUtc,
        int idUsuario,
        (Articulo Articulo, int Cantidad)[] items,
        decimal descuento = 0m)
    {
        var venta = Venta.Registrar(_idTurno, idUsuario, null);
        foreach (var (articulo, cantidad) in items)
        {
            venta.AgregarItem(articulo.Id, cantidad, articulo.PrecioVenta, articulo.CostoReposicion);
        }

        if (descuento > 0m)
        {
            venta.AplicarDescuento(descuento);
        }

        venta.ImputarPago(MedioPagoEnum.Efectivo, venta.Total);
        _context.Ventas.Add(venta);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE VENTAS SET fecha_hora = {0} WHERE id_venta = {1}", fechaUtc, venta.Id);
        return venta.Id;
    }

    [Fact]
    public async Task ObtenerFechaPrimeraVenta_SinVentas_DevuelveNull()
    {
        var primera = await _sut.ObtenerFechaPrimeraVentaAsync();

        primera.Should().BeNull();
    }

    [Fact]
    public async Task ObtenerFechaPrimeraVenta_ConDosVentas_DevuelveLaMasAntigua()
    {
        var a = await ArticuloAsync("Cuaderno", precio: 150m, costo: 100m);
        await VenderAsync(Utc(2026, 10, 14, 15), _idCajero1, [(a, 1)]);
        await VenderAsync(Utc(2026, 9, 2, 13), _idCajero1, [(a, 1)]);

        var primera = await _sut.ObtenerFechaPrimeraVentaAsync();

        primera.Should().Be(Utc(2026, 9, 2, 13));
    }

    [Fact]
    public async Task ObtenerFechaPrimeraVenta_ConVentaMasAntiguaDadaDeBaja_NoLaCuenta()
    {
        var a = await ArticuloAsync("Cuaderno", precio: 150m, costo: 100m);
        int idBorrada = await VenderAsync(Utc(2026, 8, 1, 13), _idCajero1, [(a, 1)]);
        await VenderAsync(Utc(2026, 10, 14, 15), _idCajero1, [(a, 1)]);
        await DarDeBajaVentaAsync(idBorrada);

        var primera = await _sut.ObtenerFechaPrimeraVentaAsync();

        primera.Should().Be(Utc(2026, 10, 14, 15));
    }

    // ---------- Desglose por medio de pago (D-34) ----------

    [Fact]
    public async Task ObtenerVentasPorPeriodo_ConPagoMixto_RepartePorMedioYLaSumaCoincideConElTotal()
    {
        // Arrange: una venta de 1000 pagada 600 en efectivo y 400 con débito, y otra de 500 con crédito
        var a = await ArticuloAsync("Agenda", precio: 1000m, costo: 600m);
        var b = await ArticuloAsync("Lapicera", precio: 500m, costo: 200m);
        await VenderConPagosAsync(Utc(2026, 10, 14, 15), _idCajero1, null, [(a, 1)], [(MedioPagoEnum.Efectivo, 600m), (MedioPagoEnum.TarjetaDebito, 400m)]);
        await VenderConPagosAsync(Utc(2026, 10, 14, 16), _idCajero1, null, [(b, 1)], [(MedioPagoEnum.TarjetaCredito, 500m)]);
        var rango = RangoLocal(new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 14));

        // Act
        var filas = await _sut.ObtenerVentasPorPeriodoAsync(rango, AgrupacionTemporalEnum.Dia, null, incluirMediosDePago: true);

        // Assert
        var dia = filas.Should().ContainSingle().Subject;
        dia.CantidadVentas.Should().Be(2, "los pagos no multiplican las ventas");
        dia.TotalVendido.Should().Be(1500m);
        dia.Efectivo.Should().Be(600m);
        dia.TarjetaDebito.Should().Be(400m);
        dia.TarjetaCredito.Should().Be(500m);
        dia.TransferenciaQr.Should().Be(0m);
        dia.CuentaCorriente.Should().Be(0m);
        (dia.Efectivo + dia.TarjetaDebito + dia.TarjetaCredito + dia.TransferenciaQr + dia.CuentaCorriente)
            .Should().Be(dia.TotalVendido);
    }

    [Fact]
    public async Task ObtenerVentasPorPeriodo_SinPedirMedios_DejaLosImportesPorMedioEnCeroYConservaElTotal()
    {
        // Arrange
        var a = await ArticuloAsync("Agenda", precio: 1000m, costo: 600m);
        await VenderConPagosAsync(Utc(2026, 10, 14, 15), _idCajero1, null, [(a, 1)], [(MedioPagoEnum.Efectivo, 600m), (MedioPagoEnum.TarjetaDebito, 400m)]);
        var rango = RangoLocal(new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 14));

        // Act
        var sin = await _sut.ObtenerVentasPorPeriodoAsync(rango, AgrupacionTemporalEnum.Dia, null, incluirMediosDePago: false);
        var con = await _sut.ObtenerVentasPorPeriodoAsync(rango, AgrupacionTemporalEnum.Dia, null, incluirMediosDePago: true);

        // Assert
        var fila = sin.Should().ContainSingle().Subject;
        fila.TotalVendido.Should().Be(1000m);
        fila.CantidadVentas.Should().Be(1);
        (fila.Efectivo + fila.TarjetaDebito + fila.TarjetaCredito + fila.TransferenciaQr + fila.CuentaCorriente).Should().Be(0m);
        con.Single().Efectivo.Should().Be(600m);
        con.Single().TarjetaDebito.Should().Be(400m);
    }

    [Fact]
    public async Task ObtenerVentasPorPeriodo_ConVentaDadaDeBaja_NoSumaSusPagos()
    {
        // Arrange
        var a = await ArticuloAsync("Agenda", precio: 1000m, costo: 600m);
        await VenderConPagosAsync(Utc(2026, 10, 14, 15), _idCajero1, null, [(a, 1)], [(MedioPagoEnum.Efectivo, 1000m)]);
        int idBorrada = await VenderConPagosAsync(Utc(2026, 10, 14, 16), _idCajero1, null, [(a, 3)], [(MedioPagoEnum.TransferenciaQr, 3000m)]);
        await DarDeBajaVentaAsync(idBorrada);
        var rango = RangoLocal(new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 14));

        // Act
        var filas = await _sut.ObtenerVentasPorPeriodoAsync(rango, AgrupacionTemporalEnum.Dia, null, incluirMediosDePago: true);

        // Assert
        var dia = filas.Should().ContainSingle().Subject;
        dia.TotalVendido.Should().Be(1000m);
        dia.Efectivo.Should().Be(1000m);
        dia.TransferenciaQr.Should().Be(0m);
    }

    [Fact]
    public async Task ObtenerVentasPorPeriodo_ConCuentaCorriente_VaEnSuPropiaColumna()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear(tieneCuentaCorriente: true, limiteCredito: 100_000m);
        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var a = await ArticuloAsync("Agenda", precio: 1000m, costo: 600m);
        await VenderConPagosAsync(Utc(2026, 10, 14, 15), _idCajero1, cliente.Id, [(a, 2)], [(MedioPagoEnum.CuentaCorriente, 1500m), (MedioPagoEnum.Efectivo, 500m)]);
        var rango = RangoLocal(new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 14));

        // Act
        var filas = await _sut.ObtenerVentasPorPeriodoAsync(rango, AgrupacionTemporalEnum.Dia, null, incluirMediosDePago: true);

        // Assert
        var dia = filas.Should().ContainSingle().Subject;
        dia.CuentaCorriente.Should().Be(1500m);
        dia.Efectivo.Should().Be(500m);
    }

    [Fact]
    public async Task ObtenerVentasPorPeriodo_ConFiltroDeCajero_DesglosaSoloSusPagos()
    {
        // Arrange
        var a = await ArticuloAsync("Agenda", precio: 1000m, costo: 600m);
        await VenderConPagosAsync(Utc(2026, 10, 14, 15), _idCajero1, null, [(a, 1)], [(MedioPagoEnum.Efectivo, 1000m)]);
        await VenderConPagosAsync(Utc(2026, 10, 14, 16), _idCajero2, null, [(a, 1)], [(MedioPagoEnum.TarjetaDebito, 1000m)]);
        var rango = RangoLocal(new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 14));

        // Act
        var filas = await _sut.ObtenerVentasPorPeriodoAsync(rango, AgrupacionTemporalEnum.Dia, _idCajero2, incluirMediosDePago: true);

        // Assert
        var dia = filas.Should().ContainSingle().Subject;
        dia.TotalVendido.Should().Be(1000m);
        dia.TarjetaDebito.Should().Be(1000m);
        dia.Efectivo.Should().Be(0m);
    }

    [Fact]
    public async Task ObtenerVentasPorPeriodo_AgrupadoPorSemanaYMes_ElDesgloseCoincideConLaClaveDeLasVentas()
    {
        // Arrange: lunes 05/10 y miércoles 07/10 (misma semana), y 14/09 (otra semana y otro mes)
        var a = await ArticuloAsync("Agenda", precio: 1000m, costo: 600m);
        await VenderConPagosAsync(Utc(2026, 10, 5, 15), _idCajero1, null, [(a, 1)], [(MedioPagoEnum.Efectivo, 1000m)]);
        await VenderConPagosAsync(Utc(2026, 10, 7, 15), _idCajero1, null, [(a, 1)], [(MedioPagoEnum.TarjetaDebito, 400m), (MedioPagoEnum.TransferenciaQr, 600m)]);
        await VenderConPagosAsync(Utc(2026, 9, 14, 15), _idCajero1, null, [(a, 1)], [(MedioPagoEnum.TarjetaCredito, 1000m)]);
        var rango = RangoLocal(new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 31));

        // Act
        var semanas = await _sut.ObtenerVentasPorPeriodoAsync(rango, AgrupacionTemporalEnum.Semana, null, incluirMediosDePago: true);
        var meses = await _sut.ObtenerVentasPorPeriodoAsync(rango, AgrupacionTemporalEnum.Mes, null, incluirMediosDePago: true);

        // Assert: cada período trae su desglose y Σ medios = total
        semanas.Select(f => f.InicioPeriodo).Should().Equal(new DateOnly(2026, 9, 14), new DateOnly(2026, 10, 5));
        semanas[0].TarjetaCredito.Should().Be(1000m);
        semanas[1].CantidadVentas.Should().Be(2);
        semanas[1].Efectivo.Should().Be(1000m);
        semanas[1].TarjetaDebito.Should().Be(400m);
        semanas[1].TransferenciaQr.Should().Be(600m);
        meses.Select(f => f.InicioPeriodo).Should().Equal(new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1));
        meses[1].TotalVendido.Should().Be(2000m);
        meses[1].Efectivo.Should().Be(1000m);
        foreach (var fila in semanas.Concat(meses))
        {
            (fila.Efectivo + fila.TarjetaDebito + fila.TarjetaCredito + fila.TransferenciaQr + fila.CuentaCorriente)
                .Should().Be(fila.TotalVendido);
        }
    }

    [Fact]
    public void PagosPorPeriodo_EsUnaSolaConsultaConGroupByYSumasCondicionales()
    {
        // Arrange
        var rango = RangoLocal(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        // Act
        foreach (var agrupacion in Enum.GetValues<AgrupacionTemporalEnum>())
        {
            string sql = _sut.PagosPorPeriodo(_sut.VentasDelRango(rango), agrupacion, DesfaseArgentina).ToQueryString();
            _salida.WriteLine($"--- PAGOS POR {agrupacion} ---");
            _salida.WriteLine(sql);

            // Assert
            // EF envuelve la clave calculada en una tabla derivada (igual que la consulta de ventas por semana): un solo GROUP BY.
            sql.Split("GROUP BY", StringSplitOptions.None).Length.Should().Be(2, "un único GROUP BY");
            sql.Should().Contain("GROUP BY").And.Contain("SUM(CASE").And.Contain("PAGOS_VENTA");
        }
    }

    private async Task<int> VenderConPagosAsync(
        DateTime fechaUtc,
        int idUsuario,
        int? idCliente,
        (Articulo Articulo, int Cantidad)[] items,
        (MedioPagoEnum Medio, decimal Monto)[] pagos)
    {
        var venta = Venta.Registrar(_idTurno, idUsuario, idCliente);
        foreach (var (articulo, cantidad) in items)
        {
            venta.AgregarItem(articulo.Id, cantidad, articulo.PrecioVenta, articulo.CostoReposicion);
        }

        foreach (var (medio, monto) in pagos)
        {
            venta.ImputarPago(medio, monto);
        }

        venta.ValidarCierre();
        _context.Ventas.Add(venta);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE VENTAS SET fecha_hora = {0} WHERE id_venta = {1}", fechaUtc, venta.Id);
        return venta.Id;
    }

    private async Task DarDeBajaArticuloAsync(int idArticulo)
    {
        var articulo = await _context.Articulos.SingleAsync(a => a.Id == idArticulo);
        articulo.MarkAsDeleted();
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    private async Task DarDeBajaVentaAsync(int idVenta)
    {
        var venta = await _context.Ventas.SingleAsync(v => v.Id == idVenta);
        venta.MarkAsDeleted();
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }
}

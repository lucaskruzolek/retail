using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Reportes;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
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
        var periodo = await _sut.ObtenerVentasPorPeriodoAsync(rango, AgrupacionTemporalEnum.Dia, null);
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
            RangoLocal(new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 15)), AgrupacionTemporalEnum.Dia, null);
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
        var semanas = await _sut.ObtenerVentasPorPeriodoAsync(rango, AgrupacionTemporalEnum.Semana, null);
        var meses = await _sut.ObtenerVentasPorPeriodoAsync(rango, AgrupacionTemporalEnum.Mes, null);

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
        var delCajero2 = await _sut.ObtenerVentasPorPeriodoAsync(rango, AgrupacionTemporalEnum.Dia, _idCajero2);
        var todos = await _sut.ObtenerVentasPorPeriodoAsync(rango, AgrupacionTemporalEnum.Dia, null);

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
        var periodos = await _sut.ObtenerVentasPorPeriodoAsync(rango, AgrupacionTemporalEnum.Semana, null);
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
        var categoria = new Categoria { NombreCategoria = nombre };
        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return categoria.Id;
    }

    private async Task<int> MarcaAsync(string nombre)
    {
        var marca = new Marca { NombreMarca = nombre };
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

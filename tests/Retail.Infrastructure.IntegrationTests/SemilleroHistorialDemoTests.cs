using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Reportes;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Initialization;
using Retail.Infrastructure.Persistence.Services;
using Retail.Infrastructure.Security;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Verifica contra SQL Server Express el historial de demostración. Las fechas se releen con un contexto nuevo: así se
/// comprueba lo que quedó en la base y no lo que el Change Tracker todavía tiene en memoria.
/// </summary>
public class SemilleroHistorialDemoTests : IAsyncLifetime, IDisposable
{
    private const int DesfaseArgentina = -180;
    private static readonly DateOnly Hoy = new(2026, 6, 15);
    private static readonly TimeZoneInfo ZonaArgentina = TimeZoneInfo.FindSystemTimeZoneById("Argentina Standard Time");

    private readonly string _dbName = $"RetailDb_Test_Historial_{Guid.NewGuid():N}";
    private DbContextOptions<RetailDbContext> _options = null!;
    private RetailDbContext _context = null!;

    public async Task InitializeAsync()
    {
        _options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(ServidorDePruebas.CadenaDeConexion(_dbName))
            .Options;

        _context = new RetailDbContext(_options);
        await _context.Database.CrearDesdePlantillaAsync();
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
    public async Task FijarFechaDeVenta_VentaDelDominio_GuardaLaFechaPasadaEnLaVentaSusItemsYSusPagos()
    {
        // Arrange
        var fecha = new DateTime(2026, 5, 12, 14, 30, 0, DateTimeKind.Utc);
        var turno = await AbrirTurnoAsync();
        var venta = await ArmarVentaAsync(turno);
        _context.Ventas.Add(venta);

        // Act
        SemilleroHistorialDemo.FijarFechaDeVenta(_context, venta, fecha);
        await _context.SaveChangesAsync();

        // Assert
        await using var lectura = new RetailDbContext(_options);
        var guardada = await lectura.Ventas
            .Include(v => v.Detalles)
            .Include(v => v.Pagos)
            .SingleAsync();

        guardada.FechaHora.Should().Be(fecha);
        guardada.CreatedAt.Should().Be(fecha);
        guardada.Detalles.Should().ContainSingle().Which.CreatedAt.Should().Be(fecha);
        guardada.Pagos.Should().ContainSingle().Which.CreatedAt.Should().Be(fecha);
        guardada.Total.Should().Be(300m); // los importes los siguió calculando el Dominio
    }

    [Fact]
    public async Task FijarFechasDeTurno_TurnoCerradoPorElDominio_GuardaLasFechasPasadasYConservaElArqueo()
    {
        // Arrange
        var apertura = new DateTime(2026, 5, 12, 12, 0, 0, DateTimeKind.Utc);
        var cierre = apertura.AddHours(4);
        var turno = await AbrirTurnoAsync();
        turno.CerrarConArqueoCiego(saldoDeclaradoEfectivo: 900m, montoRetenidoEnCaja: 500m);

        // Act
        SemilleroHistorialDemo.FijarFechasDeTurno(_context, turno, apertura, cierre);
        await _context.SaveChangesAsync();

        // Assert
        await using var lectura = new RetailDbContext(_options);
        var guardado = await lectura.TurnosCaja.SingleAsync();

        guardado.FechaApertura.Should().Be(apertura);
        guardado.FechaCierre.Should().Be(cierre);
        guardado.CreatedAt.Should().Be(apertura);
        guardado.Estado.Should().Be(EstadoTurnoEnum.Cerrado);
        guardado.DiferenciaEfectivo.Should().Be(-100m); // saldo inicial 1.000 contra 900 declarados
    }

    [Fact]
    public async Task CargarAsync_BaseConDiccionariosYGerente_GeneraUnHistorialCoherenteParaTodosLosReportes()
    {
        // Arrange: los diccionarios ya están cargados, que es el orden menos favorable para los nombres de las marcas.
        await SemilleroDiccionarios.CargarAsync(
            _context,
            LectorSemillaDiccionarios.Leer(Path.Combine(AppContext.BaseDirectory, "Semilla")));
        await CrearGerenteAsync();

        // Act
        var resultado = await SemilleroHistorialDemo.CargarAsync(_context, new PasswordHasher(), Hoy, ZonaArgentina);

        // Assert
        await using var lectura = new RetailDbContext(_options);
        var desde = Hoy.AddMonths(-SemilleroHistorialDemo.MesesDeHistorial);
        var inicioUtc = SemilleroHistorialDemo.AUtc(desde, TimeOnly.MinValue, ZonaArgentina);
        var hoyUtc = SemilleroHistorialDemo.AUtc(Hoy, TimeOnly.MinValue, ZonaArgentina);

        resultado.Hasta.Should().Be(Hoy.AddDays(-1));
        resultado.Ventas.Should().BeInRange(3000, 4800);
        (await lectura.Ventas.CountAsync()).Should().Be(resultado.Ventas);
        (await lectura.Ventas.MinAsync(v => v.FechaHora)).Should().BeOnOrAfter(inicioUtc);
        (await lectura.Ventas.MaxAsync(v => v.FechaHora)).Should().BeBefore(hoyUtc);

        // Las ventas no tienen descuento: el POS todavía no permite cargarlo.
        (await lectura.Ventas.AnyAsync(v => v.Descuento != 0m)).Should().BeFalse();
        (await lectura.Ventas.AnyAsync(v => v.Total != v.Pagos.Sum(p => p.Monto))).Should().BeFalse();

        await VerificarCajaAsync(lectura);
        await VerificarCuentasCorrientesAsync(lectura);
        await VerificarAbastecimientoAsync(lectura, resultado);
        await VerificarReportesAsync(lectura, desde, hoyUtc);

        (await lectura.Usuarios.Where(u => u.IdRol == (int)RolUsuarioEnum.Cajero).ToListAsync())
            .Should().HaveCount(CatalogoHistorialDemo.Cajeros.Count)
            .And.OnlyContain(u => u.DebeCambiarPassword);
        (await lectura.Marcas.Select(m => m.NombreMarca).ToListAsync())
            .GroupBy(Retail.Domain.Common.ReglasClasificacion.NormalizarTexto)
            .Should().OnlyContain(g => g.Count() == 1);
    }

    [Fact]
    public async Task CargarAsync_BaseConOperacionesDadasDeBaja_LaRechazaSinCrearNada()
    {
        // Arrange: hasta una venta dada de baja prueba que la base tuvo operaciones reales.
        await CrearGerenteAsync();
        var turno = await AbrirTurnoAsync();
        var venta = await ArmarVentaAsync(turno);
        _context.Ventas.Add(venta);
        venta.MarkAsDeleted();
        turno.MarkAsDeleted();
        await _context.SaveChangesAsync();

        // Act
        var cargar = () => SemilleroHistorialDemo.CargarAsync(_context, new PasswordHasher(), Hoy, ZonaArgentina);

        // Assert
        await cargar.Should().ThrowAsync<DomainException>().WithMessage("*sin operaciones*");
        (await _context.Proveedores.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CargarAsync_SinGerente_LoRechazaIndicandoElComandoQueFalta()
    {
        var cargar = () => SemilleroHistorialDemo.CargarAsync(_context, new PasswordHasher(), Hoy, ZonaArgentina);

        await cargar.Should().ThrowAsync<DomainException>().WithMessage("*inicializar*");
    }

    private static async Task VerificarCajaAsync(RetailDbContext lectura)
    {
        (await lectura.TurnosCaja.AnyAsync(t => t.Estado != EstadoTurnoEnum.Cerrado)).Should().BeFalse();
        (await lectura.TurnosCaja.AnyAsync(t =>
            t.SaldoTeoricoEfectivo != t.SaldoInicial + t.TotalVentasEfectivo + t.TotalIngresosEfectivo - t.TotalEgresosEfectivo))
            .Should().BeFalse();
        (await lectura.TurnosCaja.AnyAsync(t => t.DiferenciaEfectivo != 0m)).Should().BeTrue();
        (await lectura.MovimientosCaja.AnyAsync()).Should().BeTrue();

        var efectivoVendido = await lectura.PagosVenta
            .Where(p => p.MedioPago == MedioPagoEnum.Efectivo)
            .SumAsync(p => p.Monto);
        (await lectura.TurnosCaja.SumAsync(t => t.TotalVentasEfectivo)).Should().Be(efectivoVendido);
    }

    private static async Task VerificarCuentasCorrientesAsync(RetailDbContext lectura)
    {
        var fiado = await lectura.PagosVenta
            .Where(p => p.MedioPago == MedioPagoEnum.CuentaCorriente)
            .GroupBy(p => p.Venta!.IdCliente!.Value)
            .Select(g => new { IdCliente = g.Key, Total = g.Sum(p => p.Monto) })
            .ToDictionaryAsync(x => x.IdCliente, x => x.Total);
        var cobrado = await lectura.CobranzasClientes
            .GroupBy(c => c.IdCliente)
            .Select(g => new { IdCliente = g.Key, Total = g.Sum(c => c.Monto) })
            .ToDictionaryAsync(x => x.IdCliente, x => x.Total);
        var clientes = await lectura.Clientes.ToListAsync();

        clientes.Should().OnlyContain(c =>
            c.SaldoCuentaCorriente == fiado.GetValueOrDefault(c.Id) - cobrado.GetValueOrDefault(c.Id)
            && c.SaldoCuentaCorriente <= c.LimiteCredito);
        clientes.Should().Contain(c => c.TieneCuentaCorriente && c.SaldoCuentaCorriente >= c.LimiteCredito * 0.7m);
        cobrado.Should().NotBeEmpty();
    }

    private static async Task VerificarAbastecimientoAsync(RetailDbContext lectura, ResultadoHistorialDemo resultado)
    {
        (await lectura.Compras.CountAsync()).Should().Be(resultado.Compras).And.BeGreaterThan(0);
        (await lectura.Compras.AnyAsync(c => c.IdPedidoOrigen == null)).Should().BeFalse();
        (await lectura.Pedidos.Select(p => p.Estado).Distinct().ToListAsync()).Should().BeEquivalentTo(
            [EstadoPedidoEnum.Borrador, EstadoPedidoEnum.Emitido, EstadoPedidoEnum.Recibido, EstadoPedidoEnum.Cancelado]);
        (await lectura.Articulos.AnyAsync(a => a.IdArticuloOrigen != null)).Should().BeTrue();
        (await lectura.Articulos.AnyAsync(a => a.EsServicio)).Should().BeTrue();
        (await lectura.DetallesVenta.AnyAsync(d => d.CostoUnitario < d.Articulo!.CostoReposicion)).Should().BeTrue();
    }

    private static async Task VerificarReportesAsync(RetailDbContext lectura, DateOnly desde, DateTime hoyUtc)
    {
        var reportes = new ReporteQueryService(lectura);
        var rango = new RangoUtcDto(hoyUtc.AddMonths(-SemilleroHistorialDemo.MesesDeHistorial), hoyUtc, DesfaseArgentina);
        var hasta = Hoy.AddDays(-1);

        (await reportes.ObtenerResumenVentasAsync(rango)).CantidadVentas.Should().BeGreaterThan(0);

        var porMes = await reportes.ObtenerVentasPorPeriodoAsync(rango, AgrupacionTemporalEnum.Mes, null, incluirMediosDePago: true);
        porMes.Should().HaveCountGreaterThanOrEqualTo(SemilleroHistorialDemo.MesesDeHistorial);
        porMes.Sum(p => p.CuentaCorriente).Should().BeGreaterThan(0m);
        porMes.Sum(p => p.TransferenciaQr).Should().BeGreaterThan(0m);

        var ranking = new FiltroRankingDto { Desde = desde, Hasta = hasta, Cantidad = 10 };
        var masVendidos = await reportes.ObtenerMasVendidosAsync(rango, ranking);
        masVendidos[0].Unidades.Should().BeGreaterThan(masVendidos[^1].Unidades * 2);
        (await reportes.ObtenerMenosVendidosAsync(rango, ranking)).Should().Contain(f => f.Unidades == 0);

        var porCategoria = await reportes.ObtenerRentabilidadAsync(rango, new FiltroRentabilidadDto { Desde = desde, Hasta = hasta });
        porCategoria.Should().HaveCount(SemilleroDatosDemo.Categorias.Count());
        var porMarca = await reportes.ObtenerRentabilidadAsync(
            rango,
            new FiltroRentabilidadDto { Desde = desde, Hasta = hasta, AgruparPor = AgrupacionRentabilidadEnum.Marca });
        porMarca.Should().Contain(f => f.IdGrupo == null, "los artículos sin marca forman su propio grupo");

        (await reportes.ObtenerStockCriticoAsync(new FiltroInventarioDto())).Should().HaveCountGreaterThanOrEqualTo(8);
        (await reportes.ObtenerSinMovimientoAsync(new FiltroInventarioDto(), hoyUtc.AddDays(-90))).Should().HaveCountGreaterThanOrEqualTo(5);
    }

    private async Task CrearGerenteAsync()
    {
        _context.Usuarios.Add(Usuario.Crear("gerente", "Ana", "Paz", "hash", RolUsuarioEnum.Gerente, "gerente@example.com"));
        await _context.SaveChangesAsync();
    }

    private async Task<TurnoCaja> AbrirTurnoAsync()
    {
        var cajero = Usuario.Crear("cajero1", "Cajero", "Uno", "hash", RolUsuarioEnum.Cajero, null);
        _context.Usuarios.Add(cajero);
        await _context.SaveChangesAsync();

        var turno = TurnoCaja.Abrir(cajero.Id, 1000m);
        _context.TurnosCaja.Add(turno);
        await _context.SaveChangesAsync();
        return turno;
    }

    private async Task<Venta> ArmarVentaAsync(TurnoCaja turno)
    {
        var articulo = new Articulo
        {
            Descripcion = "Cuaderno",
            CostoReposicion = 100m,
            PorcentajeGanancia = 50m,
            PrecioVenta = 150m,
            StockActual = 10,
            StockMinimo = 1
        };
        _context.Articulos.Add(articulo);
        await _context.SaveChangesAsync();

        var venta = Venta.Registrar(turno.Id, turno.IdUsuario, null);
        venta.AgregarItem(articulo.Id, 2, articulo.PrecioVenta, articulo.CostoReposicion);
        venta.ImputarPago(MedioPagoEnum.Efectivo, venta.Total);
        venta.ValidarCierre();
        return venta;
    }
}

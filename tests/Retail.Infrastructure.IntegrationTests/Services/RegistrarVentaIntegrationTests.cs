using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Retail.Application.DTOs.Ventas;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Services;
using Retail.Application.Validators.Ventas;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Retail.Infrastructure.IntegrationTests.TestData;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Repositories;
using Retail.Infrastructure.Persistence.Services;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Services;

/// <summary>
/// Registro de ventas (RF-09, RF-10) con VentaService armado con piezas reales contra LocalDB.
/// Verifican lo que los tests unitarios no pueden: la persistencia en las tres tablas de la venta, la atomicidad
/// de la transacción y el control de concurrencia optimista con rowversion (D-12).
/// </summary>
public class RegistrarVentaIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_Ventas_{Guid.NewGuid():N}";
    private string _connectionString = null!;
    private RetailDbContext _context = null!;
    private VentaService _sut = null!;
    private int _idUsuario;
    private int _idTurno;

    public async Task InitializeAsync()
    {
        _connectionString = $"Server=(localdb)\\mssqllocaldb;Database={_dbName};Trusted_Connection=True;MultipleActiveResultSets=true";
        _context = CrearContexto();
        await _context.Database.MigrateAsync();
        _sut = CrearServicio(_context);

        var rol = new Rol { NombreRol = "Cajero", Descripcion = "Atiende el mostrador" };
        _context.Roles.Add(rol);
        await _context.SaveChangesAsync();

        // Base nueva: el primer rol recibe el Id 1, que coincide con RolUsuarioEnum.Cajero.
        var usuario = Usuario.Crear("cajero1", "Cajero", "Uno", "hash", RolUsuarioEnum.Cajero);
        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        var turno = TurnoCaja.Abrir(usuario.Id, saldoInicial: 5000m);
        _context.TurnosCaja.Add(turno);
        await _context.SaveChangesAsync();

        _idUsuario = usuario.Id;
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
    public async Task RegistrarVentaAsync_ConsumidorFinalConPagoMixto_PersisteVentaDetallesPagosStockYTurno()
    {
        // Arrange
        var cuaderno = await GuardarArticuloAsync("Cuaderno", precio: 1500m, stock: 8);
        var lapiz = await GuardarArticuloAsync("Lápiz", precio: 300m, stock: 20);

        var dto = CrearDto(
            [Item(cuaderno, 2), Item(lapiz, 5)],
            [Pago(MedioPagoEnum.Efectivo, 3000m), Pago(MedioPagoEnum.TarjetaDebito, 1500m)]);

        // Act
        var resultado = await _sut.RegistrarVentaAsync(dto);
        _context.ChangeTracker.Clear();

        // Assert: la venta tiene un Id real asignado por SQL Server y Consumidor Final se guarda como NULL (D-10)
        resultado.IdVenta.Should().BePositive();
        var venta = await _context.Ventas.AsNoTracking()
            .Include(v => v.Detalles)
            .Include(v => v.Pagos)
            .SingleAsync(v => v.Id == resultado.IdVenta);
        venta.IdCliente.Should().BeNull();
        venta.Total.Should().Be(4500m);
        venta.Detalles.Should().HaveCount(2);
        venta.Pagos.Should().HaveCount(2);

        // Assert: el stock y el turno se actualizaron en la misma transacción (RF-10)
        (await LeerStockAsync(cuaderno.Id)).Should().Be(6);
        (await LeerStockAsync(lapiz.Id)).Should().Be(15);
        var turno = await _context.TurnosCaja.AsNoTracking().SingleAsync(t => t.Id == _idTurno);
        turno.TotalVentasEfectivo.Should().Be(3000m);
        turno.TotalVentasElectronicas.Should().Be(1500m);
        turno.SaldoTeoricoEfectivo.Should().Be(8000m);
    }

    [Fact]
    public async Task RegistrarVentaAsync_PagoEnCuentaCorriente_PersisteLaDeudaDelClienteSinIngresarALaCaja()
    {
        // Arrange
        var cuaderno = await GuardarArticuloAsync("Cuaderno", precio: 1500m, stock: 8);
        var cliente = ClientesDePrueba.Crear(razonSocialONombre: "Librería del Centro", numeroDocumento: "30111222335", tipoDocumento: TipoDocumentoEnum.Cuit);
        cliente.HabilitarCuentaCorriente(10000m);
        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var dto = CrearDto([Item(cuaderno, 2)], [Pago(MedioPagoEnum.CuentaCorriente, 3000m)], cliente.Id);

        // Act
        await _sut.RegistrarVentaAsync(dto);
        _context.ChangeTracker.Clear();

        // Assert
        var clienteLeido = await _context.Clientes.AsNoTracking().SingleAsync(c => c.Id == cliente.Id);
        clienteLeido.SaldoCuentaCorriente.Should().Be(3000m);
        var turno = await _context.TurnosCaja.AsNoTracking().SingleAsync(t => t.Id == _idTurno);
        turno.TotalVentasEfectivo.Should().Be(0m);
        turno.TotalVentasElectronicas.Should().Be(0m);
        (await _context.Ventas.AsNoTracking().SingleAsync()).IdCliente.Should().Be(cliente.Id);
    }

    [Fact]
    public async Task RegistrarVentaAsync_VentaRechazadaPorStock_NoPersisteNadaYLaSiguienteVentaSoloGuardaLoSuyo()
    {
        // Arrange
        var cuaderno = await GuardarArticuloAsync("Cuaderno", precio: 1500m, stock: 8);
        var lapiz = await GuardarArticuloAsync("Lápiz", precio: 300m, stock: 2);

        var ventaRechazada = CrearDto(
            [Item(cuaderno, 1), Item(lapiz, 5)],
            [Pago(MedioPagoEnum.Efectivo, 3000m)]);

        // Act: la primera venta falla por el segundo artículo; la segunda se registra con el mismo contexto,
        // como ocurre en el POS, donde el DbContext vive mientras la pantalla está abierta.
        var act = () => _sut.RegistrarVentaAsync(ventaRechazada);
        await act.Should().ThrowAsync<StockInsuficienteException>();

        await _sut.RegistrarVentaAsync(CrearDto([Item(lapiz, 1)], [Pago(MedioPagoEnum.Efectivo, 300m)]));
        _context.ChangeTracker.Clear();

        // Assert: el cuaderno de la venta rechazada no quedó descontado ni se guardó con la venta siguiente
        (await LeerStockAsync(cuaderno.Id)).Should().Be(8);
        (await LeerStockAsync(lapiz.Id)).Should().Be(1);
        (await _context.Ventas.AsNoTracking().CountAsync()).Should().Be(1);
        var turno = await _context.TurnosCaja.AsNoTracking().SingleAsync(t => t.Id == _idTurno);
        turno.TotalVentasEfectivo.Should().Be(300m);
    }

    [Fact]
    public async Task RegistrarVentaAsync_OtraTerminalModificoElArticulo_LanzaConflictoRevierteTodoYElReintentoSeRegistra()
    {
        // Arrange: la terminal 2 tiene el cuaderno cargado en su contexto (como tras buscarlo en el POS)
        // antes de que la terminal 1 venda uno.
        var cuaderno = await GuardarArticuloAsync("Cuaderno", precio: 1500m, stock: 2);
        await using var contextoTerminal2 = CrearContexto();
        var ventaServiceTerminal2 = CrearServicio(contextoTerminal2);
        await contextoTerminal2.Articulos.SingleAsync(a => a.Id == cuaderno.Id);

        var dto = CrearDto([Item(cuaderno, 1)], [Pago(MedioPagoEnum.Efectivo, 1500m)]);
        await _sut.RegistrarVentaAsync(dto);

        // Act: la terminal 2 todavía ve stock 2, pero el rowversion de su copia ya no coincide con el de la base
        var ventaConDatosViejos = () => ventaServiceTerminal2.RegistrarVentaAsync(dto);

        // Assert: el UPDATE del artículo no afecta filas y SQL Server revierte toda la transacción
        // (sin esto, las dos terminales escribirían stock 1: lost update)
        await ventaConDatosViejos.Should().ThrowAsync<ConflictoDeConcurrenciaException>();
        _context.ChangeTracker.Clear();
        (await LeerStockAsync(cuaderno.Id)).Should().Be(1);
        (await _context.Ventas.AsNoTracking().CountAsync()).Should().Be(1, "la venta de la terminal 2 no se insertó");
        (await LeerEfectivoDelTurnoAsync()).Should().Be(1500m, "el turno no sumó la venta revertida");

        // Act: el cajero de la terminal 2 reintenta. DescartarCambios limpió su contexto, así que la venta relee
        // stock y rowversion actuales y no arrastra la venta ni los cambios del intento fallido.
        await ventaServiceTerminal2.RegistrarVentaAsync(dto);
        _context.ChangeTracker.Clear();

        // Assert
        (await LeerStockAsync(cuaderno.Id)).Should().Be(0);
        (await _context.Ventas.AsNoTracking().CountAsync()).Should().Be(2);
        (await LeerEfectivoDelTurnoAsync()).Should().Be(3000m);
    }

    private RetailDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(_connectionString)
            .Options;

        return new RetailDbContext(options);
    }

    private static VentaService CrearServicio(RetailDbContext context)
    {
        return new VentaService(
            new Repository<Articulo>(context),
            new ArticuloQueryService(context),
            new Repository<Venta>(context),
            new Repository<TurnoCaja>(context),
            new Repository<Cliente>(context),
            new UnitOfWork(context),
            new CrearVentaValidator(),
            Substitute.For<ITicketPrinterService>());
    }

    private async Task<Articulo> GuardarArticuloAsync(string descripcion, decimal precio, int stock)
    {
        var articulo = new Articulo
        {
            Descripcion = descripcion,
            CostoReposicion = precio / 2,
            PorcentajeGanancia = 100m,
            PrecioVenta = precio,
            StockActual = stock,
            StockMinimo = 1
        };
        _context.Articulos.Add(articulo);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return articulo;
    }

    private async Task<int> LeerStockAsync(int idArticulo)
    {
        return await _context.Articulos.AsNoTracking()
            .Where(a => a.Id == idArticulo)
            .Select(a => a.StockActual)
            .SingleAsync();
    }

    private async Task<decimal> LeerEfectivoDelTurnoAsync()
    {
        return await _context.TurnosCaja.AsNoTracking()
            .Where(t => t.Id == _idTurno)
            .Select(t => t.TotalVentasEfectivo)
            .SingleAsync();
    }

    private CrearVentaDto CrearDto(
        IReadOnlyList<DetalleVentaDto> items,
        IReadOnlyList<PagoVentaDto> pagos,
        int? idCliente = null)
    {
        return new CrearVentaDto
        {
            IdTurno = _idTurno,
            IdUsuario = _idUsuario,
            IdCliente = idCliente,
            Items = items,
            Pagos = pagos
        };
    }

    private static DetalleVentaDto Item(Articulo articulo, int cantidad)
    {
        return new DetalleVentaDto
        {
            IdArticulo = articulo.Id,
            Descripcion = articulo.Descripcion,
            Cantidad = cantidad,
            PrecioUnitario = articulo.PrecioVenta
        };
    }

    private static PagoVentaDto Pago(MedioPagoEnum medioPago, decimal monto)
    {
        return new PagoVentaDto { MedioPago = medioPago, Monto = monto };
    }
}

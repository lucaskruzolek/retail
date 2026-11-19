using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Retail.Application.DTOs.Presupuestos;
using Retail.Application.DTOs.Ventas;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Services;
using Retail.Application.Validators.Presupuestos;
using Retail.Application.Validators.Ventas;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Retail.Infrastructure.IntegrationTests.TestData;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Repositories;
using Retail.Infrastructure.Persistence.Services;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Persistencia del agregado <see cref="Presupuesto"/> (RF-11, RF-12) contra SQL Server Express: mapeo, conversión
/// atómica en la venta y las tres defensas contra la doble conversión (PS-13): estado del agregado, rowversion e
/// índice único filtrado en VENTAS.
/// </summary>
public class PresupuestoPersistenciaTests : PedidosPruebaBase
{
    private int _idTurno;
    private int _secuenciaArticulos;

    public PresupuestoPersistenciaTests()
        : base("Presupuesto")
    {
    }

    [Fact]
    public async Task Presupuesto_ConRenglones_SePersisteYSeRecuperaConFechaDescripcionYTitulo()
    {
        // Arrange
        var cliente = ClientesDePrueba.Crear(razonSocialONombre: "Instituto Rivadavia");
        Contexto.Clientes.Add(cliente);
        await Contexto.SaveChangesAsync();
        var cuaderno = await SembrarArticuloAsync("Cuaderno rayado", precio: 1500m);
        var lapiz = await SembrarArticuloAsync("Lápiz negro", precio: 300m);
        var hoy = new DateOnly(2026, 11, 17);
        var emitido = Presupuesto.Emitir(
            IdUsuario,
            cliente.Id,
            "Lista escolar 3.º grado",
            [
                new RenglonAPresupuestar(cuaderno.Id, cuaderno.Descripcion, 2, 1200m),
                new RenglonAPresupuestar(lapiz.Id, lapiz.Descripcion, 10, 250m)
            ],
            descuento: 100m,
            vigenciaDias: 15,
            hoy);
        Contexto.Presupuestos.Add(emitido);
        await Contexto.SaveChangesAsync();
        Contexto.ChangeTracker.Clear();

        // Act
        var leido = await Contexto.Presupuestos.Include(p => p.Detalles).SingleAsync(p => p.Id == emitido.Id);

        // Assert
        leido.IdCliente.Should().Be(cliente.Id);
        leido.Titulo.Should().Be("Lista escolar 3.º grado");
        leido.FechaVencimiento.Should().Be(new DateOnly(2026, 12, 2));
        leido.Estado.Should().Be(EstadoPresupuestoEnum.Pendiente);
        leido.FechaCierre.Should().BeNull();
        leido.Subtotal.Should().Be(4900m);
        leido.Descuento.Should().Be(100m);
        leido.Total.Should().Be(4800m);
        leido.Detalles.Should().HaveCount(2);
        var renglon = leido.Detalles.Single(d => d.IdArticulo == lapiz.Id);
        renglon.Descripcion.Should().Be("Lápiz negro");
        renglon.Cantidad.Should().Be(10);
        renglon.PrecioUnitarioPactado.Should().Be(250m);
    }

    [Fact]
    public async Task Presupuesto_ConsumidorFinalSinTitulo_SeGuardaConClienteYTituloNulos()
    {
        // Arrange
        var articulo = await SembrarArticuloAsync("Cuaderno", precio: 1500m);
        var emitido = await EmitirAsync(articulo, cantidad: 1, precioPactado: 1500m);
        Contexto.ChangeTracker.Clear();

        // Act
        var leido = await Contexto.Presupuestos.AsNoTracking().SingleAsync(p => p.Id == emitido.Id);

        // Assert
        leido.IdCliente.Should().BeNull();
        leido.Titulo.Should().BeNull();
    }

    [Fact]
    public async Task RegistrarVentaAsync_ConPresupuestoVigente_GuardaVentaYConversionEnUnSoloGuardado()
    {
        // Arrange
        await SembrarTurnoAsync();
        var articulo = await SembrarArticuloAsync("Cuaderno", precio: 1500m);
        var presupuesto = await EmitirAsync(articulo, cantidad: 2, precioPactado: 1200m);
        await using var contexto = NuevoContexto();
        var servicio = CrearVentaService(contexto);

        // Act
        var resultado = await servicio.RegistrarVentaAsync(CrearDto(articulo, cantidad: 2, precio: 1200m, presupuesto.Id));

        // Assert: la venta se cobró al precio pactado y el presupuesto quedó convertido, todo en la misma transacción
        resultado.Total.Should().Be(2400m);
        await using var lectura = NuevoContexto();
        var venta = await lectura.Ventas.AsNoTracking().SingleAsync(v => v.Id == resultado.IdVenta);
        venta.IdPresupuestoOrigen.Should().Be(presupuesto.Id);
        var convertido = await lectura.Presupuestos.AsNoTracking().SingleAsync(p => p.Id == presupuesto.Id);
        convertido.Estado.Should().Be(EstadoPresupuestoEnum.Convertido);
        convertido.FechaCierre.Should().NotBeNull();
    }

    [Fact]
    public async Task RegistrarVentaAsync_DosConversionesConcurrentesDelMismoPresupuesto_LaSegundaFallaYNoQuedaOtraVenta()
    {
        // Arrange: la terminal B ya tiene el presupuesto cargado (Pendiente) cuando la terminal A lo convierte
        await SembrarTurnoAsync();
        var articulo = await SembrarArticuloAsync("Cuaderno", precio: 1500m);
        var presupuesto = await EmitirAsync(articulo, cantidad: 2, precioPactado: 1200m);
        await using var contextoA = NuevoContexto();
        await using var contextoB = NuevoContexto();
        await contextoB.Presupuestos.Include(p => p.Detalles).SingleAsync(p => p.Id == presupuesto.Id);
        var dto = CrearDto(articulo, cantidad: 2, precio: 1200m, presupuesto.Id);
        await CrearVentaService(contextoA).RegistrarVentaAsync(dto);

        // Act: B todavía ve el presupuesto Pendiente, pero su rowversion ya no coincide con el de la base
        var act = () => CrearVentaService(contextoB).RegistrarVentaAsync(dto);

        // Assert: falla por el rowversion o por el índice único (el orden de las sentencias del lote no está definido)
        // y la transacción se revierte entera: queda una sola venta
        var excepcion = await act.Should().ThrowAsync<Exception>();
        excepcion.Which.Should().Match(e => e is ConflictoDeConcurrenciaException || e is RestriccionDeUnicidadException);
        await using var lectura = NuevoContexto();
        (await lectura.Ventas.AsNoTracking().CountAsync(v => v.IdPresupuestoOrigen == presupuesto.Id)).Should().Be(1);
        (await lectura.Articulos.AsNoTracking().SingleAsync(a => a.Id == articulo.Id)).StockActual.Should().Be(48);
    }

    [Fact]
    public async Task Presupuesto_AnuladoEnUnContextoYConvertidoEnOtro_LanzaConcurrencia()
    {
        // Arrange: dos terminales cargan el mismo presupuesto pendiente
        var articulo = await SembrarArticuloAsync("Cuaderno", precio: 1500m);
        var presupuesto = await EmitirAsync(articulo, cantidad: 1, precioPactado: 1500m);
        await using var contextoAnula = NuevoContexto();
        await using var contextoConvierte = NuevoContexto();
        var paraAnular = await contextoAnula.Presupuestos.SingleAsync(p => p.Id == presupuesto.Id);
        var paraConvertir = await contextoConvierte.Presupuestos.SingleAsync(p => p.Id == presupuesto.Id);

        paraAnular.Anular();
        await contextoAnula.SaveChangesAsync();
        paraConvertir.RegistrarConversion();

        // Act
        var act = () => contextoConvierte.SaveChangesAsync();

        // Assert: la terminal que convierte no pisa la anulación
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        await using var lectura = NuevoContexto();
        (await lectura.Presupuestos.AsNoTracking().SingleAsync(p => p.Id == presupuesto.Id)).Estado
            .Should().Be(EstadoPresupuestoEnum.Anulado);
    }

    [Fact]
    public async Task IndiceUnico_DosVentasConElMismoPresupuestoOrigen_LoRechazaLaBase()
    {
        // Arrange
        await SembrarTurnoAsync();
        var articulo = await SembrarArticuloAsync("Cuaderno", precio: 1500m);
        var presupuesto = await EmitirAsync(articulo, cantidad: 1, precioPactado: 1500m);
        Contexto.Ventas.Add(Venta.Registrar(_idTurno, IdUsuario, null, presupuesto.Id));
        await Contexto.SaveChangesAsync();
        Contexto.Ventas.Add(Venta.Registrar(_idTurno, IdUsuario, null, presupuesto.Id));

        // Act: se inserta por fuera del servicio, que es lo que la defensa en el motor debe atajar
        var act = () => Contexto.SaveChangesAsync();

        // Assert
        var excepcion = await act.Should().ThrowAsync<DbUpdateException>();
        excepcion.Which.InnerException.Should().BeOfType<SqlException>()
            .Which.Message.Should().Contain("IX_VENTAS_IdPresupuestoOrigen");
    }

    [Fact]
    public async Task IndiceUnico_VariasVentasSinPresupuestoOrigen_SePermiten()
    {
        // Arrange
        await SembrarTurnoAsync();
        Contexto.Ventas.Add(Venta.Registrar(_idTurno, IdUsuario, null));
        Contexto.Ventas.Add(Venta.Registrar(_idTurno, IdUsuario, null));

        // Act
        var act = () => Contexto.SaveChangesAsync();

        // Assert: el filtro [id_presupuesto_origen] IS NOT NULL deja afuera las ventas comunes
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReemplazarAsync_Pendiente_CierraElOriginalYPersisteElNuevoConLaReferencia()
    {
        // Arrange
        var articulo = await SembrarArticuloAsync("Cuaderno", precio: 1500m);
        var original = await EmitirAsync(articulo, cantidad: 5, precioPactado: 1500m);
        await using var contexto = NuevoContexto();

        // Act
        var nuevo = await CrearPresupuestoService(contexto).ReemplazarAsync(
            original.Id,
            CrearPresupuestoDto(articulo, cantidad: 3, precio: 1500m));

        // Assert: las dos escrituras salieron en el mismo guardado
        await using var lectura = NuevoContexto();
        var leidoOriginal = await lectura.Presupuestos.AsNoTracking().SingleAsync(p => p.Id == original.Id);
        leidoOriginal.Estado.Should().Be(EstadoPresupuestoEnum.Reemplazado);
        leidoOriginal.FechaCierre.Should().NotBeNull();
        var leidoNuevo = await lectura.Presupuestos.AsNoTracking().Include(p => p.Detalles).SingleAsync(p => p.Id == nuevo.IdPresupuesto);
        leidoNuevo.Estado.Should().Be(EstadoPresupuestoEnum.Pendiente);
        leidoNuevo.IdPresupuestoReemplazado.Should().Be(original.Id);
        leidoNuevo.Detalles.Should().ContainSingle().Which.Cantidad.Should().Be(3);
        nuevo.IdPresupuestoReemplazado.Should().Be(original.Id);
    }

    [Fact]
    public async Task ReemplazarAsync_SegundoReemplazoDelMismoOriginal_LoRechazaYNoQuedaOtroPresupuesto()
    {
        // Arrange
        var articulo = await SembrarArticuloAsync("Cuaderno", precio: 1500m);
        var original = await EmitirAsync(articulo, cantidad: 5, precioPactado: 1500m);
        await using var contextoA = NuevoContexto();
        await CrearPresupuestoService(contextoA).ReemplazarAsync(original.Id, CrearPresupuestoDto(articulo, cantidad: 3, precio: 1500m));
        await using var contextoB = NuevoContexto();

        // Act
        var act = () => CrearPresupuestoService(contextoB).ReemplazarAsync(original.Id, CrearPresupuestoDto(articulo, cantidad: 4, precio: 1500m));

        // Assert: el agregado ya está Reemplazado
        await act.Should().ThrowAsync<TransicionPresupuestoInvalidaException>();
        await using var lectura = NuevoContexto();
        (await lectura.Presupuestos.AsNoTracking().CountAsync(p => p.IdPresupuestoReemplazado == original.Id)).Should().Be(1);
    }

    [Fact]
    public async Task ReemplazarAsync_ReemplazoConcurrenteConElOriginalPendienteCargado_LaSegundaFalla()
    {
        // Arrange: la terminal B ya tiene el original (Pendiente) cuando A lo reemplaza
        var articulo = await SembrarArticuloAsync("Cuaderno", precio: 1500m);
        var original = await EmitirAsync(articulo, cantidad: 5, precioPactado: 1500m);
        await using var contextoA = NuevoContexto();
        await using var contextoB = NuevoContexto();
        await contextoB.Presupuestos.SingleAsync(p => p.Id == original.Id);
        await CrearPresupuestoService(contextoA).ReemplazarAsync(original.Id, CrearPresupuestoDto(articulo, cantidad: 3, precio: 1500m));

        // Act
        var act = () => CrearPresupuestoService(contextoB).ReemplazarAsync(original.Id, CrearPresupuestoDto(articulo, cantidad: 4, precio: 1500m));

        // Assert: rowversion o índice único (el orden de las sentencias no está definido); queda un solo reemplazo
        await act.Should().ThrowAsync<DomainException>().WithMessage("*cambió mientras lo corregía*");
        await using var lectura = NuevoContexto();
        (await lectura.Presupuestos.AsNoTracking().CountAsync(p => p.IdPresupuestoReemplazado == original.Id)).Should().Be(1);
    }

    [Fact]
    public async Task ReemplazarAsync_PresupuestoCobradoEnOtraTerminal_FallaYNoQuedaElNuevo()
    {
        // Arrange: B carga el presupuesto pendiente; A lo cobra (lo convierte en venta)
        await SembrarTurnoAsync();
        var articulo = await SembrarArticuloAsync("Cuaderno", precio: 1500m);
        var original = await EmitirAsync(articulo, cantidad: 2, precioPactado: 1500m);
        await using var contextoA = NuevoContexto();
        await using var contextoB = NuevoContexto();
        await contextoB.Presupuestos.SingleAsync(p => p.Id == original.Id);
        await CrearVentaService(contextoA).RegistrarVentaAsync(CrearDto(articulo, cantidad: 2, precio: 1500m, original.Id));

        // Act
        var act = () => CrearPresupuestoService(contextoB).ReemplazarAsync(original.Id, CrearPresupuestoDto(articulo, cantidad: 1, precio: 1500m));

        // Assert: el rowversion detecta la conversión y la transacción se revierte entera
        await act.Should().ThrowAsync<DomainException>().WithMessage("*cambió mientras lo corregía*");
        await using var lectura = NuevoContexto();
        (await lectura.Presupuestos.AsNoTracking().SingleAsync(p => p.Id == original.Id)).Estado
            .Should().Be(EstadoPresupuestoEnum.Convertido);
        (await lectura.Presupuestos.AsNoTracking().CountAsync(p => p.IdPresupuestoReemplazado == original.Id)).Should().Be(0);
    }

    [Fact]
    public async Task IndiceUnico_DosPresupuestosQueReemplazanAlMismo_LoRechazaLaBase()
    {
        // Arrange
        var articulo = await SembrarArticuloAsync("Cuaderno", precio: 1500m);
        var original = await EmitirAsync(articulo, cantidad: 1, precioPactado: 1500m);
        Contexto.Presupuestos.Add(EmitirReemplazo(articulo, original.Id));
        await Contexto.SaveChangesAsync();
        Contexto.Presupuestos.Add(EmitirReemplazo(articulo, original.Id));

        // Act: se inserta por fuera del servicio, que es lo que la defensa en el motor debe atajar
        var act = () => Contexto.SaveChangesAsync();

        // Assert
        var excepcion = await act.Should().ThrowAsync<DbUpdateException>();
        excepcion.Which.InnerException.Should().BeOfType<SqlException>()
            .Which.Message.Should().Contain("UX_PRESUPUESTOS_id_presupuesto_reemplazado");
    }

    [Fact]
    public async Task IndiceUnico_VariosPresupuestosSinReemplazado_SePermiten()
    {
        // Arrange
        var articulo = await SembrarArticuloAsync("Cuaderno", precio: 1500m);
        await EmitirAsync(articulo, cantidad: 1, precioPactado: 1500m);

        // Act
        var act = () => EmitirAsync(articulo, cantidad: 2, precioPactado: 1500m);

        // Assert: el filtro [id_presupuesto_reemplazado] IS NOT NULL deja afuera las emisiones comunes
        await act.Should().NotThrowAsync();
    }

    private async Task SembrarTurnoAsync()
    {
        var idTerminal = await TerminalesDePrueba.SembrarAsync(Contexto);
        var turno = TurnoCaja.Abrir(IdUsuario, idTerminal, saldoInicial: 5000m);
        Contexto.TurnosCaja.Add(turno);
        await Contexto.SaveChangesAsync();
        _idTurno = turno.Id;
    }

    private async Task<Articulo> SembrarArticuloAsync(string descripcion, decimal precio)
    {
        var articulo = new Articulo
        {
            Descripcion = descripcion,
            CodigoBarras = $"779100000{++_secuenciaArticulos:D4}",
            CostoReposicion = precio / 2,
            PorcentajeGanancia = 100m,
            PrecioVenta = precio,
            StockActual = 50,
            StockMinimo = 1
        };
        Contexto.Articulos.Add(articulo);
        await Contexto.SaveChangesAsync();
        return articulo;
    }

    private async Task<Presupuesto> EmitirAsync(Articulo articulo, int cantidad, decimal precioPactado)
    {
        var presupuesto = Presupuesto.Emitir(
            IdUsuario,
            idCliente: null,
            titulo: null,
            [new RenglonAPresupuestar(articulo.Id, articulo.Descripcion, cantidad, precioPactado)],
            descuento: 0m,
            vigenciaDias: 15,
            hoy: DateOnly.FromDateTime(DateTime.Now));
        Contexto.Presupuestos.Add(presupuesto);
        await Contexto.SaveChangesAsync();
        return presupuesto;
    }

    private CrearVentaDto CrearDto(Articulo articulo, int cantidad, decimal precio, int idPresupuesto)
    {
        return new CrearVentaDto
        {
            IdTurno = _idTurno,
            IdUsuario = IdUsuario,
            IdPresupuestoOrigen = idPresupuesto,
            Items =
            [
                new DetalleVentaDto
                {
                    IdArticulo = articulo.Id,
                    Descripcion = articulo.Descripcion,
                    Cantidad = cantidad,
                    PrecioUnitario = precio
                }
            ],
            Pagos = [new PagoVentaDto { MedioPago = MedioPagoEnum.Efectivo, Monto = cantidad * precio }]
        };
    }

    private static Presupuesto EmitirReemplazo(Articulo articulo, int idReemplazado)
    {
        return Presupuesto.Emitir(
            1,
            idCliente: null,
            titulo: null,
            [new RenglonAPresupuestar(articulo.Id, articulo.Descripcion, 1, 1500m)],
            descuento: 0m,
            vigenciaDias: 15,
            hoy: DateOnly.FromDateTime(DateTime.Now),
            idPresupuestoReemplazado: idReemplazado);
    }

    private CrearPresupuestoDto CrearPresupuestoDto(Articulo articulo, int cantidad, decimal precio)
    {
        return new CrearPresupuestoDto
        {
            IdUsuario = IdUsuario,
            Items =
            [
                new DetallePresupuestoDto
                {
                    IdArticulo = articulo.Id,
                    Descripcion = articulo.Descripcion,
                    Cantidad = cantidad,
                    PrecioUnitarioPactado = precio
                }
            ]
        };
    }

    private static PresupuestoService CrearPresupuestoService(RetailDbContext contexto)
    {
        return new PresupuestoService(
            new Repository<Presupuesto>(contexto),
            new Repository<Articulo>(contexto),
            new Repository<Cliente>(contexto),
            new PresupuestoQueryService(contexto),
            new ArticuloQueryService(contexto),
            new UnitOfWork(contexto),
            Substitute.For<ITicketPrinterService>(),
            Substitute.For<IExportadorPresupuestoPdf>(),
            TimeProvider.System,
            new CrearPresupuestoValidator(),
            new ConsultaPresupuestosValidator());
    }

    private static VentaService CrearVentaService(RetailDbContext contexto)
    {
        return new VentaService(
            new Repository<Articulo>(contexto),
            new ArticuloQueryService(contexto),
            new Repository<Venta>(contexto),
            new Repository<TurnoCaja>(contexto),
            new Repository<Cliente>(contexto),
            new UnitOfWork(contexto),
            new CrearVentaValidator(),
            Substitute.For<ITicketPrinterService>(),
            new Repository<Presupuesto>(contexto),
            new PresupuestoQueryService(contexto),
            TimeProvider.System);
    }
}

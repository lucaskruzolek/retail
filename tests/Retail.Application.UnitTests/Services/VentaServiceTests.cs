using System.Linq.Expressions;
using FluentAssertions;
using FluentValidation;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.Application.DTOs.Presupuestos;
using Retail.Application.DTOs.Ventas;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Application.UnitTests.TestData;
using Retail.Application.Validators.Ventas;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Application.UnitTests.Services;

public class VentaServiceTests
{
    private const int IdTurno = 1;
    private const int IdUsuario = 2;
    private const int IdCliente = 3;

    private readonly IRepository<Articulo> _articuloRepository;
    private readonly IArticuloQueryService _articuloQueryService;
    private readonly IRepository<Venta> _ventaRepository;
    private readonly IRepository<TurnoCaja> _turnoRepository;
    private readonly IRepository<Cliente> _clienteRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITicketPrinterService _ticketPrinterService;
    private readonly IRepository<Presupuesto> _presupuestoRepository;
    private readonly IPresupuestoQueryService _presupuestoQueryService;
    private readonly VentaService _sut;
    private readonly TurnoCaja _turno;

    public VentaServiceTests()
    {
        _articuloRepository = Substitute.For<IRepository<Articulo>>();
        _articuloQueryService = Substitute.For<IArticuloQueryService>();
        _ventaRepository = Substitute.For<IRepository<Venta>>();
        _turnoRepository = Substitute.For<IRepository<TurnoCaja>>();
        _clienteRepository = Substitute.For<IRepository<Cliente>>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _ticketPrinterService = Substitute.For<ITicketPrinterService>();
        _presupuestoRepository = Substitute.For<IRepository<Presupuesto>>();
        _presupuestoQueryService = Substitute.For<IPresupuestoQueryService>();

        _turno = TurnoCaja.Abrir(IdUsuario, idTerminal: 1, saldoInicial: 5000m);
        _turno.Id = IdTurno;
        _turnoRepository.GetByIdAsync(IdTurno, Arg.Any<CancellationToken>()).Returns(_turno);

        _sut = new VentaService(
            _articuloRepository,
            _articuloQueryService,
            _ventaRepository,
            _turnoRepository,
            _clienteRepository,
            _unitOfWork,
            new CrearVentaValidator(),
            _ticketPrinterService,
            _presupuestoRepository,
            _presupuestoQueryService,
            RelojDePrueba.EnQuinceDeOctubre());
    }

    [Fact]
    public async Task RegistrarVentaAsync_VentaValida_DescuentaStockImputaCajaYGuardaUnaSolaVez()
    {
        // Arrange
        var cuaderno = CrearArticulo(10, "Cuaderno", precio: 1500m, stock: 8);
        var lapiz = CrearArticulo(11, "Lápiz", precio: 300m, stock: 20);
        ConfigurarArticulos(cuaderno, lapiz);

        var dto = CrearDto(
            [Item(cuaderno, 2), Item(lapiz, 5)],
            [Pago(MedioPagoEnum.Efectivo, 3000m), Pago(MedioPagoEnum.TarjetaDebito, 1500m)]);

        // Act
        var resultado = await _sut.RegistrarVentaAsync(dto);

        // Assert
        cuaderno.StockActual.Should().Be(6);
        lapiz.StockActual.Should().Be(15);
        _turno.TotalVentasEfectivo.Should().Be(3000m);
        _turno.TotalVentasElectronicas.Should().Be(1500m);

        await _ventaRepository.Received(1).AddAsync(
            Arg.Is<Venta>(v => v.Total == 4500m && v.Detalles.Count == 2 && v.Pagos.Count == 2),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        resultado.Total.Should().Be(4500m);
        resultado.ClienteNombre.Should().Be("Consumidor Final");
        await _ticketPrinterService.Received(1).ImprimirTicketVentaAsync(resultado, Arg.Any<CancellationToken>());
        _unitOfWork.Received(1).DescartarCambios();
    }

    [Fact]
    public async Task RegistrarVentaAsync_ConListaDeOrigen_GuardaLaListaYCobraPrecioDeCatalogo()
    {
        // Arrange
        var cuaderno = CrearArticulo(10, "Cuaderno", precio: 1500m, stock: 8);
        ConfigurarArticulos(cuaderno);

        Venta? ventaPersistida = null;
        await _ventaRepository.AddAsync(Arg.Do<Venta>(v => ventaPersistida = v), Arg.Any<CancellationToken>());

        var dto = CrearDto([Item(cuaderno, 2)], [Pago(MedioPagoEnum.Efectivo, 3000m)]) with { IdListaOrigen = 4 };

        // Act
        var resultado = await _sut.RegistrarVentaAsync(dto);

        // Assert: la lista solo queda como origen; el precio es el de catálogo (L-08, L-09).
        ventaPersistida.Should().NotBeNull();
        ventaPersistida!.IdListaOrigen.Should().Be(4);
        ventaPersistida.IdPresupuestoOrigen.Should().BeNull();
        ventaPersistida.Total.Should().Be(3000m);
        resultado.IdListaOrigen.Should().Be(4);
    }

    [Fact]
    public async Task RegistrarVentaAsync_ArticuloConCosto_ConservaCostoDeReposicionEnElDetalle()
    {
        // Arrange
        var cuaderno = CrearArticulo(10, "Cuaderno", precio: 1500m, stock: 8);
        cuaderno.CostoReposicion = 700m;
        var lapiz = CrearArticulo(11, "Lápiz", precio: 300m, stock: 20);
        lapiz.CostoReposicion = 120m;
        ConfigurarArticulos(cuaderno, lapiz);

        Venta? ventaPersistida = null;
        await _ventaRepository.AddAsync(
            Arg.Do<Venta>(v => ventaPersistida = v),
            Arg.Any<CancellationToken>());

        var dto = CrearDto(
            [Item(cuaderno, 2), Item(lapiz, 5)],
            [Pago(MedioPagoEnum.Efectivo, 4500m)]);

        // Act
        await _sut.RegistrarVentaAsync(dto);

        // Assert
        ventaPersistida.Should().NotBeNull();
        ventaPersistida!.Detalles.Single(d => d.IdArticulo == 10).CostoUnitario.Should().Be(700m);
        ventaPersistida.Detalles.Single(d => d.IdArticulo == 11).CostoUnitario.Should().Be(120m);
    }

    [Fact]
    public async Task VerificarTicketAsync_PreciosYStockVigentes_NoInformaNada()
    {
        // Arrange
        ConfigurarDisponibilidad(Disponibilidad(10, "Cuaderno", precio: 1500m, stock: 8));

        // Act
        var resultado = await _sut.VerificarTicketAsync([ItemDto(10, "Cuaderno", cantidad: 8, precio: 1500m)]);

        // Assert
        resultado.PreciosActualizados.Should().BeEmpty();
        resultado.Faltantes.Should().BeEmpty();
        resultado.ArticulosNoDisponibles.Should().BeEmpty();
    }

    [Fact]
    public async Task VerificarTicketAsync_PrecioCambiadoEnElCatalogo_InformaPrecioAnteriorYActual()
    {
        // Arrange
        ConfigurarDisponibilidad(Disponibilidad(10, "Cuaderno", precio: 1800m, stock: 8));

        // Act
        var resultado = await _sut.VerificarTicketAsync([ItemDto(10, "Cuaderno", cantidad: 1, precio: 1500m)]);

        // Assert
        var cambio = resultado.PreciosActualizados.Should().ContainSingle().Subject;
        cambio.PrecioAnterior.Should().Be(1500m);
        cambio.PrecioActual.Should().Be(1800m);
    }

    [Fact]
    public async Task VerificarTicketAsync_FaltaStockDeUnArticuloDeCompra_InformaFaltanteSinFraccionar()
    {
        // Arrange
        ConfigurarDisponibilidad(Disponibilidad(10, "Cuaderno", precio: 1500m, stock: 2));

        // Act
        var resultado = await _sut.VerificarTicketAsync([ItemDto(10, "Cuaderno", cantidad: 5, precio: 1500m)]);

        // Assert
        var faltante = resultado.Faltantes.Should().ContainSingle().Subject;
        faltante.Faltante.Should().Be(3);
        faltante.OrigenesAFraccionar.Should().Be(0);
        faltante.PuedeFraccionar.Should().BeFalse();
    }

    [Fact]
    public async Task VerificarTicketAsync_FaltanSueltosYHayPacks_InformaElMinimoDePacksAFraccionar()
    {
        // Arrange: faltan 3 sobres sueltos; el pack trae 100 y hay 4 packs
        ConfigurarDisponibilidad(Disponibilidad(20, "Sobre (unidad)", precio: 50m, stock: 2) with
        {
            IdArticuloOrigen = 21,
            DescripcionOrigen = "Sobre (pack x100)",
            UnidadesPorOrigen = 100,
            StockOrigen = 4
        });

        // Act
        var resultado = await _sut.VerificarTicketAsync([ItemDto(20, "Sobre (unidad)", cantidad: 5, precio: 50m)]);

        // Assert
        var faltante = resultado.Faltantes.Should().ContainSingle().Subject;
        faltante.OrigenesAFraccionar.Should().Be(1);
        faltante.PuedeFraccionar.Should().BeTrue();
        faltante.DescripcionOrigen.Should().Be("Sobre (pack x100)");
    }

    [Fact]
    public async Task VerificarTicketAsync_FaltanSueltosYNoAlcanzanLosPacks_InformaQueNoSePuedeFraccionar()
    {
        // Arrange: faltan 250 sueltos (3 packs) y hay 2 packs
        ConfigurarDisponibilidad(Disponibilidad(20, "Sobre (unidad)", precio: 50m, stock: 0) with
        {
            IdArticuloOrigen = 21,
            DescripcionOrigen = "Sobre (pack x100)",
            UnidadesPorOrigen = 100,
            StockOrigen = 2
        });

        // Act
        var resultado = await _sut.VerificarTicketAsync([ItemDto(20, "Sobre (unidad)", cantidad: 250, precio: 50m)]);

        // Assert
        var faltante = resultado.Faltantes.Should().ContainSingle().Subject;
        faltante.OrigenesAFraccionar.Should().Be(3);
        faltante.PuedeFraccionar.Should().BeFalse();
    }

    [Fact]
    public async Task VerificarTicketAsync_PresentacionConOrigenDadoDeBaja_NoOfreceFraccionar()
    {
        // Arrange: la proyección devuelve el origen nulo cuando está dado de baja
        ConfigurarDisponibilidad(Disponibilidad(20, "Sobre (unidad)", precio: 50m, stock: 0) with
        {
            IdArticuloOrigen = 21,
            UnidadesPorOrigen = 100
        });

        // Act
        var resultado = await _sut.VerificarTicketAsync([ItemDto(20, "Sobre (unidad)", cantidad: 1, precio: 50m)]);

        // Assert
        resultado.Faltantes.Single().PuedeFraccionar.Should().BeFalse();
    }

    [Fact]
    public async Task VerificarTicketAsync_ServicioSinStock_NoEsFaltante()
    {
        // Arrange
        ConfigurarDisponibilidad(Disponibilidad(12, "Fotocopia", precio: 50m, stock: 0) with { EsServicio = true });

        // Act
        var resultado = await _sut.VerificarTicketAsync([ItemDto(12, "Fotocopia", cantidad: 10, precio: 50m)]);

        // Assert
        resultado.Faltantes.Should().BeEmpty();
    }

    [Fact]
    public async Task VerificarTicketAsync_ArticuloDadoDeBaja_LoInformaComoNoDisponible()
    {
        // Arrange
        ConfigurarDisponibilidad();

        // Act
        var resultado = await _sut.VerificarTicketAsync([ItemDto(10, "Cuaderno", cantidad: 1, precio: 1500m)]);

        // Assert
        resultado.ArticulosNoDisponibles.Should().ContainSingle().Which.Should().Be("Cuaderno");
    }

    [Fact]
    public async Task VerificarTicketAsync_ArticuloRepetido_VerificaLaCantidadTotal()
    {
        // Arrange
        ConfigurarDisponibilidad(Disponibilidad(10, "Cuaderno", precio: 1500m, stock: 5));

        // Act
        var resultado = await _sut.VerificarTicketAsync(
            [ItemDto(10, "Cuaderno", cantidad: 3, precio: 1500m), ItemDto(10, "Cuaderno", cantidad: 3, precio: 1500m)]);

        // Assert
        resultado.Faltantes.Should().ContainSingle().Which.CantidadSolicitada.Should().Be(6);
    }

    [Fact]
    public async Task BuscarPorTextoAsync_TerminoConEspacios_DelegaEnLaConsultaSinTracking()
    {
        // Act
        await _sut.BuscarPorTextoAsync("  cuaderno ", 10);

        // Assert
        await _articuloQueryService.Received(1).BuscarParaVentaAsync("cuaderno", 10, Arg.Any<CancellationToken>());
        await _articuloRepository.DidNotReceiveWithAnyArgs().FindAsync(default!, default, default);
    }

    [Fact]
    public async Task BuscarPorTextoAsync_TerminoVacio_NoConsultaLaBase()
    {
        // Act
        var resultado = await _sut.BuscarPorTextoAsync("   ");

        // Assert
        resultado.Should().BeEmpty();
        await _articuloQueryService.DidNotReceiveWithAnyArgs().BuscarParaVentaAsync(default!, default, default);
    }

    [Fact]
    public async Task RegistrarVentaAsync_TurnoInexistente_LanzaCajaCerradaExceptionSinGuardar()
    {
        // Arrange
        _turnoRepository.GetByIdAsync(IdTurno, Arg.Any<CancellationToken>()).Returns((TurnoCaja?)null);
        var cuaderno = CrearArticulo(10, "Cuaderno", precio: 1500m, stock: 8);
        ConfigurarArticulos(cuaderno);

        // Act
        var act = () => _sut.RegistrarVentaAsync(CrearDto([Item(cuaderno, 1)], [Pago(MedioPagoEnum.Efectivo, 1500m)]));

        // Assert
        await act.Should().ThrowAsync<CajaCerradaException>();
        await AssertNoSeGuardoNadaAsync();
    }

    [Fact]
    public async Task RegistrarVentaAsync_TurnoDeOtroCajero_LanzaTurnoAjenoSinGuardar()
    {
        // Arrange: el turno es de IdUsuario; otro cajero intenta venderle (MT-14).
        var cuaderno = CrearArticulo(10, "Cuaderno", precio: 1500m, stock: 8);
        ConfigurarArticulos(cuaderno);
        var dto = CrearDto([Item(cuaderno, 1)], [Pago(MedioPagoEnum.Efectivo, 1500m)]) with { IdUsuario = IdUsuario + 1 };

        // Act
        var act = () => _sut.RegistrarVentaAsync(dto);

        // Assert
        await act.Should().ThrowAsync<TurnoAjenoException>().WithMessage("*Solo esa persona puede operarlo*");
        cuaderno.StockActual.Should().Be(8);
        await AssertNoSeGuardoNadaAsync();
    }

    [Fact]
    public async Task RegistrarVentaAsync_TurnoCerrado_LanzaCajaCerradaExceptionSinGuardar()
    {
        // Arrange
        _turno.CerrarConArqueoCiego(saldoDeclaradoEfectivo: 5000m, montoRetenidoEnCaja: 0m);
        var cuaderno = CrearArticulo(10, "Cuaderno", precio: 1500m, stock: 8);
        ConfigurarArticulos(cuaderno);

        // Act
        var act = () => _sut.RegistrarVentaAsync(CrearDto([Item(cuaderno, 1)], [Pago(MedioPagoEnum.Efectivo, 1500m)]));

        // Assert
        await act.Should().ThrowAsync<CajaCerradaException>();
        cuaderno.StockActual.Should().Be(8);
        await AssertNoSeGuardoNadaAsync();
    }

    [Fact]
    public async Task RegistrarVentaAsync_StockInsuficienteEnElSegundoArticulo_NoModificaNingunAgregadoNiGuarda()
    {
        // Arrange
        var cuaderno = CrearArticulo(10, "Cuaderno", precio: 1500m, stock: 8);
        var lapiz = CrearArticulo(11, "Lápiz", precio: 300m, stock: 2);
        ConfigurarArticulos(cuaderno, lapiz);

        var dto = CrearDto(
            [Item(cuaderno, 1), Item(lapiz, 5)],
            [Pago(MedioPagoEnum.Efectivo, 3000m)]);

        // Act
        var act = () => _sut.RegistrarVentaAsync(dto);

        // Assert
        await act.Should().ThrowAsync<StockInsuficienteException>();
        cuaderno.StockActual.Should().Be(8, "el stock del primer artículo no puede quedar descontado en el ChangeTracker");
        lapiz.StockActual.Should().Be(2);
        _turno.TotalVentasEfectivo.Should().Be(0m);
        await AssertNoSeGuardoNadaAsync();
    }

    [Fact]
    public async Task RegistrarVentaAsync_ArticuloRepetidoEnElTicket_VerificaElStockContraLaCantidadTotal()
    {
        // Arrange
        var lapiz = CrearArticulo(11, "Lápiz", precio: 300m, stock: 5);
        ConfigurarArticulos(lapiz);

        var dto = CrearDto(
            [Item(lapiz, 3), Item(lapiz, 3)],
            [Pago(MedioPagoEnum.Efectivo, 1800m)]);

        // Act
        var act = () => _sut.RegistrarVentaAsync(dto);

        // Assert
        await act.Should().ThrowAsync<StockInsuficienteException>()
            .Where(e => e.CantidadSolicitada == 6);
        lapiz.StockActual.Should().Be(5);
        await AssertNoSeGuardoNadaAsync();
    }

    [Fact]
    public async Task RegistrarVentaAsync_Servicio_NoDescuentaStock()
    {
        // Arrange
        var fotocopia = CrearArticulo(12, "Fotocopia", precio: 50m, stock: 0);
        fotocopia.EsServicio = true;
        ConfigurarArticulos(fotocopia);

        // Act
        await _sut.RegistrarVentaAsync(CrearDto([Item(fotocopia, 10)], [Pago(MedioPagoEnum.Efectivo, 500m)]));

        // Assert
        fotocopia.StockActual.Should().Be(0);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarVentaAsync_PrecioDistintoAlDelCatalogo_LanzaDomainExceptionSinGuardar()
    {
        // Arrange
        var cuaderno = CrearArticulo(10, "Cuaderno", precio: 1800m, stock: 8);
        ConfigurarArticulos(cuaderno);

        var itemConPrecioViejo = Item(cuaderno, 1) with { PrecioUnitario = 1500m };
        var dto = CrearDto([itemConPrecioViejo], [Pago(MedioPagoEnum.Efectivo, 1500m)]);

        // Act
        var act = () => _sut.RegistrarVentaAsync(dto);

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*precio*cambió*");
        cuaderno.StockActual.Should().Be(8);
        await AssertNoSeGuardoNadaAsync();
    }

    [Fact]
    public async Task RegistrarVentaAsync_ArticuloDadoDeBaja_LanzaDomainExceptionSinGuardar()
    {
        // Arrange: el repositorio no devuelve artículos borrados lógicamente.
        var cuaderno = CrearArticulo(10, "Cuaderno", precio: 1500m, stock: 8);
        ConfigurarArticulos();

        // Act
        var act = () => _sut.RegistrarVentaAsync(CrearDto([Item(cuaderno, 1)], [Pago(MedioPagoEnum.Efectivo, 1500m)]));

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*Cuaderno*no está disponible*");
        await AssertNoSeGuardoNadaAsync();
    }

    [Fact]
    public async Task RegistrarVentaAsync_PagosInsuficientes_LanzaMontoPagoInsuficienteExceptionSinGuardar()
    {
        // Arrange
        var cuaderno = CrearArticulo(10, "Cuaderno", precio: 1500m, stock: 8);
        ConfigurarArticulos(cuaderno);

        // Act
        var act = () => _sut.RegistrarVentaAsync(CrearDto([Item(cuaderno, 1)], [Pago(MedioPagoEnum.Efectivo, 1000m)]));

        // Assert
        await act.Should().ThrowAsync<MontoPagoInsuficienteException>();
        cuaderno.StockActual.Should().Be(8);
        await AssertNoSeGuardoNadaAsync();
    }

    [Fact]
    public async Task RegistrarVentaAsync_PagoMixtoConCuentaCorriente_DebitaAlClienteYNoLoImputaEnCaja()
    {
        // Arrange
        var cuaderno = CrearArticulo(10, "Cuaderno", precio: 1500m, stock: 8);
        ConfigurarArticulos(cuaderno);
        var cliente = ConfigurarClienteConCuentaCorriente(limiteCredito: 10000m);

        var dto = CrearDto(
            [Item(cuaderno, 2)],
            [Pago(MedioPagoEnum.Efectivo, 1000m), Pago(MedioPagoEnum.CuentaCorriente, 2000m)],
            idCliente: IdCliente);

        // Act
        var resultado = await _sut.RegistrarVentaAsync(dto);

        // Assert
        cliente.SaldoCuentaCorriente.Should().Be(2000m);
        _turno.TotalVentasEfectivo.Should().Be(1000m);
        _turno.TotalVentasElectronicas.Should().Be(0m, "la cuenta corriente es deuda: no ingresa a la caja");
        resultado.ClienteNombre.Should().Be("Librería del Centro");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarVentaAsync_CuentaCorrienteExcedeElLimite_LanzaLimiteCreditoExcedidoSinDescontarStock()
    {
        // Arrange
        var cuaderno = CrearArticulo(10, "Cuaderno", precio: 1500m, stock: 8);
        ConfigurarArticulos(cuaderno);
        var cliente = ConfigurarClienteConCuentaCorriente(limiteCredito: 1000m);

        var dto = CrearDto(
            [Item(cuaderno, 2)],
            [Pago(MedioPagoEnum.CuentaCorriente, 3000m)],
            idCliente: IdCliente);

        // Act
        var act = () => _sut.RegistrarVentaAsync(dto);

        // Assert
        await act.Should().ThrowAsync<LimiteCreditoExcedidoException>();
        cliente.SaldoCuentaCorriente.Should().Be(0m);
        cuaderno.StockActual.Should().Be(8);
        _turno.TotalVentasEfectivo.Should().Be(0m);
        await AssertNoSeGuardoNadaAsync();
    }

    [Fact]
    public async Task RegistrarVentaAsync_ClienteInexistente_LanzaDomainExceptionSinGuardar()
    {
        // Arrange
        var cuaderno = CrearArticulo(10, "Cuaderno", precio: 1500m, stock: 8);
        ConfigurarArticulos(cuaderno);
        _clienteRepository.GetByIdAsync(IdCliente, false, Arg.Any<CancellationToken>()).Returns((Cliente?)null);

        var dto = CrearDto([Item(cuaderno, 1)], [Pago(MedioPagoEnum.Efectivo, 1500m)], idCliente: IdCliente);

        // Act
        var act = () => _sut.RegistrarVentaAsync(dto);

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*cliente*");
        await AssertNoSeGuardoNadaAsync();
    }

    [Fact]
    public async Task RegistrarVentaAsync_FallaLaImpresora_RetornaLaVentaConfirmada()
    {
        // Arrange
        var cuaderno = CrearArticulo(10, "Cuaderno", precio: 1500m, stock: 8);
        ConfigurarArticulos(cuaderno);
        _ticketPrinterService
            .ImprimirTicketVentaAsync(Arg.Any<VentaResponseDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("Impresora sin papel"));

        // Act
        var resultado = await _sut.RegistrarVentaAsync(CrearDto([Item(cuaderno, 1)], [Pago(MedioPagoEnum.Efectivo, 1500m)]));

        // Assert
        resultado.Total.Should().Be(1500m);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarVentaAsync_ConflictoDeConcurrenciaAlGuardar_PropagaLaExcepcionSinImprimir()
    {
        // Arrange
        var cuaderno = CrearArticulo(10, "Cuaderno", precio: 1500m, stock: 8);
        ConfigurarArticulos(cuaderno);
        _unitOfWork
            .SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConflictoDeConcurrenciaException());

        // Act
        var act = () => _sut.RegistrarVentaAsync(CrearDto([Item(cuaderno, 1)], [Pago(MedioPagoEnum.Efectivo, 1500m)]));

        // Assert: el descarte de los cambios lo garantiza la Unit of Work (UnitOfWorkIntegrationTests)
        await act.Should().ThrowAsync<ConflictoDeConcurrenciaException>();
        await _ticketPrinterService.DidNotReceiveWithAnyArgs().ImprimirTicketVentaAsync(default!, default);
    }

    [Fact]
    public async Task RegistrarVentaAsync_DtoSinItems_LanzaValidationExceptionSinConsultarLaBase()
    {
        // Act
        var act = () => _sut.RegistrarVentaAsync(CrearDto([], [Pago(MedioPagoEnum.Efectivo, 100m)]));

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        await _turnoRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
        await AssertNoSeGuardoNadaAsync();
    }

    // ── Presupuesto de origen (RF-12, PS-12, PS-14) ──

    private static readonly DateOnly Hoy = new(2026, 10, 15);

    [Fact]
    public async Task RegistrarVentaAsync_PresupuestoVigente_CobraElPrecioPactadoYLoConvierteEnUnSoloGuardado()
    {
        // Arrange: el catálogo subió a 1800 después de pactar 1500.
        var cuaderno = CrearArticulo(10, "Cuaderno", precio: 1800m, stock: 8);
        ConfigurarArticulos(cuaderno);
        var presupuesto = ConfigurarPresupuesto(Hoy, (10, 3, 1500m));

        Venta? ventaPersistida = null;
        await _ventaRepository.AddAsync(Arg.Do<Venta>(v => ventaPersistida = v), Arg.Any<CancellationToken>());

        var dto = CrearDto(
            [Item(cuaderno, 2) with { PrecioUnitario = 1500m }],
            [Pago(MedioPagoEnum.Efectivo, 3000m)],
            idPresupuestoOrigen: presupuesto.Id);

        // Act
        var resultado = await _sut.RegistrarVentaAsync(dto);

        // Assert
        resultado.Total.Should().Be(3000m);
        resultado.IdPresupuestoOrigen.Should().Be(presupuesto.Id);
        ventaPersistida!.IdPresupuestoOrigen.Should().Be(presupuesto.Id);
        presupuesto.Estado.Should().Be(EstadoPresupuestoEnum.Convertido);
        await _presupuestoRepository.Received(1).UpdateAsync(presupuesto, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarVentaAsync_PresupuestoVencido_ExigeElPrecioDeCatalogo()
    {
        // Arrange: venció hace un día; el cajero todavía manda el precio pactado.
        var cuaderno = CrearArticulo(10, "Cuaderno", precio: 1800m, stock: 8);
        ConfigurarArticulos(cuaderno);
        var presupuesto = ConfigurarPresupuesto(Hoy.AddDays(-16), (10, 3, 1500m));

        var conPrecioPactado = CrearDto(
            [Item(cuaderno, 2) with { PrecioUnitario = 1500m }],
            [Pago(MedioPagoEnum.Efectivo, 3000m)],
            idPresupuestoOrigen: presupuesto.Id);

        // Act
        var act = () => _sut.RegistrarVentaAsync(conPrecioPactado);

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*precio*cambió*");
        presupuesto.Estado.Should().Be(EstadoPresupuestoEnum.Pendiente);
        await AssertNoSeGuardoNadaAsync();

        // Con el precio de catálogo, el vencido se cobra y se convierte.
        var resultado = await _sut.RegistrarVentaAsync(CrearDto(
            [Item(cuaderno, 2)],
            [Pago(MedioPagoEnum.Efectivo, 3600m)],
            idPresupuestoOrigen: presupuesto.Id));

        resultado.Total.Should().Be(3600m);
        presupuesto.Estado.Should().Be(EstadoPresupuestoEnum.Convertido);
    }

    [Fact]
    public async Task RegistrarVentaAsync_PresupuestoVigenteConCantidadMayorALaPresupuestada_LanzaDomainExceptionSinGuardar()
    {
        // Arrange
        var cuaderno = CrearArticulo(10, "Cuaderno", precio: 1800m, stock: 8);
        ConfigurarArticulos(cuaderno);
        var presupuesto = ConfigurarPresupuesto(Hoy, (10, 2, 1500m));

        var dto = CrearDto(
            [Item(cuaderno, 3) with { PrecioUnitario = 1500m }],
            [Pago(MedioPagoEnum.Efectivo, 4500m)],
            idPresupuestoOrigen: presupuesto.Id);

        // Act
        var act = () => _sut.RegistrarVentaAsync(dto);

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*2 unidades*3*");
        presupuesto.Estado.Should().Be(EstadoPresupuestoEnum.Pendiente);
        cuaderno.StockActual.Should().Be(8);
        await AssertNoSeGuardoNadaAsync();
    }

    [Fact]
    public async Task RegistrarVentaAsync_PresupuestoVigenteConArticuloRepetido_ComparaLaCantidadTotal()
    {
        // Arrange: dos líneas de 2 suman 4, y se presupuestaron 3.
        var cuaderno = CrearArticulo(10, "Cuaderno", precio: 1800m, stock: 8);
        ConfigurarArticulos(cuaderno);
        var presupuesto = ConfigurarPresupuesto(Hoy, (10, 3, 1500m));
        var linea = Item(cuaderno, 2) with { PrecioUnitario = 1500m };

        // Act
        var act = () => _sut.RegistrarVentaAsync(CrearDto(
            [linea, linea],
            [Pago(MedioPagoEnum.Efectivo, 6000m)],
            idPresupuestoOrigen: presupuesto.Id));

        // Assert
        await act.Should().ThrowAsync<DomainException>();
        await AssertNoSeGuardoNadaAsync();
    }

    [Fact]
    public async Task RegistrarVentaAsync_PresupuestoYaConvertido_LanzaTransicionInvalidaSinGuardarNiModificarlo()
    {
        // Arrange
        var cuaderno = CrearArticulo(10, "Cuaderno", precio: 1500m, stock: 8);
        ConfigurarArticulos(cuaderno);
        var presupuesto = ConfigurarPresupuesto(Hoy, (10, 3, 1500m));
        presupuesto.RegistrarConversion();
        var cierreOriginal = presupuesto.FechaCierre;

        // Act
        var act = () => _sut.RegistrarVentaAsync(CrearDto(
            [Item(cuaderno, 1)],
            [Pago(MedioPagoEnum.Efectivo, 1500m)],
            idPresupuestoOrigen: presupuesto.Id));

        // Assert
        await act.Should().ThrowAsync<TransicionPresupuestoInvalidaException>();
        presupuesto.FechaCierre.Should().Be(cierreOriginal);
        await _presupuestoRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
        cuaderno.StockActual.Should().Be(8);
        await AssertNoSeGuardoNadaAsync();
    }

    [Fact]
    public async Task RegistrarVentaAsync_PresupuestoInexistente_LanzaDomainExceptionSinGuardar()
    {
        // Arrange
        var cuaderno = CrearArticulo(10, "Cuaderno", precio: 1500m, stock: 8);
        ConfigurarArticulos(cuaderno);

        // Act
        var act = () => _sut.RegistrarVentaAsync(CrearDto(
            [Item(cuaderno, 1)],
            [Pago(MedioPagoEnum.Efectivo, 1500m)],
            idPresupuestoOrigen: 99));

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*presupuesto*");
        await AssertNoSeGuardoNadaAsync();
    }

    [Fact]
    public async Task RegistrarVentaAsync_PresupuestoPendienteYStockInsuficiente_NoLoDejaConvertidoEnElContexto()
    {
        // Arrange: la venta falla en el paso del stock, después de haber validado el presupuesto.
        var cuaderno = CrearArticulo(10, "Cuaderno", precio: 1500m, stock: 1);
        ConfigurarArticulos(cuaderno);
        var presupuesto = ConfigurarPresupuesto(Hoy, (10, 3, 1500m));

        // Act
        var act = () => _sut.RegistrarVentaAsync(CrearDto(
            [Item(cuaderno, 3)],
            [Pago(MedioPagoEnum.Efectivo, 4500m)],
            idPresupuestoOrigen: presupuesto.Id));

        // Assert
        await act.Should().ThrowAsync<StockInsuficienteException>();
        presupuesto.Estado.Should().Be(EstadoPresupuestoEnum.Pendiente, "el presupuesto solo se convierte con todo validado");
        await _presupuestoRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
        await AssertNoSeGuardoNadaAsync();
    }

    [Fact]
    public async Task VerificarTicketAsync_PresupuestoVigente_NoInformaComoDesactualizadoElPrecioPactado()
    {
        // Arrange: catálogo en 1800, pactado en 1500.
        ConfigurarDisponibilidad(Disponibilidad(10, "Cuaderno", precio: 1800m, stock: 8));
        ConfigurarPresupuestoDto(vigente: true, (10, 3, 1500m));

        // Act
        var resultado = await _sut.VerificarTicketAsync([ItemDto(10, "Cuaderno", cantidad: 2, precio: 1500m)], idPresupuestoOrigen: 5);

        // Assert
        resultado.PreciosActualizados.Should().BeEmpty();
    }

    [Fact]
    public async Task VerificarTicketAsync_PresupuestoVencido_InformaElPrecioDeCatalogo()
    {
        // Arrange
        ConfigurarDisponibilidad(Disponibilidad(10, "Cuaderno", precio: 1800m, stock: 8));
        ConfigurarPresupuestoDto(vigente: false, (10, 3, 1500m));

        // Act
        var resultado = await _sut.VerificarTicketAsync([ItemDto(10, "Cuaderno", cantidad: 2, precio: 1500m)], idPresupuestoOrigen: 5);

        // Assert
        resultado.PreciosActualizados.Should().ContainSingle()
            .Which.PrecioActual.Should().Be(1800m);
    }

    [Fact]
    public async Task VerificarTicketAsync_PresupuestoVigenteConArticuloAgregadoAparte_UsaElCatalogoParaEse()
    {
        // Arrange: el artículo 11 no figura en el presupuesto (PS-04).
        ConfigurarDisponibilidad(
            Disponibilidad(10, "Cuaderno", precio: 1800m, stock: 8),
            Disponibilidad(11, "Lápiz", precio: 300m, stock: 8));
        ConfigurarPresupuestoDto(vigente: true, (10, 3, 1500m));

        // Act
        var resultado = await _sut.VerificarTicketAsync(
            [ItemDto(10, "Cuaderno", 1, 1500m), ItemDto(11, "Lápiz", 1, 250m)],
            idPresupuestoOrigen: 5);

        // Assert
        resultado.PreciosActualizados.Should().ContainSingle().Which.IdArticulo.Should().Be(11);
    }

    private Presupuesto ConfigurarPresupuesto(DateOnly fechaEmision, params (int IdArticulo, int Cantidad, decimal Precio)[] renglones)
    {
        var presupuesto = Presupuesto.Emitir(
            IdUsuario,
            idCliente: null,
            titulo: null,
            renglones.Select(r => new RenglonAPresupuestar(r.IdArticulo, $"Artículo {r.IdArticulo}", r.Cantidad, r.Precio)).ToList(),
            descuento: 0m,
            Presupuesto.VigenciaPorDefectoDias,
            fechaEmision);
        presupuesto.Id = 5;

        _presupuestoRepository
            .GetByIdWithIncludesAsync(5, Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Presupuesto, object>>[]>())
            .Returns(presupuesto);

        return presupuesto;
    }

    private void ConfigurarPresupuestoDto(bool vigente, params (int IdArticulo, int Cantidad, decimal Precio)[] renglones)
    {
        var items = renglones
            .Select(r => new DetallePresupuestoDto
            {
                IdArticulo = r.IdArticulo,
                Descripcion = $"Artículo {r.IdArticulo}",
                Cantidad = r.Cantidad,
                PrecioUnitarioPactado = r.Precio
            })
            .ToList();

        _presupuestoQueryService
            .ObtenerAsync(5, Hoy, Arg.Any<CancellationToken>())
            .Returns(new PresupuestoDto
            {
                IdPresupuesto = 5,
                IdUsuario = IdUsuario,
                FechaEmision = DateTime.UtcNow,
                FechaVencimiento = vigente ? Hoy.AddDays(5) : Hoy.AddDays(-1),
                Subtotal = items.Sum(i => i.SubtotalItem),
                Descuento = 0m,
                Total = items.Sum(i => i.SubtotalItem),
                Estado = EstadoPresupuestoEnum.Pendiente,
                EstaVigente = vigente,
                Items = items
            });
    }

    private static Articulo CrearArticulo(int id, string descripcion, decimal precio, int stock)
    {
        return new Articulo
        {
            Id = id,
            Descripcion = descripcion,
            PrecioVenta = precio,
            StockActual = stock
        };
    }

    private void ConfigurarArticulos(params Articulo[] articulos)
    {
        _articuloRepository
            .FindAsync(Arg.Any<Expression<Func<Articulo, bool>>>(), false, Arg.Any<CancellationToken>())
            .Returns(articulos);
    }

    private void ConfigurarDisponibilidad(params DisponibilidadArticuloDto[] disponibilidades)
    {
        _articuloQueryService
            .ObtenerDisponibilidadAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(disponibilidades);
    }

    private static DisponibilidadArticuloDto Disponibilidad(int id, string descripcion, decimal precio, int stock)
    {
        return new DisponibilidadArticuloDto
        {
            IdArticulo = id,
            Descripcion = descripcion,
            PrecioVenta = precio,
            StockActual = stock,
            EsServicio = false
        };
    }

    private static DetalleVentaDto ItemDto(int idArticulo, string descripcion, int cantidad, decimal precio)
    {
        return new DetalleVentaDto
        {
            IdArticulo = idArticulo,
            Descripcion = descripcion,
            Cantidad = cantidad,
            PrecioUnitario = precio
        };
    }

    private Cliente ConfigurarClienteConCuentaCorriente(decimal limiteCredito)
    {
        var cliente = ClientesDePrueba.Crear(id: IdCliente, razonSocialONombre: "Librería del Centro");
        cliente.HabilitarCuentaCorriente(limiteCredito);
        _clienteRepository.GetByIdAsync(IdCliente, false, Arg.Any<CancellationToken>()).Returns(cliente);
        return cliente;
    }

    [Fact]
    public async Task RegistrarVentaAsync_ConflictoYTurnoCerradoEnOtraTerminal_LanzaCajaCerrada()
    {
        // Arrange: la primera lectura ve el turno abierto; tras el conflicto, la relectura lo encuentra cerrado.
        var cuaderno = CrearArticulo(10, "Cuaderno", precio: 1500m, stock: 8);
        ConfigurarArticulos(cuaderno);
        var turnoCerrado = TurnoCaja.Abrir(IdUsuario, idTerminal: 1, saldoInicial: 5000m);
        turnoCerrado.Id = IdTurno;
        turnoCerrado.CerrarConArqueoCiego(5000m, 0m);
        _turnoRepository.GetByIdAsync(IdTurno, Arg.Any<CancellationToken>()).Returns(_turno, turnoCerrado);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new ConflictoDeConcurrenciaException());

        var dto = CrearDto([Item(cuaderno, 1)], [Pago(MedioPagoEnum.Efectivo, 1500m)]);

        // Act
        var act = async () => await _sut.RegistrarVentaAsync(dto);

        // Assert
        var excepcion = await act.Should().ThrowAsync<CajaCerradaException>();
        excepcion.WithMessage("*cerrado desde otra terminal*");
        excepcion.And.InnerException.Should().BeOfType<ConflictoDeConcurrenciaException>();
        await _ticketPrinterService.DidNotReceiveWithAnyArgs().ImprimirTicketVentaAsync(default!, default);
    }

    [Fact]
    public async Task RegistrarVentaAsync_ConflictoConTurnoAunAbierto_RelanzaElConflictoOriginal()
    {
        // Arrange: el conflicto lo causó otro agregado (por ejemplo, el stock): el turno sigue abierto.
        var cuaderno = CrearArticulo(10, "Cuaderno", precio: 1500m, stock: 8);
        ConfigurarArticulos(cuaderno);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new ConflictoDeConcurrenciaException());

        var dto = CrearDto([Item(cuaderno, 1)], [Pago(MedioPagoEnum.Efectivo, 1500m)]);

        // Act
        var act = async () => await _sut.RegistrarVentaAsync(dto);

        // Assert
        await act.Should().ThrowAsync<ConflictoDeConcurrenciaException>();
        await _ticketPrinterService.DidNotReceiveWithAnyArgs().ImprimirTicketVentaAsync(default!, default);
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

    private static CrearVentaDto CrearDto(
        IReadOnlyList<DetalleVentaDto> items,
        IReadOnlyList<PagoVentaDto> pagos,
        int? idCliente = null,
        int? idPresupuestoOrigen = null)
    {
        return new CrearVentaDto
        {
            IdTurno = IdTurno,
            IdUsuario = IdUsuario,
            IdCliente = idCliente,
            IdPresupuestoOrigen = idPresupuestoOrigen,
            Items = items,
            Pagos = pagos
        };
    }

    private async Task AssertNoSeGuardoNadaAsync()
    {
        await _ventaRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }
}

using System.Linq.Expressions;
using FluentAssertions;
using FluentValidation;
using NSubstitute;
using Retail.Application.DTOs.Presupuestos;
using Retail.Application.DTOs.Ventas;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Application.UnitTests.TestData;
using Retail.Application.Validators.Presupuestos;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Application.UnitTests.Services;

public class PresupuestoServiceTests
{
    private const int IdUsuario = 2;
    private const int IdPresupuesto = 7;

    // El reloj de prueba fija el 15/10/2026 (hora local UTC-3).
    private static readonly DateOnly Hoy = new(2026, 10, 15);

    private readonly IRepository<Presupuesto> _presupuestoRepository = Substitute.For<IRepository<Presupuesto>>();
    private readonly IRepository<Articulo> _articuloRepository = Substitute.For<IRepository<Articulo>>();
    private readonly IRepository<Cliente> _clienteRepository = Substitute.For<IRepository<Cliente>>();
    private readonly IPresupuestoQueryService _consultas = Substitute.For<IPresupuestoQueryService>();
    private readonly IArticuloQueryService _articuloQueryService = Substitute.For<IArticuloQueryService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITicketPrinterService _ticketPrinterService = Substitute.For<ITicketPrinterService>();
    private readonly IExportadorPresupuestoPdf _exportadorPdf = Substitute.For<IExportadorPresupuestoPdf>();
    private readonly PresupuestoService _sut;

    public PresupuestoServiceTests()
    {
        _sut = new PresupuestoService(
            _presupuestoRepository,
            _articuloRepository,
            _clienteRepository,
            _consultas,
            _articuloQueryService,
            _unitOfWork,
            _ticketPrinterService,
            _exportadorPdf,
            RelojDePrueba.EnQuinceDeOctubre(),
            new CrearPresupuestoValidator(),
            new ConsultaPresupuestosValidator());
    }

    // ── EmitirAsync ──

    [Fact]
    public async Task EmitirAsync_PrecioVistoIgualAlDeCatalogo_EmiteConPrecioYDescripcionDelArticulo()
    {
        // Arrange: el DTO trae una descripción distinta; el presupuesto debe guardar la del artículo (PS-11).
        var cuaderno = CrearArticulo(10, "Cuaderno A4", precio: 1500m);
        ConfigurarArticulos(cuaderno);
        ConfigurarLecturaDelPresupuestoGuardado();

        Presupuesto? emitido = null;
        await _presupuestoRepository.AddAsync(Arg.Do<Presupuesto>(p => emitido = p), Arg.Any<CancellationToken>());

        var dto = CrearDto([Item(10, "Descripción vieja", cantidad: 2, precio: 1500m)], titulo: "  Lista   escolar ");

        // Act
        var resultado = await _sut.EmitirAsync(dto);

        // Assert
        emitido.Should().NotBeNull();
        emitido!.Detalles.Should().ContainSingle()
            .Which.Should().Match<DetallePresupuesto>(d => d.Descripcion == "Cuaderno A4" && d.PrecioUnitarioPactado == 1500m && d.Cantidad == 2);
        emitido.Total.Should().Be(3000m);
        emitido.Titulo.Should().Be("Lista escolar");
        emitido.FechaVencimiento.Should().Be(Hoy.AddDays(Presupuesto.VigenciaPorDefectoDias));
        resultado.IdPresupuesto.Should().Be(IdPresupuesto);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmitirAsync_VigenciaPersonalizada_VenceElDiaCalculadoConLaFechaLocal()
    {
        // Arrange
        ConfigurarArticulos(CrearArticulo(10, "Cuaderno", precio: 1500m));
        ConfigurarLecturaDelPresupuestoGuardado();
        Presupuesto? emitido = null;
        await _presupuestoRepository.AddAsync(Arg.Do<Presupuesto>(p => emitido = p), Arg.Any<CancellationToken>());

        // Act
        await _sut.EmitirAsync(CrearDto([Item(10, "Cuaderno", 1, 1500m)]) with { VigenciaDias = 30 });

        // Assert
        emitido!.FechaVencimiento.Should().Be(Hoy.AddDays(30));
    }

    [Fact]
    public async Task EmitirAsync_PrecioVistoDesactualizado_LanzaDomainExceptionSinGuardar()
    {
        // Arrange
        ConfigurarArticulos(CrearArticulo(10, "Cuaderno", precio: 1800m));

        // Act
        var act = () => _sut.EmitirAsync(CrearDto([Item(10, "Cuaderno", 1, 1500m)]));

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*precio*cambió*");
        await AssertNoSeGuardoNadaAsync();
    }

    [Fact]
    public async Task EmitirAsync_ArticuloDadoDeBaja_LanzaDomainExceptionSinGuardar()
    {
        // Arrange: el repositorio no devuelve artículos borrados lógicamente.
        ConfigurarArticulos();

        // Act
        var act = () => _sut.EmitirAsync(CrearDto([Item(10, "Cuaderno", 1, 1500m)]));

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*Cuaderno*dado de baja*");
        await AssertNoSeGuardoNadaAsync();
    }

    [Fact]
    public async Task EmitirAsync_ClienteInexistente_LanzaDomainExceptionSinGuardar()
    {
        // Arrange
        ConfigurarArticulos(CrearArticulo(10, "Cuaderno", precio: 1500m));
        _clienteRepository.GetByIdAsync(3, false, Arg.Any<CancellationToken>()).Returns((Cliente?)null);

        // Act
        var act = () => _sut.EmitirAsync(CrearDto([Item(10, "Cuaderno", 1, 1500m)]) with { IdCliente = 3 });

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*cliente*3*");
        await AssertNoSeGuardoNadaAsync();
    }

    [Fact]
    public async Task EmitirAsync_ClienteExistente_LoRegistraEnElPresupuesto()
    {
        // Arrange
        ConfigurarArticulos(CrearArticulo(10, "Cuaderno", precio: 1500m));
        ConfigurarLecturaDelPresupuestoGuardado();
        _clienteRepository.GetByIdAsync(3, false, Arg.Any<CancellationToken>())
            .Returns(ClientesDePrueba.Crear(id: 3, razonSocialONombre: "Instituto Rivadavia"));
        Presupuesto? emitido = null;
        await _presupuestoRepository.AddAsync(Arg.Do<Presupuesto>(p => emitido = p), Arg.Any<CancellationToken>());

        // Act
        await _sut.EmitirAsync(CrearDto([Item(10, "Cuaderno", 1, 1500m)]) with { IdCliente = 3 });

        // Assert
        emitido!.IdCliente.Should().Be(3);
    }

    [Fact]
    public async Task EmitirAsync_SinTurnoDeCajaAbierto_EmiteIgual()
    {
        // Arrange: el servicio ni siquiera depende de la caja (PS-17); no hay forma de que la consulte.
        ConfigurarArticulos(CrearArticulo(10, "Cuaderno", precio: 1500m));
        ConfigurarLecturaDelPresupuestoGuardado();

        // Act
        var resultado = await _sut.EmitirAsync(CrearDto([Item(10, "Cuaderno", 1, 1500m)]));

        // Assert
        resultado.Should().NotBeNull();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmitirAsync_DtoInvalido_LanzaValidationExceptionSinConsultarLaBase()
    {
        // Act
        var act = () => _sut.EmitirAsync(CrearDto([]));

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        await _articuloRepository.DidNotReceiveWithAnyArgs().FindAsync(default!, default, default);
        await AssertNoSeGuardoNadaAsync();
    }

    [Fact]
    public async Task EmitirAsync_LaConsultaNoEncuentraElGuardado_LanzaInvalidOperationException()
    {
        // Arrange
        ConfigurarArticulos(CrearArticulo(10, "Cuaderno", precio: 1500m));
        _consultas.ObtenerAsync(Arg.Any<int>(), Hoy, Arg.Any<CancellationToken>()).Returns((PresupuestoDto?)null);

        // Act
        var act = () => _sut.EmitirAsync(CrearDto([Item(10, "Cuaderno", 1, 1500m)]));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── ListarAsync / ObtenerAsync ──

    [Fact]
    public async Task ListarAsync_ConsultaValida_DelegaEnLaConsultaConLaFechaDeHoy()
    {
        // Arrange
        var pagina = new PresupuestosPaginadosDto { TotalRegistros = 1, PaginaActual = 1, TamanoPagina = 20 };
        var consulta = new ConsultaPresupuestosDto { TerminoBusqueda = "escuela", Filtro = FiltroEstadoPresupuestoEnum.Vigentes };
        _consultas.ListarAsync(consulta, Hoy, Arg.Any<CancellationToken>()).Returns(pagina);

        // Act
        var resultado = await _sut.ListarAsync(consulta);

        // Assert
        resultado.Should().BeSameAs(pagina);
    }

    [Fact]
    public async Task ListarAsync_PaginaInvalida_LanzaValidationException()
    {
        // Act
        var act = () => _sut.ListarAsync(new ConsultaPresupuestosDto { Pagina = 0 });

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        await _consultas.DidNotReceiveWithAnyArgs().ListarAsync(default!, default, default);
    }

    [Fact]
    public async Task ObtenerAsync_Existente_DelegaEnLaConsultaConLaFechaDeHoy()
    {
        // Arrange
        var presupuesto = CrearPresupuestoDto(vigente: true);
        _consultas.ObtenerAsync(IdPresupuesto, Hoy, Arg.Any<CancellationToken>()).Returns(presupuesto);

        // Act
        var resultado = await _sut.ObtenerAsync(IdPresupuesto);

        // Assert
        resultado.Should().BeSameAs(presupuesto);
    }

    // ── RecuperarParaVentaAsync ──

    [Fact]
    public async Task RecuperarParaVentaAsync_Vigente_CobraElPactadoSinDiscrepanciasAunqueElCatalogoCambie()
    {
        // Arrange: pactado 1500, catálogo 1800.
        ConfigurarPresupuesto(vigente: true, (10, 2, 1500m));
        ConfigurarDisponibilidad(Disponibilidad(10, "Cuaderno", precio: 1800m, stock: 8));

        // Act
        var resultado = await _sut.RecuperarParaVentaAsync(IdPresupuesto);

        // Assert
        resultado.EstaVigente.Should().BeTrue();
        var renglon = resultado.Renglones.Should().ContainSingle().Subject;
        renglon.PrecioPactado.Should().Be(1500m);
        renglon.PrecioActual.Should().Be(1800m);
        renglon.PrecioACobrar.Should().Be(1500m);
        renglon.Cantidad.Should().Be(2);
        renglon.StockActual.Should().Be(8);
        resultado.Discrepancias.Should().BeEmpty();
        resultado.RequiereConfirmacion.Should().BeFalse();
    }

    [Fact]
    public async Task RecuperarParaVentaAsync_Vencido_CobraElActualYListaLasDiscrepancias()
    {
        // Arrange
        ConfigurarPresupuesto(vigente: false, (10, 2, 1500m), (11, 1, 300m));
        ConfigurarDisponibilidad(
            Disponibilidad(10, "Cuaderno", precio: 1800m, stock: 8),
            Disponibilidad(11, "Lápiz", precio: 300m, stock: 8));

        // Act
        var resultado = await _sut.RecuperarParaVentaAsync(IdPresupuesto);

        // Assert
        resultado.EstaVigente.Should().BeFalse();
        resultado.Renglones.Single(r => r.IdArticulo == 10).PrecioACobrar.Should().Be(1800m);
        resultado.Renglones.Single(r => r.IdArticulo == 11).PrecioACobrar.Should().Be(300m);
        var discrepancia = resultado.Discrepancias.Should().ContainSingle().Subject;
        discrepancia.IdArticulo.Should().Be(10);
        discrepancia.PrecioPactado.Should().Be(1500m);
        discrepancia.PrecioActualCatalogo.Should().Be(1800m);
        resultado.RequiereConfirmacion.Should().BeTrue();
    }

    [Fact]
    public async Task RecuperarParaVentaAsync_VencidoSinCambiosDePrecio_NoRequiereConfirmacion()
    {
        // Arrange
        ConfigurarPresupuesto(vigente: false, (10, 2, 1500m));
        ConfigurarDisponibilidad(Disponibilidad(10, "Cuaderno", precio: 1500m, stock: 8));

        // Act
        var resultado = await _sut.RecuperarParaVentaAsync(IdPresupuesto);

        // Assert
        resultado.Discrepancias.Should().BeEmpty();
        resultado.RequiereConfirmacion.Should().BeFalse();
    }

    [Fact]
    public async Task RecuperarParaVentaAsync_StockInsuficiente_InformaFaltante()
    {
        // Arrange
        ConfigurarPresupuesto(vigente: true, (10, 5, 1500m));
        ConfigurarDisponibilidad(Disponibilidad(10, "Cuaderno", precio: 1500m, stock: 3));

        // Act
        var resultado = await _sut.RecuperarParaVentaAsync(IdPresupuesto);

        // Assert
        var faltante = resultado.Faltantes.Should().ContainSingle().Subject;
        faltante.CantidadSolicitada.Should().Be(5);
        faltante.StockActual.Should().Be(3);
        faltante.Faltante.Should().Be(2);
        resultado.RequiereConfirmacion.Should().BeTrue();
    }

    [Fact]
    public async Task RecuperarParaVentaAsync_PresentacionSinStock_CalculaLosOrigenesAFraccionar()
    {
        // Arrange: faltan 4 sueltos y cada pack rinde 10.
        ConfigurarPresupuesto(vigente: true, (20, 6, 50m));
        ConfigurarDisponibilidad(new DisponibilidadArticuloDto
        {
            IdArticulo = 20,
            Descripcion = "Sobre (unidad)",
            PrecioVenta = 50m,
            StockActual = 2,
            EsServicio = false,
            IdArticuloOrigen = 21,
            DescripcionOrigen = "Pack de sobres",
            UnidadesPorOrigen = 10,
            StockOrigen = 3
        });

        // Act
        var resultado = await _sut.RecuperarParaVentaAsync(IdPresupuesto);

        // Assert
        var faltante = resultado.Faltantes.Should().ContainSingle().Subject;
        faltante.OrigenesAFraccionar.Should().Be(1);
        faltante.PuedeFraccionar.Should().BeTrue();
    }

    [Fact]
    public async Task RecuperarParaVentaAsync_ServicioSinStock_NoEsFaltante()
    {
        // Arrange
        ConfigurarPresupuesto(vigente: true, (12, 10, 50m));
        ConfigurarDisponibilidad(Disponibilidad(12, "Fotocopia", precio: 50m, stock: 0) with { EsServicio = true });

        // Act
        var resultado = await _sut.RecuperarParaVentaAsync(IdPresupuesto);

        // Assert
        resultado.Faltantes.Should().BeEmpty();
        resultado.Renglones.Should().ContainSingle().Which.EsServicio.Should().BeTrue();
    }

    [Fact]
    public async Task RecuperarParaVentaAsync_ArticuloDadoDeBaja_LoInformaYLoQuitaDeLosRenglones()
    {
        // Arrange: el artículo 11 ya no existe en la disponibilidad.
        ConfigurarPresupuesto(vigente: true, (10, 1, 1500m), (11, 1, 300m));
        ConfigurarDisponibilidad(Disponibilidad(10, "Cuaderno", precio: 1500m, stock: 8));

        // Act
        var resultado = await _sut.RecuperarParaVentaAsync(IdPresupuesto);

        // Assert
        resultado.Renglones.Should().ContainSingle().Which.IdArticulo.Should().Be(10);
        resultado.ArticulosNoDisponibles.Should().ContainSingle().Which.Should().Be("Artículo 11");
        resultado.RequiereConfirmacion.Should().BeTrue();
    }

    [Theory]
    [InlineData(EstadoPresupuestoEnum.Convertido)]
    [InlineData(EstadoPresupuestoEnum.Anulado)]
    public async Task RecuperarParaVentaAsync_NoPendiente_LanzaTransicionInvalida(EstadoPresupuestoEnum estado)
    {
        // Arrange
        _consultas.ObtenerAsync(IdPresupuesto, Hoy, Arg.Any<CancellationToken>())
            .Returns(CrearPresupuestoDto(vigente: true, estado));

        // Act
        var act = () => _sut.RecuperarParaVentaAsync(IdPresupuesto);

        // Assert
        await act.Should().ThrowAsync<TransicionPresupuestoInvalidaException>()
            .Where(e => e.EstadoActual == estado);
    }

    [Fact]
    public async Task RecuperarParaVentaAsync_Inexistente_LanzaDomainException()
    {
        // Arrange
        _consultas.ObtenerAsync(IdPresupuesto, Hoy, Arg.Any<CancellationToken>()).Returns((PresupuestoDto?)null);

        // Act
        var act = () => _sut.RecuperarParaVentaAsync(IdPresupuesto);

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*presupuesto*");
    }

    [Fact]
    public async Task RecuperarParaVentaAsync_PresupuestoPendiente_NoModificaNiGuardaNada()
    {
        // Arrange
        ConfigurarPresupuesto(vigente: true, (10, 1, 1500m));
        ConfigurarDisponibilidad(Disponibilidad(10, "Cuaderno", precio: 1500m, stock: 8));

        // Act
        await _sut.RecuperarParaVentaAsync(IdPresupuesto);

        // Assert
        await AssertNoSeGuardoNadaAsync();
        await _presupuestoRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    // ── AnularAsync ──

    [Fact]
    public async Task AnularAsync_Pendiente_LoAnulaYGuardaUnaSolaVez()
    {
        // Arrange
        var presupuesto = EmitirDeDominio();
        _presupuestoRepository.GetByIdAsync(IdPresupuesto, Arg.Any<CancellationToken>()).Returns(presupuesto);

        // Act
        await _sut.AnularAsync(IdPresupuesto);

        // Assert
        presupuesto.Estado.Should().Be(EstadoPresupuestoEnum.Anulado);
        await _presupuestoRepository.Received(1).UpdateAsync(presupuesto, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _unitOfWork.Received(1).DescartarCambios();
    }

    [Fact]
    public async Task AnularAsync_Inexistente_LanzaDomainExceptionSinGuardar()
    {
        // Arrange
        _presupuestoRepository.GetByIdAsync(IdPresupuesto, Arg.Any<CancellationToken>()).Returns((Presupuesto?)null);

        // Act
        var act = () => _sut.AnularAsync(IdPresupuesto);

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*presupuesto*");
        await AssertNoSeGuardoNadaAsync();
    }

    [Fact]
    public async Task AnularAsync_YaConvertido_LanzaTransicionInvalidaSinGuardar()
    {
        // Arrange
        var presupuesto = EmitirDeDominio();
        presupuesto.RegistrarConversion();
        _presupuestoRepository.GetByIdAsync(IdPresupuesto, Arg.Any<CancellationToken>()).Returns(presupuesto);

        // Act
        var act = () => _sut.AnularAsync(IdPresupuesto);

        // Assert
        await act.Should().ThrowAsync<TransicionPresupuestoInvalidaException>();
        await AssertNoSeGuardoNadaAsync();
    }

    // ── ImprimirAsync / ExportarPdfAsync ──

    [Fact]
    public async Task ImprimirAsync_Existente_DelegaEnLaImpresora()
    {
        // Arrange
        var presupuesto = CrearPresupuestoDto(vigente: true);
        _consultas.ObtenerAsync(IdPresupuesto, Hoy, Arg.Any<CancellationToken>()).Returns(presupuesto);

        // Act
        await _sut.ImprimirAsync(IdPresupuesto);

        // Assert
        await _ticketPrinterService.Received(1).ImprimirPresupuestoAsync(presupuesto, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImprimirAsync_Inexistente_LanzaDomainExceptionSinImprimir()
    {
        // Arrange
        _consultas.ObtenerAsync(IdPresupuesto, Hoy, Arg.Any<CancellationToken>()).Returns((PresupuestoDto?)null);

        // Act
        var act = () => _sut.ImprimirAsync(IdPresupuesto);

        // Assert
        await act.Should().ThrowAsync<DomainException>();
        await _ticketPrinterService.DidNotReceiveWithAnyArgs().ImprimirPresupuestoAsync(default!, default);
    }

    [Fact]
    public async Task ExportarPdfAsync_Existente_EntregaElDtoYElStreamAlExportador()
    {
        // Arrange
        var presupuesto = CrearPresupuestoDto(vigente: true);
        _consultas.ObtenerAsync(IdPresupuesto, Hoy, Arg.Any<CancellationToken>()).Returns(presupuesto);
        await using var destino = new MemoryStream();

        // Act
        await _sut.ExportarPdfAsync(IdPresupuesto, destino);

        // Assert
        await _exportadorPdf.Received(1).ExportarAsync(presupuesto, destino, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportarPdfAsync_Inexistente_LanzaDomainExceptionSinExportar()
    {
        // Arrange
        _consultas.ObtenerAsync(IdPresupuesto, Hoy, Arg.Any<CancellationToken>()).Returns((PresupuestoDto?)null);
        await using var destino = new MemoryStream();

        // Act
        var act = () => _sut.ExportarPdfAsync(IdPresupuesto, destino);

        // Assert
        await act.Should().ThrowAsync<DomainException>();
        await _exportadorPdf.DidNotReceiveWithAnyArgs().ExportarAsync(default!, default!, default);
    }

    // ── Auxiliares ──

    private static Articulo CrearArticulo(int id, string descripcion, decimal precio)
    {
        return new Articulo
        {
            Id = id,
            Descripcion = descripcion,
            PrecioVenta = precio,
            StockActual = 10
        };
    }

    private void ConfigurarArticulos(params Articulo[] articulos)
    {
        _articuloRepository
            .FindAsync(Arg.Any<Expression<Func<Articulo, bool>>>(), false, Arg.Any<CancellationToken>())
            .Returns(articulos);
    }

    private void ConfigurarLecturaDelPresupuestoGuardado()
    {
        _consultas
            .ObtenerAsync(Arg.Any<int>(), Hoy, Arg.Any<CancellationToken>())
            .Returns(CrearPresupuestoDto(vigente: true));
    }

    private void ConfigurarPresupuesto(bool vigente, params (int IdArticulo, int Cantidad, decimal Precio)[] renglones)
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

        _consultas
            .ObtenerAsync(IdPresupuesto, Hoy, Arg.Any<CancellationToken>())
            .Returns(CrearPresupuestoDto(vigente, items: items));
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

    private static PresupuestoDto CrearPresupuestoDto(
        bool vigente,
        EstadoPresupuestoEnum estado = EstadoPresupuestoEnum.Pendiente,
        IReadOnlyList<DetallePresupuestoDto>? items = null)
    {
        items ??= [new DetallePresupuestoDto { IdArticulo = 10, Descripcion = "Cuaderno", Cantidad = 1, PrecioUnitarioPactado = 1500m }];
        var subtotal = items.Sum(i => i.SubtotalItem);

        return new PresupuestoDto
        {
            IdPresupuesto = IdPresupuesto,
            IdUsuario = IdUsuario,
            FechaEmision = DateTime.UtcNow,
            FechaVencimiento = vigente ? Hoy.AddDays(5) : Hoy.AddDays(-1),
            Subtotal = subtotal,
            Descuento = 0m,
            Total = subtotal,
            Estado = estado,
            EstaVigente = vigente,
            Items = items
        };
    }

    private static Presupuesto EmitirDeDominio()
    {
        var presupuesto = Presupuesto.Emitir(
            IdUsuario,
            idCliente: null,
            titulo: null,
            [new RenglonAPresupuestar(10, "Cuaderno", 1, 1500m)],
            descuento: 0m,
            Presupuesto.VigenciaPorDefectoDias,
            Hoy);
        presupuesto.Id = IdPresupuesto;
        return presupuesto;
    }

    private static CrearPresupuestoDto CrearDto(IReadOnlyList<DetallePresupuestoDto> items, string? titulo = null)
    {
        return new CrearPresupuestoDto
        {
            IdUsuario = IdUsuario,
            Titulo = titulo,
            Items = items
        };
    }

    private static DetallePresupuestoDto Item(int idArticulo, string descripcion, int cantidad, decimal precio)
    {
        return new DetallePresupuestoDto
        {
            IdArticulo = idArticulo,
            Descripcion = descripcion,
            Cantidad = cantidad,
            PrecioUnitarioPactado = precio
        };
    }

    private async Task AssertNoSeGuardoNadaAsync()
    {
        await _presupuestoRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }
}

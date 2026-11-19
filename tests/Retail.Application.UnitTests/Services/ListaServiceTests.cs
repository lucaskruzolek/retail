using System.Linq.Expressions;
using FluentAssertions;
using NSubstitute;
using Retail.Application.DTOs.Listas;
using Retail.Application.DTOs.Ventas;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Application.UnitTests.TestData;
using Retail.Application.Validators.Listas;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Application.UnitTests.Services;

public class ListaServiceTests
{
    private const int IdUsuario = 2;
    private const int IdLista = 7;

    // El reloj de prueba fija el 15/10/2026 (hora local UTC-3).
    private static readonly DateOnly Hoy = new(2026, 10, 15);

    private readonly IRepository<ListaArticulos> _listaRepository = Substitute.For<IRepository<ListaArticulos>>();
    private readonly IRepository<Articulo> _articuloRepository = Substitute.For<IRepository<Articulo>>();
    private readonly IListaQueryService _consultas = Substitute.For<IListaQueryService>();
    private readonly IArticuloQueryService _articuloQueryService = Substitute.For<IArticuloQueryService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITicketPrinterService _ticketPrinterService = Substitute.For<ITicketPrinterService>();
    private readonly IExportadorListaPdf _exportadorPdf = Substitute.For<IExportadorListaPdf>();
    private readonly ListaService _sut;

    public ListaServiceTests()
    {
        _sut = new ListaService(
            _listaRepository,
            _articuloRepository,
            _consultas,
            _articuloQueryService,
            _unitOfWork,
            _ticketPrinterService,
            _exportadorPdf,
            RelojDePrueba.EnQuinceDeOctubre(),
            new CrearListaValidator(),
            new ActualizarListaValidator(),
            new ConsultaListasValidator());
    }

    // ── CrearAsync ──

    [Fact]
    public async Task CrearAsync_DatosValidos_AgregaLaListaYGuarda()
    {
        // Arrange
        ConfigurarArticulosActivos(CrearArticulo(10, "Cuaderno"));
        ConfigurarLecturaDeLaLista(CrearListaDto());

        ListaArticulos? creada = null;
        await _listaRepository.AddAsync(Arg.Do<ListaArticulos>(l => creada = l), Arg.Any<CancellationToken>());

        var dto = new CrearListaDto
        {
            IdUsuario = IdUsuario,
            Nombre = "  Útiles   1° grado ",
            Renglones = [new RenglonListaEntradaDto { IdArticulo = 10, Cantidad = 3 }]
        };

        // Act
        var resultado = await _sut.CrearAsync(dto);

        // Assert
        creada.Should().NotBeNull();
        creada!.Nombre.Should().Be("Útiles 1° grado");
        creada.Renglones.Should().ContainSingle().Which.Cantidad.Should().Be(3);
        resultado.IdLista.Should().Be(IdLista);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _unitOfWork.Received(1).DescartarCambios();
    }

    [Fact]
    public async Task CrearAsync_ArticuloDadoDeBaja_LanzaDomainExceptionSinGuardar()
    {
        // Arrange: el repositorio no devuelve artículos borrados lógicamente.
        ConfigurarArticulosActivos();

        // Act
        var act = () => _sut.CrearAsync(CrearDtoDeCreacion());

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*artículo ID 10*dado de baja*");
        await _listaRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task CrearAsync_NombreRepetido_LanzaDomainExceptionConElNombre()
    {
        // Arrange
        ConfigurarArticulosActivos(CrearArticulo(10, "Cuaderno"));
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns<Task<int>>(_ => throw new RestriccionDeUnicidadException());

        // Act
        var act = () => _sut.CrearAsync(CrearDtoDeCreacion() with { Nombre = "  Útiles   2° grado " });

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("Ya existe una lista llamada «Útiles 2° grado».");
        _unitOfWork.Received(1).DescartarCambios();
    }

    // ── ActualizarAsync ──

    [Fact]
    public async Task ActualizarAsync_DatosValidos_RenombraYReemplazaLosRenglones()
    {
        // Arrange
        var lista = CrearListaDeDominio((10, 1), (11, 2));
        ConfigurarListaExistente(lista);
        ConfigurarArticulosActivos(CrearArticulo(12, "Regla"));
        ConfigurarLecturaDeLaLista(CrearListaDto());

        var dto = new ActualizarListaDto
        {
            Nombre = "Nuevo nombre",
            FechaModificacionLeida = lista.FechaModificacion,
            Renglones =
            [
                new RenglonListaEntradaDto { IdArticulo = 10, Cantidad = 5 },
                new RenglonListaEntradaDto { IdArticulo = 12, Cantidad = 1 }
            ]
        };

        // Act
        await _sut.ActualizarAsync(IdLista, dto);

        // Assert
        lista.Nombre.Should().Be("Nuevo nombre");
        lista.Renglones.Select(r => (r.IdArticulo, r.Cantidad)).Should().BeEquivalentTo([(10, 5), (12, 1)]);
        await _listaRepository.Received(1).UpdateAsync(lista, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _unitOfWork.Received(1).DescartarCambios();
    }

    [Fact]
    public async Task ActualizarAsync_FechaLeidaDesactualizada_LanzaDomainExceptionSinGuardar()
    {
        // Arrange
        var lista = CrearListaDeDominio((10, 1));
        ConfigurarListaExistente(lista);

        var dto = CrearDtoDeActualizacion(lista.FechaModificacion.AddMinutes(-5));

        // Act
        var act = () => _sut.ActualizarAsync(IdLista, dto);

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("Otro usuario modificó la lista «Útiles»*");
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task ActualizarAsync_ConflictoDeConcurrenciaAlGuardar_LoTraduceAlMensajeDeModificacion()
    {
        // Arrange
        var lista = CrearListaDeDominio((10, 1));
        ConfigurarListaExistente(lista);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns<Task<int>>(_ => throw new ConflictoDeConcurrenciaException());

        // Act
        var act = () => _sut.ActualizarAsync(IdLista, CrearDtoDeActualizacion(lista.FechaModificacion));

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("Otro usuario modificó la lista*");
        _unitOfWork.Received(1).DescartarCambios();
    }

    [Fact]
    public async Task ActualizarAsync_ArticuloYaPresenteDadoDeBaja_SeConservaSinRechazar()
    {
        // Arrange: el artículo 10 ya figuraba en la lista y hoy está dado de baja; solo se renombra.
        var lista = CrearListaDeDominio((10, 1));
        ConfigurarListaExistente(lista);
        ConfigurarArticulosActivos();
        ConfigurarLecturaDeLaLista(CrearListaDto());

        // Act
        await _sut.ActualizarAsync(IdLista, CrearDtoDeActualizacion(lista.FechaModificacion) with { Nombre = "Renombrada" });

        // Assert
        lista.Nombre.Should().Be("Renombrada");
        lista.Renglones.Should().ContainSingle(r => r.IdArticulo == 10);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActualizarAsync_ListaInexistente_LanzaDomainException()
    {
        // Arrange
        _listaRepository
            .GetByIdWithIncludesAsync(IdLista, Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<ListaArticulos, object>>[]>())
            .Returns((ListaArticulos?)null);

        // Act
        var act = () => _sut.ActualizarAsync(IdLista, CrearDtoDeActualizacion(DateTime.UtcNow));

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("No se encontró la lista 7.");
    }

    // ── EliminarAsync ──

    [Fact]
    public async Task EliminarAsync_ListaExistente_LaDaDeBajaLogicamenteYGuarda()
    {
        // Arrange
        var lista = CrearListaDeDominio((10, 1));
        _listaRepository.GetByIdAsync(IdLista, Arg.Any<CancellationToken>()).Returns(lista);

        // Act
        await _sut.EliminarAsync(IdLista);

        // Assert
        await _listaRepository.Received(1).DeleteAsync(lista, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── ObtenerParaVentaAsync ──

    [Fact]
    public async Task ObtenerParaVentaAsync_ArticuloDadoDeBaja_LoOmiteYLoInformaComoNoDisponible()
    {
        // Arrange
        ConfigurarLecturaDeLaLista(CrearListaDto(
            Renglon(10, "Cuaderno", 2, precioGuardado: 1000m),
            Renglon(11, "Regla vieja", 1, precioGuardado: 200m, disponible: false)));
        ConfigurarDisponibilidad(Disponibilidad(10, "Cuaderno", 1500m, stock: 10));

        // Act
        var resultado = await _sut.ObtenerParaVentaAsync(IdLista);

        // Assert: el precio es el de catálogo de hoy (L-08), no el que traía la lectura de la lista.
        resultado.Renglones.Should().ContainSingle().Which.PrecioActual.Should().Be(1500m);
        resultado.ArticulosNoDisponibles.Should().ContainSingle().Which.Should().Be("Regla vieja");
        resultado.Faltantes.Should().BeEmpty();
        resultado.RequiereConfirmacion.Should().BeTrue();
    }

    [Fact]
    public async Task ObtenerParaVentaAsync_CantidadMayorAlStock_InformaElFaltante()
    {
        // Arrange
        ConfigurarLecturaDeLaLista(CrearListaDto(Renglon(10, "Cuaderno", 5, precioGuardado: 1500m)));
        ConfigurarDisponibilidad(Disponibilidad(10, "Cuaderno", 1500m, stock: 2));

        // Act
        var resultado = await _sut.ObtenerParaVentaAsync(IdLista);

        // Assert
        var faltante = resultado.Faltantes.Should().ContainSingle().Subject;
        faltante.IdArticulo.Should().Be(10);
        faltante.CantidadSolicitada.Should().Be(5);
        faltante.StockActual.Should().Be(2);
        resultado.RequiereConfirmacion.Should().BeTrue();
    }

    [Fact]
    public async Task ObtenerParaVentaAsync_ListaInexistente_LanzaDomainException()
    {
        // Arrange
        _consultas.ObtenerAsync(IdLista, Arg.Any<CancellationToken>()).Returns((ListaDto?)null);

        // Act
        var act = () => _sut.ObtenerParaVentaAsync(IdLista);

        // Assert
        await act.Should().ThrowAsync<DomainException>();
    }

    // ── ImprimirAsync / ExportarPdfAsync ──

    [Fact]
    public async Task ExportarPdfAsync_ListaExistente_PasaLaFechaLocalDeHoyAlExportador()
    {
        // Arrange
        var lista = CrearListaDto();
        ConfigurarLecturaDeLaLista(lista);
        using var destino = new MemoryStream();

        // Act
        await _sut.ExportarPdfAsync(IdLista, destino);

        // Assert
        await _exportadorPdf.Received(1).ExportarAsync(lista, Hoy, destino, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImprimirAsync_ListaExistente_DelegaEnLaImpresoraConLaFechaDeHoy()
    {
        // Arrange
        var lista = CrearListaDto();
        ConfigurarLecturaDeLaLista(lista);

        // Act
        await _sut.ImprimirAsync(IdLista);

        // Assert
        await _ticketPrinterService.Received(1).ImprimirListaAsync(lista, Hoy, Arg.Any<CancellationToken>());
    }

    // ── Auxiliares ──

    private static Articulo CrearArticulo(int id, string descripcion)
    {
        return new Articulo
        {
            Id = id,
            Descripcion = descripcion,
            PrecioVenta = 1500m,
            StockActual = 10
        };
    }

    private void ConfigurarArticulosActivos(params Articulo[] articulos)
    {
        _articuloRepository
            .FindAsync(Arg.Any<Expression<Func<Articulo, bool>>>(), false, Arg.Any<CancellationToken>())
            .Returns(articulos);
    }

    private void ConfigurarListaExistente(ListaArticulos lista)
    {
        _listaRepository
            .GetByIdWithIncludesAsync(IdLista, Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<ListaArticulos, object>>[]>())
            .Returns(lista);
    }

    private void ConfigurarLecturaDeLaLista(ListaDto lista)
    {
        // Cualquier id: la lista recién creada en un test no tiene el id que le asignaría la base.
        _consultas.ObtenerAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(lista);
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

    private static CrearListaDto CrearDtoDeCreacion()
    {
        return new CrearListaDto
        {
            IdUsuario = IdUsuario,
            Nombre = "Útiles",
            Renglones = [new RenglonListaEntradaDto { IdArticulo = 10, Cantidad = 1 }]
        };
    }

    private static ActualizarListaDto CrearDtoDeActualizacion(DateTime fechaLeida)
    {
        return new ActualizarListaDto
        {
            Nombre = "Útiles",
            FechaModificacionLeida = fechaLeida,
            Renglones = [new RenglonListaEntradaDto { IdArticulo = 10, Cantidad = 1 }]
        };
    }

    private static ListaArticulos CrearListaDeDominio(params (int IdArticulo, int Cantidad)[] renglones)
    {
        var lista = ListaArticulos.Crear("Útiles", IdUsuario, renglones);
        lista.Id = IdLista;
        return lista;
    }

    private static RenglonListaDto Renglon(int id, string descripcion, int cantidad, decimal precioGuardado, bool disponible = true)
    {
        return new RenglonListaDto
        {
            IdArticulo = id,
            Descripcion = descripcion,
            Cantidad = cantidad,
            PrecioActual = precioGuardado,
            Subtotal = precioGuardado * cantidad,
            EstaDisponible = disponible
        };
    }

    private static ListaDto CrearListaDto(params RenglonListaDto[] renglones)
    {
        var items = renglones.Length > 0 ? renglones : [Renglon(10, "Cuaderno", 1, 1500m)];

        return new ListaDto
        {
            IdLista = IdLista,
            Nombre = "Útiles",
            FechaModificacion = DateTime.UtcNow,
            Renglones = items,
            TotalEstimado = items.Where(r => r.EstaDisponible).Sum(r => r.Subtotal)
        };
    }
}

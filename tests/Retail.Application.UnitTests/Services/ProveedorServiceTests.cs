using System.Linq.Expressions;
using FluentAssertions;
using FluentValidation;
using NSubstitute;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Application.Validators.Proveedores;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Application.UnitTests.Services;

public class ProveedorServiceTests
{
    private readonly IRepository<Proveedor> _proveedorRepoMock;
    private readonly ICatalogoProveedorQueryService _catalogoQueryMock;
    private readonly IRepository<Articulo> _articuloRepoMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly IExcelCatalogParser _excelParserMock;
    private readonly ProveedorService _service;

    public ProveedorServiceTests()
    {
        _proveedorRepoMock = Substitute.For<IRepository<Proveedor>>();
        _catalogoQueryMock = Substitute.For<ICatalogoProveedorQueryService>();
        _articuloRepoMock = Substitute.For<IRepository<Articulo>>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _excelParserMock = Substitute.For<IExcelCatalogParser>();

        _service = new ProveedorService(
            _proveedorRepoMock,
            _catalogoQueryMock,
            _articuloRepoMock,
            _unitOfWorkMock,
            _excelParserMock,
            new CrearProveedorValidator(),
            new ActualizarProveedorValidator(),
            new MapeoColumnasValidator(),
            new IncorporarCatalogoArticulosValidator()
        );
    }

    [Fact]
    public async Task CrearProveedorAsync_ConDatosValidos_GuardaProveedorYRetornaDto()
    {
        // Arrange
        var dto = new CrearProveedorDto
        {
            RazonSocial = "Papelera Central",
            Cuit = "30-11223344-5",
            Telefono = "12345678",
            Email = "ventas@central.com"
        };

        _proveedorRepoMock.FindAsync(Arg.Any<Expression<Func<Proveedor, bool>>>(), false, Arg.Any<CancellationToken>())
            .Returns(new List<Proveedor>());

        // Act
        var resultado = await _service.CrearProveedorAsync(dto);

        // Assert
        resultado.Should().NotBeNull();
        resultado.RazonSocial.Should().Be("Papelera Central");
        resultado.Cuit.Should().Be("30-11223344-5");

        await _proveedorRepoMock.Received(1).AddAsync(Arg.Is<Proveedor>(p =>
            p.RazonSocial == "Papelera Central" &&
            p.Cuit == "30-11223344-5"
        ), Arg.Any<CancellationToken>());

        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrearProveedorAsync_ConCuitDuplicado_LanzaDomainException()
    {
        // Arrange
        var dto = new CrearProveedorDto
        {
            RazonSocial = "Papelera Duplicada",
            Cuit = "30-11223344-5"
        };

        _proveedorRepoMock.FindAsync(Arg.Any<Expression<Func<Proveedor, bool>>>(), false, Arg.Any<CancellationToken>())
            .Returns(new List<Proveedor>
            {
                new() { RazonSocial = "Ya Existente", Cuit = "30-11223344-5" }
            });

        // Act
        var act = async () => await _service.CrearProveedorAsync(dto);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*registrado con el CUIT*");
    }

    [Fact]
    public async Task ActualizarProveedorAsync_ConDatosValidos_ActualizaProveedorYGuarda()
    {
        // Arrange
        var proveedorExistente = new Proveedor
        {
            Id = 5,
            RazonSocial = "Nombre Anterior",
            Cuit = "30-99999999-1"
        };

        _proveedorRepoMock.GetByIdAsync(5, false, Arg.Any<CancellationToken>())
            .Returns(proveedorExistente);

        _proveedorRepoMock.FindAsync(Arg.Any<Expression<Func<Proveedor, bool>>>(), false, Arg.Any<CancellationToken>())
            .Returns(new List<Proveedor>());

        var dto = new ProveedorDto
        {
            IdProveedor = 5,
            RazonSocial = "Nombre Modificado",
            Cuit = "30-88888888-2",
            Telefono = "456789",
            Email = "nuevo@email.com"
        };

        // Act
        await _service.ActualizarProveedorAsync(dto);

        // Assert
        proveedorExistente.RazonSocial.Should().Be("Nombre Modificado");
        proveedorExistente.Cuit.Should().Be("30-88888888-2");
        await _proveedorRepoMock.Received(1).UpdateAsync(proveedorExistente, Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EliminarProveedorAsync_ConIdValido_MarcaComoEliminado()
    {
        // Arrange
        var proveedor = new Proveedor
        {
            Id = 3,
            RazonSocial = "Proveedor a Borrar",
            Cuit = "30-33333333-3"
        };

        _proveedorRepoMock.GetByIdAsync(3, false, Arg.Any<CancellationToken>())
            .Returns(proveedor);

        // Act
        await _service.BajaProveedorAsync(3);

        // Assert
        proveedor.IsDeleted.Should().BeTrue();
        proveedor.DeletedAt.Should().NotBeNull();
        await _proveedorRepoMock.Received(1).UpdateAsync(proveedor, Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VincularArticuloACatalogoAsync_ConArticuloYCatalogoExistentes_ActualizaCostoYRecalcula()
    {
        // Arrange
        var articulo = new Articulo
        {
            Id = 10,
            Descripcion = "Marcador Permanente Negro",
            CostoReposicion = 500m,
            PorcentajeGanancia = 40m,
            PrecioVenta = 700m,
            StockActual = 10,
            StockMinimo = 2
        };

        var catalogo = new CatalogoProveedor
        {
            Id = 25,
            IdProveedor = 1,
            CodigoProveedor = "EDD-700",
            DescripcionProveedor = "Edding 700 Negro",
            CostoReposicion = 800m
        };

        _articuloRepoMock.GetByIdAsync(10, false, Arg.Any<CancellationToken>())
            .Returns(articulo);

        _catalogoQueryMock.ObtenerPorIdAsync(25, Arg.Any<CancellationToken>())
            .Returns(catalogo);

        // Act
        await _service.VincularArticuloACatalogoAsync(10, 25);

        // Assert
        articulo.IdCatalogoProveedor.Should().Be(25);
        articulo.CostoReposicion.Should().Be(800m);
        articulo.PrecioVenta.Should().Be(1120m); // 800 * 1.40 = 1120

        await _articuloRepoMock.Received(1).UpdateAsync(articulo, Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListarItemsCatalogoAsync_DelegaPaginacionAlServicioDeConsulta()
    {
        // Arrange
        var paginadoEsperado = new CatalogoPaginadoDto
        {
            Items = new List<CatalogoProveedorDto>
            {
                new()
                {
                    Id = 1,
                    IdProveedor = 2,
                    CodigoProveedor = "A1",
                    DescripcionProveedor = "Prod A1",
                    CostoReposicion = 100m,
                    FechaActualizacion = DateTime.UtcNow
                }
            },
            TotalRegistros = 1,
            PaginaActual = 1,
            TamanoPagina = 50
        };

        var consulta = new ConsultaCatalogoProveedorDto
        {
            IdProveedor = 2,
            TerminoBusqueda = "A1",
            EstadoVinculacion = EstadoVinculacionCatalogoEnum.Todos,
            Pagina = 1,
            TamañoPagina = 50
        };

        _catalogoQueryMock.ObtenerCatalogoPaginadoAsync(consulta, Arg.Any<CancellationToken>())
            .Returns(paginadoEsperado);

        // Act
        var resultado = await _service.ListarItemsCatalogoAsync(consulta);

        // Assert
        resultado.Should().BeEquivalentTo(paginadoEsperado);
        await _catalogoQueryMock.Received(1).ObtenerCatalogoPaginadoAsync(consulta, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IncorporarArticulosATiendaAsync_ConPorcentajesIndividuales_CalculaPrecioCorrectoPorItem()
    {
        // Arrange
        var catalogos = new List<CatalogoProveedor>
        {
            new()
            {
                Id = 1,
                IdProveedor = 5,
                CodigoProveedor = "P1",
                DescripcionProveedor = "Prod 1",
                CostoReposicion = 1000m
            },
            new()
            {
                Id = 2,
                IdProveedor = 5,
                CodigoProveedor = "P2",
                DescripcionProveedor = "Prod 2",
                CostoReposicion = 2000m
            }
        };

        _catalogoQueryMock.ObtenerPorIdsAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(catalogos);

        var dto = new IncorporarCatalogoArticulosDto
        {
            Items = new List<ItemIncorporacionArticuloDto>
            {
                new() { IdCatalogo = 1, PorcentajeGanancia = 50m },
                new() { IdCatalogo = 2, PorcentajeGanancia = 25m }
            },
            PorcentajeGananciaSugerido = 40m
        };

        _articuloRepoMock.FindAsync(Arg.Any<Expression<Func<Articulo, bool>>>(), false, Arg.Any<CancellationToken>())
            .Returns(new List<Articulo>());

        var articulosAgregados = new List<Articulo>();
        await _articuloRepoMock.AddAsync(Arg.Do<Articulo>(a => articulosAgregados.Add(a)), Arg.Any<CancellationToken>());

        // Act
        await _service.IncorporarArticulosATiendaAsync(dto);

        // Assert
        articulosAgregados.Should().HaveCount(2);
        var art1 = articulosAgregados.First(a => a.IdCatalogoProveedor == 1);
        art1.PorcentajeGanancia.Should().Be(50m);
        art1.PrecioVenta.Should().Be(1500m); // 1000 * 1.50 = 1500

        var art2 = articulosAgregados.First(a => a.IdCatalogoProveedor == 2);
        art2.PorcentajeGanancia.Should().Be(25m);
        art2.PrecioVenta.Should().Be(2500m); // 2000 * 1.25 = 2500

        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static ItemCatalogoImportadoDto Fila(string codigo, string descripcion = "Producto", decimal precio = 100m, string? codigoBarras = null)
    {
        return new ItemCatalogoImportadoDto
        {
            CodigoProveedor = codigo,
            Descripcion = descripcion,
            PrecioCosto = precio,
            CodigoBarras = codigoBarras
        };
    }

    private void PrepararImportacion(
        IReadOnlyList<ItemCatalogoImportadoDto> filas,
        Dictionary<string, CatalogoProveedor>? catalogosExistentes = null,
        List<Articulo>? articulosVinculados = null)
    {
        _proveedorRepoMock.GetByIdAsync(1, false, Arg.Any<CancellationToken>())
            .Returns(new Proveedor { Id = 1 });

        _excelParserMock.ParsearCatalogoAsync(Arg.Any<Stream>(), Arg.Any<MapeoColumnasDto>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .Returns(filas);

        _articuloRepoMock.FindAsync(Arg.Any<Expression<Func<Articulo, bool>>>(), false, Arg.Any<CancellationToken>())
            .Returns(articulosVinculados ?? new List<Articulo>());

        _catalogoQueryMock.ObtenerMapaPorCodigosProveedorAsync(1, Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(catalogosExistentes ?? new Dictionary<string, CatalogoProveedor>());
    }

    private static MapeoColumnasDto Mapeo()
    {
        return new MapeoColumnasDto
        {
            IdProveedor = 1,
            ColumnaCodigo = "A",
            ColumnaDescripcion = "B",
            ColumnaPrecioCosto = "C"
        };
    }

    [Fact]
    public async Task ImportarPlanillaProveedorAsync_ConCodigosDuplicadosPorMayusculasOEspacios_ConservaPrimeraYReportaError()
    {
        // Arrange
        PrepararImportacion(new List<ItemCatalogoImportadoDto>
        {
            Fila("ABC-1"),
            Fila("abc-1 "),
            Fila(" ABC-1"),
            Fila("XYZ-9")
        });

        var agregados = new List<CatalogoProveedor>();
        await _catalogoQueryMock.AgregarAsync(Arg.Do<CatalogoProveedor>(c => agregados.Add(c)), Arg.Any<CancellationToken>());

        // Act
        var resultado = await _service.ImportarPlanillaProveedorAsync(new MemoryStream(), Mapeo());

        // Assert
        agregados.Select(c => c.CodigoProveedor).Should().Equal("ABC-1", "XYZ-9");
        resultado.NuevosRegistros.Should().Be(2);
        resultado.FilasConError.Should().Be(2);
        resultado.ErroresDetalle.Should().HaveCount(2);
        resultado.ErroresDetalle[0].Should().Contain("Fila 2").And.Contain("duplicado");
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportarPlanillaProveedorAsync_ConCodigoODescripcionExcedidos_ReportaErrorSinAbortarElResto()
    {
        // Arrange
        PrepararImportacion(new List<ItemCatalogoImportadoDto>
        {
            Fila(new string('C', 51)),
            Fila("OK-1", new string('D', 201)),
            Fila("OK-2", "Descripción válida", 50m, new string('9', 51)),
            Fila("OK-3")
        });

        var agregados = new List<CatalogoProveedor>();
        await _catalogoQueryMock.AgregarAsync(Arg.Do<CatalogoProveedor>(c => agregados.Add(c)), Arg.Any<CancellationToken>());

        // Act
        var resultado = await _service.ImportarPlanillaProveedorAsync(new MemoryStream(), Mapeo());

        // Assert
        agregados.Should().ContainSingle().Which.CodigoProveedor.Should().Be("OK-3");
        resultado.FilasConError.Should().Be(3);
        resultado.ErroresDetalle.Should().Contain(e => e.StartsWith("Fila 1:") && e.Contains("50"));
        resultado.ErroresDetalle.Should().Contain(e => e.StartsWith("Fila 2:") && e.Contains("200"));
        resultado.ErroresDetalle.Should().Contain(e => e.StartsWith("Fila 3:") && e.Contains("código de barras"));
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportarPlanillaProveedorAsync_ConCodigoExistenteEnOtraCapitalizacion_ActualizaSinCrearDuplicado()
    {
        // Arrange
        var existente = new CatalogoProveedor { Id = 7, IdProveedor = 1, CodigoProveedor = "ABC-1", DescripcionProveedor = "Viejo", CostoReposicion = 10m };
        PrepararImportacion(
            new List<ItemCatalogoImportadoDto> { Fila(" abc-1 ", "Nuevo", 99m) },
            new Dictionary<string, CatalogoProveedor> { ["ABC-1"] = existente });

        // Act
        var resultado = await _service.ImportarPlanillaProveedorAsync(new MemoryStream(), Mapeo());

        // Assert
        existente.CostoReposicion.Should().Be(99m);
        resultado.NuevosRegistros.Should().Be(0);
        resultado.FilasConError.Should().Be(0);
        await _catalogoQueryMock.DidNotReceive().AgregarAsync(Arg.Any<CatalogoProveedor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportarPlanillaProveedorAsync_ConDosArticulosVinculadosAlMismoCatalogo_ActualizaAmbosSinFallar()
    {
        // Arrange
        var existente = new CatalogoProveedor { Id = 7, IdProveedor = 1, CodigoProveedor = "ABC-1", DescripcionProveedor = "Viejo", CostoReposicion = 10m };
        var articuloA = new Articulo { Id = 1, Descripcion = "A", IdCatalogoProveedor = 7, CostoReposicion = 10m, PorcentajeGanancia = 50m, PrecioVenta = 15m };
        var articuloB = new Articulo { Id = 2, Descripcion = "B", IdCatalogoProveedor = 7, CostoReposicion = 10m, PorcentajeGanancia = 100m, PrecioVenta = 20m };
        PrepararImportacion(
            new List<ItemCatalogoImportadoDto> { Fila("ABC-1", "Nuevo", 100m) },
            new Dictionary<string, CatalogoProveedor> { ["ABC-1"] = existente },
            new List<Articulo> { articuloA, articuloB });

        // Act
        var resultado = await _service.ImportarPlanillaProveedorAsync(new MemoryStream(), Mapeo());

        // Assert
        resultado.PreciosActualizados.Should().Be(2);
        articuloA.PrecioVenta.Should().Be(150m);
        articuloB.PrecioVenta.Should().Be(200m);
    }

    [Fact]
    public async Task IncorporarArticulosATiendaAsync_ConCatalogoYaVinculado_NoCreaArticuloDuplicado()
    {
        // Arrange
        var catalogos = new List<CatalogoProveedor>
        {
            new() { Id = 1, IdProveedor = 5, CodigoProveedor = "P1", DescripcionProveedor = "Prod 1", CostoReposicion = 1000m },
            new() { Id = 2, IdProveedor = 5, CodigoProveedor = "P2", DescripcionProveedor = "Prod 2", CostoReposicion = 2000m }
        };

        _catalogoQueryMock.ObtenerPorIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(catalogos);

        _articuloRepoMock.FindAsync(Arg.Any<Expression<Func<Articulo, bool>>>(), false, Arg.Any<CancellationToken>())
            .Returns(new List<Articulo> { new() { Id = 99, Descripcion = "Ya existe", IdCatalogoProveedor = 1 } });

        var dto = new IncorporarCatalogoArticulosDto
        {
            Items = new List<ItemIncorporacionArticuloDto>
            {
                new() { IdCatalogo = 1, PorcentajeGanancia = 50m },
                new() { IdCatalogo = 2, PorcentajeGanancia = 25m }
            }
        };

        var agregados = new List<Articulo>();
        await _articuloRepoMock.AddAsync(Arg.Do<Articulo>(a => agregados.Add(a)), Arg.Any<CancellationToken>());

        // Act
        await _service.IncorporarArticulosATiendaAsync(dto);

        // Assert
        agregados.Should().ContainSingle().Which.IdCatalogoProveedor.Should().Be(2);
    }
}

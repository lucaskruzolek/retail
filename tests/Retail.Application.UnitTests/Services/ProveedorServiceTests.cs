using System.Linq.Expressions;
using FluentAssertions;
using FluentValidation;
using NSubstitute;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Application.UnitTests.TestData;
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
            Cuit = "30-11223344-6",
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
        resultado.Cuit.Should().Be("30112233446"); // se guarda sin guiones

        await _proveedorRepoMock.Received(1).AddAsync(Arg.Is<Proveedor>(p =>
            p.RazonSocial == "Papelera Central" &&
            p.Cuit == "30112233446"
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
            Cuit = "30-11223344-6"
        };

        _proveedorRepoMock.FindAsync(Arg.Any<Expression<Func<Proveedor, bool>>>(), false, Arg.Any<CancellationToken>())
            .Returns(new List<Proveedor>
            {
                Proveedor.Crear("Ya Existente", "30-11223344-6")
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
        var proveedorExistente = ProveedoresDePrueba.ConId(5, Proveedor.Crear("Nombre Anterior", "30-99999999-5"));

        _proveedorRepoMock.GetByIdAsync(5, false, Arg.Any<CancellationToken>())
            .Returns(proveedorExistente);

        _proveedorRepoMock.FindAsync(Arg.Any<Expression<Func<Proveedor, bool>>>(), false, Arg.Any<CancellationToken>())
            .Returns(new List<Proveedor>());

        var dto = new ActualizarProveedorDto
        {
            IdProveedor = 5,
            RazonSocial = "Nombre Modificado",
            Cuit = "30-88888888-4",
            Telefono = "4567-8901",
            Email = "nuevo@email.com"
        };

        // Act
        await _service.ActualizarProveedorAsync(dto);

        // Assert
        proveedorExistente.RazonSocial.Should().Be("Nombre Modificado");
        proveedorExistente.Cuit.Should().Be("30888888884");
        proveedorExistente.Telefono.Should().Be("45678901");
        await _proveedorRepoMock.Received(1).UpdateAsync(proveedorExistente, Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EliminarProveedorAsync_ConIdValido_MarcaComoEliminado()
    {
        // Arrange
        var proveedor = ProveedoresDePrueba.ConId(3, Proveedor.Crear("Proveedor a Borrar", "33-33333333-9"));

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
            NumeroFila = 0, // PrepararImportacion asigna el número de fila real
            CodigoProveedor = codigo,
            Descripcion = descripcion,
            PrecioCosto = precio,
            CodigoBarras = codigoBarras
        };
    }

    /// <summary>
    /// Simula lo que entrega el parser para una planilla con el encabezado en la fila 1:
    /// la primera fila de datos es la 2, la segunda la 3, y así sucesivamente.
    /// </summary>
    private void PrepararImportacion(
        IReadOnlyList<ItemCatalogoImportadoDto> filas,
        Dictionary<string, CatalogoProveedor>? catalogosExistentes = null,
        List<Articulo>? articulosVinculados = null,
        IReadOnlyList<string>? filasDescartadasPorParser = null)
    {
        _proveedorRepoMock.GetByIdAsync(1, false, Arg.Any<CancellationToken>())
            .Returns(ProveedoresDePrueba.ConId(1, Proveedor.Crear("Distribuidora Mayorista", "30712345671")));

        var parseo = new ResultadoParseoCatalogoDto
        {
            Items = filas.Select((fila, indice) => fila with { NumeroFila = indice + 2 }).ToList(),
            FilasDescartadas = filasDescartadasPorParser ?? Array.Empty<string>()
        };

        _excelParserMock.ParsearCatalogoAsync(Arg.Any<Stream>(), Arg.Any<MapeoColumnasDto>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .Returns(parseo);

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
        resultado.ErroresDetalle[0].Should().Contain("Fila 3").And.Contain("duplicado");
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
        resultado.ErroresDetalle.Should().Contain(e => e.StartsWith("Fila 2:") && e.Contains("50"));
        resultado.ErroresDetalle.Should().Contain(e => e.StartsWith("Fila 3:") && e.Contains("200"));
        resultado.ErroresDetalle.Should().Contain(e => e.StartsWith("Fila 4:") && e.Contains("código de barras"));
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportarPlanillaProveedorAsync_CanceladaAMitadDelLote_DescartaLosCambiosYNoGuarda()
    {
        // Arrange: el usuario cancela mientras se procesa la fila 2; las filas 1 y 2 ya quedaron en el contexto
        PrepararImportacion(new List<ItemCatalogoImportadoDto> { Fila("A-1"), Fila("A-2"), Fila("A-3") });

        using var cancelacion = new CancellationTokenSource();
        int agregados = 0;
        await _catalogoQueryMock.AgregarAsync(
            Arg.Do<CatalogoProveedor>(_ =>
            {
                agregados++;
                if (agregados == 2)
                {
                    cancelacion.Cancel();
                }
            }),
            Arg.Any<CancellationToken>());

        // Act
        var act = () => _service.ImportarPlanillaProveedorAsync(new MemoryStream(), Mapeo(), cancellationToken: cancelacion.Token);

        // Assert: sin el descarte, media planilla se guardaría con la próxima operación de la pantalla
        await act.Should().ThrowAsync<OperationCanceledException>();
        agregados.Should().Be(2);
        _unitOfWorkMock.Received(1).DescartarCambios();
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
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
        var resultado = await _service.IncorporarArticulosATiendaAsync(dto);

        // Assert
        agregados.Should().ContainSingle().Which.IdCatalogoProveedor.Should().Be(2);
        resultado.Incorporados.Should().Be(1);
        resultado.OmitidosYaVinculados.Should().Be(1);
    }

    [Fact]
    public async Task ImportarPlanillaProveedorAsync_ConFilasDescartadasPorElParser_LasIncluyeEnErroresDetalle()
    {
        // Arrange
        PrepararImportacion(
            new List<ItemCatalogoImportadoDto> { Fila("OK-1") },
            filasDescartadasPorParser: new List<string> { "Fila 7: el precio 'abc' no es un importe válido." });

        // Act
        var resultado = await _service.ImportarPlanillaProveedorAsync(new MemoryStream(), Mapeo());

        // Assert
        resultado.NuevosRegistros.Should().Be(1);
        resultado.FilasConError.Should().Be(1);
        resultado.ErroresDetalle.Should().ContainSingle().Which.Should().StartWith("Fila 7:");
    }

    [Fact]
    public async Task ImportarPlanillaProveedorAsync_ConCostoIgualAlDelArticulo_NoCuentaPrecioActualizadoNiLoModifica()
    {
        // Arrange: el artículo ya tiene el costo que trae la planilla
        var existente = new CatalogoProveedor { Id = 7, IdProveedor = 1, CodigoProveedor = "ABC-1", DescripcionProveedor = "Viejo", CostoReposicion = 100m };
        var articulo = new Articulo { Id = 1, Descripcion = "A", IdCatalogoProveedor = 7, CostoReposicion = 100m, PorcentajeGanancia = 50m, PrecioVenta = 150m };
        PrepararImportacion(
            new List<ItemCatalogoImportadoDto> { Fila("ABC-1", "Nuevo", 100m) },
            new Dictionary<string, CatalogoProveedor> { ["ABC-1"] = existente },
            new List<Articulo> { articulo });

        // Act
        var resultado = await _service.ImportarPlanillaProveedorAsync(new MemoryStream(), Mapeo());

        // Assert
        resultado.PreciosActualizados.Should().Be(0);
        articulo.PrecioVenta.Should().Be(150m);
        await _articuloRepoMock.DidNotReceive().UpdateAsync(Arg.Any<Articulo>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportarPlanillaProveedorAsync_ConCostoDistintoSoloEnUnArticulo_CuentaSoloEseArticulo()
    {
        // Arrange: el artículo B fue editado a mano y ya tiene el costo nuevo; el A no
        var existente = new CatalogoProveedor { Id = 7, IdProveedor = 1, CodigoProveedor = "ABC-1", DescripcionProveedor = "Viejo", CostoReposicion = 10m };
        var articuloA = new Articulo { Id = 1, Descripcion = "A", IdCatalogoProveedor = 7, CostoReposicion = 10m, PorcentajeGanancia = 50m, PrecioVenta = 15m };
        var articuloB = new Articulo { Id = 2, Descripcion = "B", IdCatalogoProveedor = 7, CostoReposicion = 100m, PorcentajeGanancia = 50m, PrecioVenta = 150m };
        PrepararImportacion(
            new List<ItemCatalogoImportadoDto> { Fila("ABC-1", "Nuevo", 100m) },
            new Dictionary<string, CatalogoProveedor> { ["ABC-1"] = existente },
            new List<Articulo> { articuloA, articuloB });

        // Act
        var resultado = await _service.ImportarPlanillaProveedorAsync(new MemoryStream(), Mapeo());

        // Assert
        resultado.PreciosActualizados.Should().Be(1);
        articuloA.PrecioVenta.Should().Be(150m);
        await _articuloRepoMock.Received(1).UpdateAsync(articuloA, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Configura el repositorio para que evalúe en memoria el predicado que recibe. Así el test verifica
    /// la condición del filtro (no solo que se llamó a FindAsync), aunque no su traducción a SQL.
    /// </summary>
    private void PrepararArticulosParaVincular(Articulo articulo, CatalogoProveedor catalogo, List<Articulo> articulosEnTienda)
    {
        _articuloRepoMock.GetByIdAsync(articulo.Id, false, Arg.Any<CancellationToken>())
            .Returns(articulo);

        _catalogoQueryMock.ObtenerPorIdAsync(catalogo.Id, Arg.Any<CancellationToken>())
            .Returns(catalogo);

        _articuloRepoMock.FindAsync(Arg.Any<Expression<Func<Articulo, bool>>>(), false, Arg.Any<CancellationToken>())
            .Returns(llamada => articulosEnTienda
                .Where(llamada.Arg<Expression<Func<Articulo, bool>>>().Compile())
                .ToList());
    }

    [Fact]
    public async Task VincularArticuloACatalogoAsync_ConCatalogoVinculadoAOtroArticulo_LanzaDomainExceptionSinGuardar()
    {
        // Arrange
        var catalogo = new CatalogoProveedor { Id = 25, IdProveedor = 1, CodigoProveedor = "EDD-700", DescripcionProveedor = "Edding 700", CostoReposicion = 800m };
        var duenio = new Articulo { Id = 5, Descripcion = "Marcador Edding", IdCatalogoProveedor = 25 };
        var articulo = new Articulo { Id = 10, Descripcion = "Marcador Permanente Negro", CostoReposicion = 500m, PorcentajeGanancia = 40m };
        PrepararArticulosParaVincular(articulo, catalogo, new List<Articulo> { duenio, articulo });

        // Act
        var accion = () => _service.VincularArticuloACatalogoAsync(10, 25);

        // Assert
        await accion.Should().ThrowAsync<DomainException>()
            .WithMessage("*EDD-700*Marcador Edding*");
        articulo.IdCatalogoProveedor.Should().BeNull();
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VincularArticuloACatalogoAsync_RevinculandoElMismoArticulo_ActualizaSinError()
    {
        // Arrange: el único artículo vinculado al ítem es el mismo que se revincula
        var catalogo = new CatalogoProveedor { Id = 25, IdProveedor = 1, CodigoProveedor = "EDD-700", DescripcionProveedor = "Edding 700", CostoReposicion = 800m };
        var articulo = new Articulo { Id = 10, Descripcion = "Marcador Permanente Negro", IdCatalogoProveedor = 25, CostoReposicion = 500m, PorcentajeGanancia = 40m };
        PrepararArticulosParaVincular(articulo, catalogo, new List<Articulo> { articulo });

        // Act
        await _service.VincularArticuloACatalogoAsync(10, 25);

        // Assert
        articulo.CostoReposicion.Should().Be(800m);
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---------- Propagación a presentaciones derivadas (RF-21) ----------

    [Fact]
    public async Task ImportarPlanillaProveedorAsync_CambiaCostoDelOrigen_PropagaAlDerivadoYLoCuenta()
    {
        // Arrange
        var (catalogo, origen, derivado) = PrepararPackConPresentacion(costo: 1000m, unidadesPorOrigen: 100);
        PrepararImportacion(
            new List<ItemCatalogoImportadoDto> { Fila("SOB-100", "Sobre manila x100", 2000m) },
            new Dictionary<string, CatalogoProveedor> { ["SOB-100"] = catalogo });
        EvaluarPredicadosSobre(origen, derivado);

        // Act
        var resultado = await _service.ImportarPlanillaProveedorAsync(new MemoryStream(), Mapeo());

        // Assert
        origen.CostoReposicion.Should().Be(2000m);
        derivado.CostoReposicion.Should().Be(20m);
        derivado.PrecioVenta.Should().Be(30m);
        resultado.PreciosActualizados.Should().Be(2);
        await _articuloRepoMock.Received(1).UpdateAsync(derivado, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportarPlanillaProveedorAsync_CostoDelOrigenSinCambios_NoTocaDerivados()
    {
        // Arrange: el costo del derivado se editó a mano y se respeta mientras el origen no cambie
        var (catalogo, origen, derivado) = PrepararPackConPresentacion(costo: 1000m, unidadesPorOrigen: 100);
        derivado.ActualizarCostoYRecalcularPrecio(12m);
        PrepararImportacion(
            new List<ItemCatalogoImportadoDto> { Fila("SOB-100", "Sobre manila x100", 1000m) },
            new Dictionary<string, CatalogoProveedor> { ["SOB-100"] = catalogo });
        EvaluarPredicadosSobre(origen, derivado);

        // Act
        var resultado = await _service.ImportarPlanillaProveedorAsync(new MemoryStream(), Mapeo());

        // Assert
        derivado.CostoReposicion.Should().Be(12m);
        resultado.PreciosActualizados.Should().Be(0);
        await _articuloRepoMock.DidNotReceive().UpdateAsync(Arg.Any<Articulo>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportarPlanillaProveedorAsync_CostoDelDerivadoIgualTrasRedondear_CuentaSoloElOrigen()
    {
        // Arrange: 45 / 1000 y 45,20 / 1000 redondean a 0,05
        var (catalogo, origen, derivado) = PrepararPackConPresentacion(costo: 45m, unidadesPorOrigen: 1000);
        PrepararImportacion(
            new List<ItemCatalogoImportadoDto> { Fila("SOB-100", "Sobre manila x1000", 45.20m) },
            new Dictionary<string, CatalogoProveedor> { ["SOB-100"] = catalogo });
        EvaluarPredicadosSobre(origen, derivado);

        // Act
        var resultado = await _service.ImportarPlanillaProveedorAsync(new MemoryStream(), Mapeo());

        // Assert
        derivado.CostoReposicion.Should().Be(0.05m);
        resultado.PreciosActualizados.Should().Be(1);
        await _articuloRepoMock.DidNotReceive().UpdateAsync(derivado, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VincularArticuloACatalogoAsync_OrigenConPresentaciones_PropagaElCosto()
    {
        // Arrange
        var catalogo = new CatalogoProveedor { Id = 25, IdProveedor = 1, CodigoProveedor = "EDD-700", DescripcionProveedor = "Edding 700 x10", CostoReposicion = 800m };
        var articulo = new Articulo { Id = 10, Descripcion = "Marcador Edding (caja x10)", CostoReposicion = 500m, PorcentajeGanancia = 40m };
        var unidad = new Articulo { Id = 11, Descripcion = "Marcador Edding (unidad)", PorcentajeGanancia = 50m };
        unidad.DefinirComoPresentacionDe(articulo, 10);
        PrepararArticulosParaVincular(articulo, catalogo, new List<Articulo> { articulo, unidad });

        // Act
        await _service.VincularArticuloACatalogoAsync(10, 25);

        // Assert
        unidad.CostoReposicion.Should().Be(80m);
        unidad.PrecioVenta.Should().Be(120m);
        await _articuloRepoMock.Received(1).UpdateAsync(unidad, Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Un artículo de compra (pack) vinculado al renglón "SOB-100" del proveedor 1, con una presentación derivada.
    /// Las navegaciones quedan cargadas para que los predicados de la importación se puedan evaluar en memoria.
    /// </summary>
    private static (CatalogoProveedor Catalogo, Articulo Origen, Articulo Derivado) PrepararPackConPresentacion(
        decimal costo,
        int unidadesPorOrigen)
    {
        var catalogo = new CatalogoProveedor { Id = 7, IdProveedor = 1, CodigoProveedor = "SOB-100", DescripcionProveedor = "Sobre manila", CostoReposicion = costo };
        var origen = new Articulo
        {
            Id = 1,
            Descripcion = "Sobre manila (pack)",
            IdCatalogoProveedor = 7,
            CatalogoProveedor = catalogo,
            CostoReposicion = costo,
            PorcentajeGanancia = 40m
        };
        var derivado = new Articulo { Id = 2, Descripcion = "Sobre manila (unidad)", PorcentajeGanancia = 50m };
        derivado.DefinirComoPresentacionDe(origen, unidadesPorOrigen);

        return (catalogo, origen, derivado);
    }

    /// <summary>
    /// Reemplaza la respuesta fija de <c>FindAsync</c> por la evaluación en memoria del predicado recibido.
    /// </summary>
    private void EvaluarPredicadosSobre(params Articulo[] articulos)
    {
        _articuloRepoMock.FindAsync(Arg.Any<Expression<Func<Articulo, bool>>>(), false, Arg.Any<CancellationToken>())
            .Returns(llamada => articulos
                .Where(llamada.Arg<Expression<Func<Articulo, bool>>>().Compile())
                .ToList());
    }

    [Fact]
    public async Task CrearProveedorAsync_CuitConGuionesYaRegistradoSinGuiones_LoDetectaComoDuplicado()
    {
        // Arrange: regresión de P-1. El proveedor existente se guardó como "20123456786" y el nuevo se escribe con
        // guiones; antes la búsqueda probaba "20123456786" y "20-12345678-6" contra un valor guardado crudo y,
        // según cómo se hubiera escrito el primero, no lo encontraba.
        var dto = new CrearProveedorDto { RazonSocial = "Distribuidora Sur", Cuit = "20-12345678-6" };
        var yaRegistrado = Proveedor.Crear("Distribuidora Sur", "20123456786");

        Expression<Func<Proveedor, bool>>? filtro = null;
        _proveedorRepoMock.FindAsync(Arg.Do<Expression<Func<Proveedor, bool>>>(f => filtro = f), false, Arg.Any<CancellationToken>())
            .Returns(callInfo => filtro!.Compile()(yaRegistrado) ? new List<Proveedor> { yaRegistrado } : new List<Proveedor>());

        // Act
        var act = async () => await _service.CrearProveedorAsync(dto);

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*20123456786*");
        await _proveedorRepoMock.DidNotReceive().AddAsync(Arg.Any<Proveedor>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("30-11223344-5")]
    [InlineData("11111111111")]
    public async Task CrearProveedorAsync_CuitConDigitoVerificadorIncorrecto_LanzaValidationException(string cuit)
    {
        // Act
        var act = async () => await _service.CrearProveedorAsync(new CrearProveedorDto { RazonSocial = "Papelera Central", Cuit = cuit });

        // Assert
        await act.Should().ThrowAsync<FluentValidation.ValidationException>().WithMessage("*CUIT*");
    }
}

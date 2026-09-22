using FluentAssertions;
using NSubstitute;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Application.Validators.Proveedores;
using Retail.Domain.Entities;
using Xunit;

namespace Retail.Application.UnitTests.Services;

public class ImportadorHandlerTests
{
    private readonly IRepository<Proveedor> _proveedorRepoMock;
    private readonly ICatalogoProveedorQueryService _catalogoQueryServiceMock;
    private readonly IRepository<Articulo> _articuloRepoMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly IExcelCatalogParser _excelParserMock;
    private readonly ProveedorService _service;

    public ImportadorHandlerTests()
    {
        _proveedorRepoMock = Substitute.For<IRepository<Proveedor>>();
        _catalogoQueryServiceMock = Substitute.For<ICatalogoProveedorQueryService>();
        _articuloRepoMock = Substitute.For<IRepository<Articulo>>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _excelParserMock = Substitute.For<IExcelCatalogParser>();

        _service = new ProveedorService(
            _proveedorRepoMock,
            _catalogoQueryServiceMock,
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
    public async Task IncorporarArticulosATiendaAsync_ConIdsValidos_CreaNuevosArticulosCorrectamente()
    {
        // Arrange
        var idCatalogo = 10;
        var idCategoria = 2;
        var idMarca = 3;
        var costoReposicion = 1000m;
        var gananciaSugerida = 50m; // 50% -> Precio venta esperado: 1500

        var catalogoItem = new CatalogoProveedor
        {
            Id = idCatalogo,
            IdProveedor = 1,
            CodigoProveedor = "PROD-01",
            DescripcionProveedor = "Artículo Mayorista de Prueba",
            CostoReposicion = costoReposicion
        };

        _catalogoQueryServiceMock
            .ObtenerPorIdsAsync(Arg.Is<IEnumerable<int>>(ids => ids.Contains(idCatalogo)), Arg.Any<CancellationToken>())
            .Returns(new List<CatalogoProveedor> { catalogoItem });

        var dto = new IncorporarCatalogoArticulosDto
        {
            IdsCatalogo = new List<int> { idCatalogo },
            IdCategoria = idCategoria,
            IdMarca = idMarca,
            PorcentajeGananciaSugerido = gananciaSugerida
        };

        // Act
        await _service.IncorporarArticulosATiendaAsync(dto);

        // Assert
        await _articuloRepoMock.Received(1).AddAsync(Arg.Is<Articulo>(a =>
            a.Descripcion == "Artículo Mayorista de Prueba" &&
            a.IdCategoria == idCategoria &&
            a.IdMarca == idMarca &&
            a.CostoReposicion == 1000m &&
            a.PorcentajeGanancia == 50m &&
            a.PrecioVenta == 1500m &&
            a.IdCatalogoProveedor == idCatalogo
        ), Arg.Any<CancellationToken>());

        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

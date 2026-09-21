using Moq;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Domain.Entities;
using Xunit;

namespace Retail.Application.UnitTests.Services;

public class ImportadorHandlerTests
{
    private readonly Mock<IRepository<Proveedor>> _proveedorRepoMock;
    private readonly Mock<IRepository<CatalogoProveedor>> _catalogoRepoMock;
    private readonly Mock<IRepository<Articulo>> _articuloRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly ProveedorService _service;

    public ImportadorHandlerTests()
    {
        _proveedorRepoMock = new Mock<IRepository<Proveedor>>();
        _catalogoRepoMock = new Mock<IRepository<CatalogoProveedor>>();
        _articuloRepoMock = new Mock<IRepository<Articulo>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _service = new ProveedorService(
            _proveedorRepoMock.Object,
            _catalogoRepoMock.Object,
            _articuloRepoMock.Object,
            _unitOfWorkMock.Object
        );
    }

    [Fact]
    public async Task IncorporarArticulosATiendaAsync_CreaNuevosArticulosCorrectamente()
    {
        // Arrange
        var idCatalogo = 10;
        var idCategoria = 2;
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

        _catalogoRepoMock
            .Setup(r => r.GetByIdAsync(idCatalogo, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(catalogoItem);

        _articuloRepoMock
            .Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Articulo, bool>>>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Articulo>()); // No existe aún en tienda

        var dto = new IncorporarCatalogoArticulosDto
        {
            IdsCatalogo = new List<int> { idCatalogo },
            IdCategoria = idCategoria,
            PorcentajeGananciaSugerido = gananciaSugerida
        };

        // Act
        await _service.IncorporarArticulosATiendaAsync(dto);

        // Assert
        _articuloRepoMock.Verify(r => r.AddAsync(It.Is<Articulo>(a =>
            a.Descripcion == "Artículo Mayorista de Prueba" &&
            a.CostoReposicion == 1000m &&
            a.PorcentajeGanancia == 50m &&
            a.PrecioVenta == 1500m &&
            a.IdCatalogoProveedor == idCatalogo
        ), It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
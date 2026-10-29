using FluentAssertions;
using Retail.App.ViewModels.Articulos;
using Retail.Application.DTOs.Articulos;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class ArticuloFilaTests
{
    private static ArticuloDto CrearArticulo(int? idCatalogoProveedor = null, bool esServicio = false, int? idArticuloOrigen = null)
    {
        return new ArticuloDto
        {
            IdArticulo = 1,
            Descripcion = "Cuaderno A4",
            IdCatalogoProveedor = idCatalogoProveedor,
            IdArticuloOrigen = idArticuloOrigen,
            UnidadesPorOrigen = idArticuloOrigen is null ? null : 12,
            CostoReposicion = 100m,
            PorcentajeGanancia = 40m,
            PrecioVenta = 140m,
            StockActual = 3,
            StockMinimo = 5,
            EsServicio = esServicio
        };
    }

    [Fact]
    public void Constructor_ArticuloVinculadoAProveedor_SePuedePedir()
    {
        var sut = new ArticuloFila(CrearArticulo(idCatalogoProveedor: 40));

        sut.SePuedePedir.Should().BeTrue();
        sut.MotivoNoSePuedePedir.Should().BeNull();
        sut.EstaSeleccionado.Should().BeFalse();
    }

    [Fact]
    public void Constructor_SinProveedorVinculado_NoSePuedePedirConSuMotivo()
    {
        var sut = new ArticuloFila(CrearArticulo());

        sut.SePuedePedir.Should().BeFalse();
        sut.MotivoNoSePuedePedir.Should().Be("no tiene proveedor vinculado");
    }

    [Fact]
    public void Constructor_PresentacionDerivada_NombraQueSePideSuOrigen()
    {
        var sut = new ArticuloFila(CrearArticulo(idArticuloOrigen: 9));

        sut.SePuedePedir.Should().BeFalse();
        sut.MotivoNoSePuedePedir.Should().Be("es una presentación derivada: se pide su artículo de origen");
    }

    [Fact]
    public void Constructor_ServicioAunqueTengaProveedor_NoSePuedePedir()
    {
        var sut = new ArticuloFila(CrearArticulo(idCatalogoProveedor: 40, esServicio: true));

        sut.SePuedePedir.Should().BeFalse();
        sut.MotivoNoSePuedePedir.Should().Be("es un servicio: no se pide a proveedores");
    }
}

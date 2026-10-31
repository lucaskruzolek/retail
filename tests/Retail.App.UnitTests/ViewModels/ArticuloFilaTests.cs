using FluentAssertions;
using Retail.App.ViewModels.Articulos;
using Retail.Application.DTOs.Articulos;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class ArticuloFilaTests
{
    private static ArticuloDto CrearArticulo(
        int? idCatalogoProveedor = null,
        bool esServicio = false,
        int? idArticuloOrigen = null,
        int? idFuentePedido = null)
    {
        return new ArticuloDto
        {
            IdArticulo = 1,
            Descripcion = "Cuaderno A4",
            IdCatalogoProveedor = idCatalogoProveedor,
            IdFuentePedido = idFuentePedido,
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
    public void Constructor_SinFuentes_NoSePuedePedirConSuMotivo()
    {
        var sut = new ArticuloFila(CrearArticulo());

        sut.SePuedePedir.Should().BeFalse();
        sut.MotivoNoSePuedePedir.Should().Be("no tiene fuentes");
        sut.IdFuenteParaPedir.Should().BeNull();
    }

    [Fact]
    public void Constructor_SinPrincipalConFuentes_SePidePorLaFuenteQueEligioLaConsulta()
    {
        // Track 4.3, L4, paso 3c: la consulta elige la más barata; la fila no la convierte en principal
        var sut = new ArticuloFila(CrearArticulo(idFuentePedido: 26));

        sut.SePuedePedir.Should().BeTrue();
        sut.MotivoNoSePuedePedir.Should().BeNull();
        sut.IdFuenteParaPedir.Should().Be(26);
        sut.Articulo.EstaVinculadoAProveedor.Should().BeFalse();
    }

    [Fact]
    public void IdFuenteParaPedir_ConPrincipalSinDatoDeLaConsulta_UsaLaPrincipal()
    {
        var sut = new ArticuloFila(CrearArticulo(idCatalogoProveedor: 40));

        sut.IdFuenteParaPedir.Should().Be(40);
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

    // Indicador "+n" de fuentes adicionales (Track 4.3, A-19): CantidadFuentes cuenta todas, principal incluida.
    private static ArticuloFila CrearFilaConFuentes(int cantidadFuentes, int? idPrincipal)
    {
        return new ArticuloFila(new ArticuloDto
        {
            IdArticulo = 1,
            Descripcion = "Cuaderno",
            CostoReposicion = 100m,
            PorcentajeGanancia = 40m,
            PrecioVenta = 140m,
            StockActual = 3,
            StockMinimo = 5,
            EsServicio = false,
            IdCatalogoProveedor = idPrincipal,
            CantidadFuentes = cantidadFuentes
        });
    }

    [Fact]
    public void FuentesAdicionales_PrincipalYDosMas_MuestraMasDos()
    {
        var sut = CrearFilaConFuentes(cantidadFuentes: 3, idPrincipal: 10);

        sut.FuentesAdicionales.Should().Be(2);
        sut.TieneFuentesAdicionales.Should().BeTrue();
        sut.TextoFuentesAdicionales.Should().Be("+2");
        sut.TieneFuentesSinPrincipal.Should().BeFalse();
    }

    [Fact]
    public void FuentesAdicionales_SoloLaPrincipal_NoHayAdicionales()
    {
        var sut = CrearFilaConFuentes(cantidadFuentes: 1, idPrincipal: 10);

        sut.FuentesAdicionales.Should().Be(0);
        sut.TieneFuentesAdicionales.Should().BeFalse();
        sut.TieneFuentesSinPrincipal.Should().BeFalse();
    }

    [Fact]
    public void FuentesAdicionales_SinPrincipalConDosFuentes_TodasSonAdicionalesYFaltaLaPrincipal()
    {
        var sut = CrearFilaConFuentes(cantidadFuentes: 2, idPrincipal: null);

        sut.TieneFuentesSinPrincipal.Should().BeTrue();
        sut.FuentesAdicionales.Should().Be(2);
        sut.TextoFuentesAdicionales.Should().Be("+2");
    }

    [Fact]
    public void FuentesAdicionales_SinFuentes_NoMuestraNada()
    {
        var sut = CrearFilaConFuentes(cantidadFuentes: 0, idPrincipal: null);

        sut.FuentesAdicionales.Should().Be(0);
        sut.TieneFuentesAdicionales.Should().BeFalse();
        sut.TieneFuentesSinPrincipal.Should().BeFalse();
    }

    [Fact]
    public void FuentesAdicionales_PrincipalSinCantidadInformada_NuncaEsNegativa()
    {
        var sut = CrearFilaConFuentes(cantidadFuentes: 0, idPrincipal: 10);

        sut.FuentesAdicionales.Should().Be(0);
    }

    [Theory]
    [InlineData(2, "1 fuente adicional. Abrir el artículo para verlas o cambiar la principal.")]
    [InlineData(3, "2 fuentes adicionales. Abrir el artículo para verlas o cambiar la principal.")]
    public void TooltipFuentes_SegunLaCantidad_UsaSingularOPlural(int cantidadFuentes, string esperado)
    {
        var sut = CrearFilaConFuentes(cantidadFuentes, idPrincipal: 10);

        sut.TooltipFuentes.Should().Be(esperado);
    }
}

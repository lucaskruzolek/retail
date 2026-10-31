using FluentAssertions;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

/// <summary>
/// Pruebas unitarias para la entidad interna de catálogo de proveedor.
/// </summary>
public class CatalogoProveedorDomainTests
{
    [Fact]
    public void ActualizarPrecio_ConPrecioValido_ActualizaCostoYFechaActualizacionUtc()
    {
        // Arrange
        var fechaInicial = DateTime.UtcNow.AddDays(-5);
        var item = new CatalogoProveedor
        {
            IdProveedor = 1,
            CodigoProveedor = "COD-001",
            DescripcionProveedor = "Cuaderno Rivadavia ABC",
            CostoReposicion = 2500m,
            FechaActualizacion = fechaInicial
        };

        var fechaAntes = DateTime.UtcNow.AddSeconds(-1);

        // Act
        item.ActualizarPrecio(3200.50m, "Cuaderno Rivadavia Tapa Dura", "7791234567890");

        // Assert
        item.CostoReposicion.Should().Be(3200.50m);
        item.DescripcionProveedor.Should().Be("Cuaderno Rivadavia Tapa Dura");
        item.CodigoBarras.Should().Be("7791234567890");
        item.FechaActualizacion.Should().BeOnOrAfter(fechaAntes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(-0.01)]
    public void ActualizarPrecio_ConPrecioMenorOIgualACero_LanzaArgumentOutOfRangeException(decimal precioInvalido)
    {
        // Arrange
        var item = new CatalogoProveedor
        {
            IdProveedor = 1,
            CodigoProveedor = "COD-001",
            DescripcionProveedor = "Resma A4",
            CostoReposicion = 4500m
        };

        // Act
        var act = () => item.ActualizarPrecio(precioInvalido);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*nuevoCosto*");
    }

    [Fact]
    public void ActualizarPrecio_ConDescripcionNulaOEnBlanco_ConservaDescripcionOriginal()
    {
        // Arrange
        var item = new CatalogoProveedor
        {
            IdProveedor = 1,
            CodigoProveedor = "COD-001",
            DescripcionProveedor = "Lapicera BIC Azul",
            CostoReposicion = 100m,
            CodigoBarras = "123456"
        };

        // Act
        item.ActualizarPrecio(150m, "   ", null);

        // Assert
        item.CostoReposicion.Should().Be(150m);
        item.DescripcionProveedor.Should().Be("Lapicera BIC Azul");
        item.CodigoBarras.Should().Be("123456");
    }

    [Fact]
    public void ActualizarPrecio_ConTextosDeClasificacion_LosGuardaComoVienenConEspaciosNormalizados()
    {
        var item = CrearItem();

        item.ActualizarPrecio(150m, rubroProveedor: " Escolar ", categoriaProveedor: "Cuadernos  T/D", marcaProveedor: "Rivadavia");

        item.RubroProveedor.Should().Be("Escolar");
        item.CategoriaProveedor.Should().Be("Cuadernos T/D");
        item.MarcaProveedor.Should().Be("Rivadavia");
    }

    [Fact]
    public void ActualizarPrecio_SinTextosDeClasificacion_ConservaLosAnteriores()
    {
        var item = CrearItem();
        item.ActualizarPrecio(150m, rubroProveedor: "Escolar", categoriaProveedor: "Cuadernos", marcaProveedor: "Rivadavia");

        item.ActualizarPrecio(200m, rubroProveedor: "  ", categoriaProveedor: null, marcaProveedor: "");

        item.CostoReposicion.Should().Be(200m);
        item.RubroProveedor.Should().Be("Escolar");
        item.CategoriaProveedor.Should().Be("Cuadernos");
        item.MarcaProveedor.Should().Be("Rivadavia");
    }

    [Fact]
    public void ActualizarPrecio_TextoDeClasificacionDemasiadoLargo_LanzaDomainExceptionSinModificarNada()
    {
        var item = CrearItem();
        var fechaAnterior = item.FechaActualizacion;

        var act = () => item.ActualizarPrecio(
            999m,
            "Descripción nueva",
            "7790000000001",
            categoriaProveedor: new string('A', 101));

        act.Should().Throw<DomainException>();
        item.CostoReposicion.Should().Be(100m);
        item.DescripcionProveedor.Should().Be("Resma A4");
        item.CodigoBarras.Should().BeNull();
        item.CategoriaProveedor.Should().BeNull();
        item.FechaActualizacion.Should().Be(fechaAnterior);
    }

    [Fact]
    public void ActualizarClasificacionDelProveedor_ItemNuevo_RegistraLosTextosSinTocarElPrecio()
    {
        var item = CrearItem();

        item.ActualizarClasificacionDelProveedor("Escolar", null, " Rivadavia ");

        item.RubroProveedor.Should().Be("Escolar");
        item.CategoriaProveedor.Should().BeNull();
        item.MarcaProveedor.Should().Be("Rivadavia");
        item.CostoReposicion.Should().Be(100m);
    }

    [Fact]
    public void ActualizarClasificacionDelProveedor_UnTextoDemasiadoLargo_LanzaDomainExceptionSinAsignarLosOtros()
    {
        var item = CrearItem();

        var act = () => item.ActualizarClasificacionDelProveedor("Escolar", "Cuadernos", new string('A', 101));

        act.Should().Throw<DomainException>();
        item.RubroProveedor.Should().BeNull();
        item.CategoriaProveedor.Should().BeNull();
    }

    // ---------- Fuentes múltiples (Track 4.3, A-1, A-16, J1) ----------

    [Fact]
    public void AsignarArticulo_ItemLibre_LoRegistraComoFuenteDelArticulo()
    {
        var item = CrearItem();
        var articulo = new Articulo { Id = 7, Descripcion = "Resma A4" };

        item.AsignarArticulo(articulo);

        item.IdArticulo.Should().Be(7);
        item.EstaVinculado.Should().BeTrue();
        item.EsFuenteDe(articulo).Should().BeTrue();
    }

    [Fact]
    public void AsignarArticulo_ArticuloNuevoSinId_LoEnlazaPorLaNavegacion()
    {
        var item = CrearItem();
        var articulo = new Articulo { Descripcion = "Resma A4" };

        item.AsignarArticulo(articulo);

        item.IdArticulo.Should().BeNull();
        item.Articulo.Should().BeSameAs(articulo);
        item.EstaVinculado.Should().BeTrue();
        item.EsFuenteDe(articulo).Should().BeTrue();
    }

    [Fact]
    public void AsignarArticulo_MismoArticulo_NoCambiaNada()
    {
        var item = CrearItem();
        var articulo = new Articulo { Id = 7, Descripcion = "Resma A4" };
        item.AsignarArticulo(articulo);

        var act = () => item.AsignarArticulo(articulo);

        act.Should().NotThrow();
        item.IdArticulo.Should().Be(7);
    }

    [Fact]
    public void AsignarArticulo_ItemFuenteDeOtroArticulo_LanzaDomainExceptionYConservaElVinculo()
    {
        var item = CrearItem();
        item.AsignarArticulo(new Articulo { Id = 7, Descripcion = "Resma A4" });

        var act = () => item.AsignarArticulo(new Articulo { Id = 8, Descripcion = "Resma A4 Autor" });

        act.Should().Throw<DomainException>().WithMessage("*ya es fuente de otro artículo*");
        item.IdArticulo.Should().Be(7);
    }

    [Fact]
    public void AsignarArticulo_PresentacionDerivada_LanzaDomainException()
    {
        var item = CrearItem();
        var derivado = new Articulo { Id = 9, Descripcion = "Hoja suelta", IdArticuloOrigen = 7, UnidadesPorOrigen = 500 };

        var act = () => item.AsignarArticulo(derivado);

        act.Should().Throw<DomainException>().WithMessage("*presentación derivada*");
        item.EstaVinculado.Should().BeFalse();
    }

    [Fact]
    public void AsignarArticulo_Servicio_LanzaDomainExceptionYNoLoVincula()
    {
        var item = CrearItem();
        var servicio = new Articulo { Id = 9, Descripcion = "Anillado", EsServicio = true };

        var act = () => item.AsignarArticulo(servicio);

        act.Should().Throw<DomainException>().WithMessage("*no puede tener fuentes*");
        item.EstaVinculado.Should().BeFalse();
    }

    [Fact]
    public void AsignarArticulo_ArticuloNulo_LanzaArgumentNullException()
    {
        var item = CrearItem();

        var act = () => item.AsignarArticulo(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EsFuenteDe_OtraInstanciaConElMismoId_DevuelveTrue()
    {
        // Simula el artículo materializado de nuevo por EF Core: misma fila, otra instancia.
        var item = CrearItem();
        item.AsignarArticulo(new Articulo { Id = 7, Descripcion = "Resma A4" });

        item.EsFuenteDe(new Articulo { Id = 7, Descripcion = "Resma A4" }).Should().BeTrue();
        item.EsFuenteDe(new Articulo { Id = 8, Descripcion = "Otra" }).Should().BeFalse();
    }

    [Fact]
    public void LiberarArticulo_ItemVinculado_QuedaLibreYSePuedeAsignarAOtro()
    {
        var item = CrearItem();
        item.AsignarArticulo(new Articulo { Id = 7, Descripcion = "Resma A4" });

        item.LiberarArticulo();
        item.AsignarArticulo(new Articulo { Id = 8, Descripcion = "Resma A4 Autor" });

        item.IdArticulo.Should().Be(8);
    }

    private static CatalogoProveedor CrearItem()
    {
        return new CatalogoProveedor
        {
            IdProveedor = 1,
            CodigoProveedor = "COD-001",
            DescripcionProveedor = "Resma A4",
            CostoReposicion = 100m
        };
    }
}

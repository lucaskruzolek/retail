using FluentAssertions;
using Retail.App.ViewModels.Compras;
using Retail.Application.DTOs.Compras;
using Retail.Application.DTOs.Pedidos;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

/// <summary>
/// Un renglón de compra anticipa el precio que dejará el registro (RF-19): usa el markup real del artículo y la
/// fórmula del Dominio, nunca un porcentaje fijo.
/// </summary>
public sealed class DetalleCompraItemViewModelTests
{
    [Fact]
    public void NuevoPrecioVenta_ConMarkupDelArticulo_UsaLaFormulaDelDominio()
    {
        // Arrange: markup del 25 %, que no es el 40 % que antes se suponía para todos.
        var renglon = new DetalleCompraItemViewModel
        {
            CostoUnitario = 200m,
            CostoActual = 150m,
            PorcentajeGanancia = 25m,
            PrecioVentaActual = 187.50m,
        };

        // Assert
        renglon.NuevoPrecioVenta.Should().Be(Articulo.CalcularPrecioVenta(200m, 25m, RedondeoPrecioEnum.EnteroMasCercano));
        renglon.NuevoPrecioVenta.Should().Be(250m);
        renglon.NuevoPrecioVenta.Should().NotBe(200m * 1.4m);
    }

    [Fact]
    public void NuevoPrecioVenta_ConPoliticaMultiploDe10Superior_PrevisualizaElPrecioRedondeado()
    {
        // Arrange: 131 × 1,5 = 196,50
        var renglon = new DetalleCompraItemViewModel
        {
            CostoUnitario = 131m,
            CostoActual = 120m,
            PorcentajeGanancia = 50m,
            PrecioVentaActual = 180m,
        };

        // Act
        renglon.RedondeoPrecio = RedondeoPrecioEnum.MultiploDe10Superior;

        // Assert: la vista previa coincide con lo que guardará CompraService con esa política (D-38)
        renglon.NuevoPrecioVenta.Should().Be(200m);
        renglon.SubePrecio.Should().BeTrue();
    }

    [Fact]
    public void NuevoPrecioVenta_ConCostoCero_DevuelveElPrecioVigenteSinLanzar()
    {
        // Arrange
        var renglon = new DetalleCompraItemViewModel
        {
            CostoUnitario = 0m,
            CostoActual = 150m,
            PorcentajeGanancia = 25m,
            PrecioVentaActual = 187.50m,
        };

        // Act
        var nuevo = () => renglon.NuevoPrecioVenta;

        // Assert
        nuevo.Should().NotThrow();
        renglon.NuevoPrecioVenta.Should().Be(187.50m);
        renglon.SubePrecio.Should().BeFalse();
        renglon.BajaPrecio.Should().BeFalse();
    }

    [Theory]
    [InlineData(150, 150, false)]
    [InlineData(160, 150, true)]
    [InlineData(140, 150, true)]
    public void CambiaPrecio_SegunCostoDeLaFacturaYCostoVigente_SigueElCriterioDeCompraService(
        int costoFactura,
        int costoActual,
        bool esperado)
    {
        // Arrange
        var renglon = new DetalleCompraItemViewModel
        {
            CostoUnitario = costoFactura,
            CostoActual = costoActual,
            PorcentajeGanancia = 40m,
            PrecioVentaActual = 210m,
        };

        // Assert
        renglon.CambiaPrecio.Should().Be(esperado);
    }

    [Fact]
    public void SubePrecioYBajaPrecio_SegunElCostoNuevo_IndicanLaDireccionDelCambio()
    {
        // Arrange: costo 100 con markup 50 % deja el precio vigente en 150.
        var renglon = new DetalleCompraItemViewModel
        {
            CostoUnitario = 100m,
            CostoActual = 100m,
            PorcentajeGanancia = 50m,
            PrecioVentaActual = 150m,
        };

        // Assert: sin cambio de costo no sube ni baja.
        renglon.SubePrecio.Should().BeFalse();
        renglon.BajaPrecio.Should().BeFalse();

        renglon.CostoUnitario = 120m;
        renglon.SubePrecio.Should().BeTrue();
        renglon.BajaPrecio.Should().BeFalse();

        renglon.CostoUnitario = 80m;
        renglon.SubePrecio.Should().BeFalse();
        renglon.BajaPrecio.Should().BeTrue();
    }

    [Fact]
    public void Subtotal_AlEditarCantidadYCosto_SeRecalculaYNotifica()
    {
        // Arrange
        var renglon = new DetalleCompraItemViewModel { Cantidad = 2, CostoUnitario = 150m };
        var notificadas = new List<string?>();
        renglon.PropertyChanged += (_, e) => notificadas.Add(e.PropertyName);

        // Act
        renglon.Cantidad = 3;
        renglon.CostoUnitario = 200m;

        // Assert
        renglon.Subtotal.Should().Be(600m);
        notificadas.Should().Contain(nameof(DetalleCompraItemViewModel.Subtotal));
    }

    [Fact]
    public void CostoUnitario_AlCambiar_NotificaLosDatosDerivadosDelPrecio()
    {
        // Arrange
        var renglon = new DetalleCompraItemViewModel { CostoActual = 100m, CostoUnitario = 100m, PorcentajeGanancia = 50m, PrecioVentaActual = 150m };
        var notificadas = new List<string?>();
        renglon.PropertyChanged += (_, e) => notificadas.Add(e.PropertyName);

        // Act
        renglon.CostoUnitario = 130m;

        // Assert
        notificadas.Should().Contain(new[]
        {
            nameof(DetalleCompraItemViewModel.NuevoPrecioVenta),
            nameof(DetalleCompraItemViewModel.CambiaPrecio),
            nameof(DetalleCompraItemViewModel.SubePrecio),
            nameof(DetalleCompraItemViewModel.BajaPrecio),
        });
    }

    [Fact]
    public void DesdeRenglonDePedido_RenglonConCodigoDeProveedor_PrecargaCantidadYCostoPactado()
    {
        // Arrange
        var dto = RenglonDePedido(codigoProveedor: "PRV-101", codigoBarras: "7790001");

        // Act
        var renglon = DetalleCompraItemViewModel.DesdeRenglonDePedido(dto);

        // Assert
        renglon.IdArticulo.Should().Be(101);
        renglon.CodigoArticulo.Should().Be("PRV-101");
        renglon.CantidadPedida.Should().Be(5);
        renglon.Cantidad.Should().Be(5);
        renglon.CostoUnitario.Should().Be(500m);
        renglon.CostoActual.Should().Be(450m);
        renglon.PorcentajeGanancia.Should().Be(40m);
        renglon.PrecioVentaActual.Should().Be(630m);
        renglon.CambiaPrecio.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "7790001", "7790001")]
    [InlineData("  ", "7790001", "7790001")]
    [InlineData("", null, "—")]
    [InlineData("", "", "—")]
    public void DesdeRenglonDePedido_SinCodigoDeProveedor_CaeAlCodigoDeBarrasOGuion_NuncaAlIdInterno(
        string codigoProveedor,
        string? codigoBarras,
        string esperado)
    {
        // Arrange
        var dto = RenglonDePedido(codigoProveedor, codigoBarras);

        // Act
        var renglon = DetalleCompraItemViewModel.DesdeRenglonDePedido(dto);

        // Assert
        renglon.CodigoArticulo.Should().Be(esperado);
        renglon.CodigoArticulo.Should().NotBe("101");
    }

    [Fact]
    public void DesdeArticulo_ArticuloDeLaBusqueda_ArrancaConUnaUnidadAlCostoVigenteYSinPedido()
    {
        // Arrange
        var articulo = new ArticuloParaCompraDto
        {
            IdArticulo = 9,
            CodigoBarras = null,
            Descripcion = "Resma A4",
            CostoReposicion = 4000m,
            PorcentajeGanancia = 30m,
            PrecioVenta = 5200m,
        };

        // Act
        var renglon = DetalleCompraItemViewModel.DesdeArticulo(articulo);

        // Assert
        renglon.IdArticulo.Should().Be(9);
        renglon.CodigoArticulo.Should().Be("—", "un artículo sin código de barras no muestra su Id interno");
        renglon.Cantidad.Should().Be(1);
        renglon.CantidadPedida.Should().BeNull();
        renglon.CostoUnitario.Should().Be(4000m);
        renglon.CambiaPrecio.Should().BeFalse("el costo de la factura arranca igual al vigente");
        renglon.NuevoPrecioVenta.Should().Be(5200m);
    }

    private static RenglonPedidoParaCompraDto RenglonDePedido(string codigoProveedor, string? codigoBarras)
    {
        return new RenglonPedidoParaCompraDto
        {
            IdArticulo = 101,
            CodigoBarras = codigoBarras,
            DescripcionArticulo = "Cuaderno Rivadavia",
            CodigoProveedor = codigoProveedor,
            Cantidad = 5,
            CostoPactado = 500m,
            CostoReposicionActual = 450m,
            PorcentajeGanancia = 40m,
            PrecioVentaActual = 630m,
        };
    }
}

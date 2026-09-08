using FluentAssertions;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Caja;
using Retail.Application.DTOs.Clientes;
using Retail.Application.DTOs.Compras;
using Retail.Application.DTOs.Presupuestos;
using Retail.Application.DTOs.Ventas;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.Application.UnitTests.DTOs;

public class DtoCalculatedPropertiesTests
{
    [Theory]
    [InlineData(10, 5, false, false)]
    [InlineData(5, 5, false, true)]
    [InlineData(2, 5, false, true)]
    [InlineData(0, 5, true, false)] // Servicios nunca disparan alerta de stock bajo
    public void ArticuloDto_StockBajo_DebeCalcularseCorrectamente(int stockActual, int stockMinimo, bool esServicio, bool esperado)
    {
        // Arrange
        var dto = new ArticuloDto
        {
            IdArticulo = 1,
            Descripcion = "Cuaderno Rivadavia",
            IdCategoria = 1,
            IdMarca = 1,
            CostoReposicion = 1000m,
            PorcentajeGanancia = 40m,
            PrecioVenta = 1400m,
            StockActual = stockActual,
            StockMinimo = stockMinimo,
            EsServicio = esServicio
        };

        // Assert
        dto.StockBajo.Should().Be(esperado);
    }

    [Fact]
    public void ClienteDto_CreditoDisponible_DebeCalcularDiferenciaPositiva()
    {
        // Arrange
        var conCtaCte = new ClienteDto
        {
            IdCliente = 1,
            RazonSocialONombre = "Librería Central",
            TipoDocumento = TipoDocumentoEnum.Cuit,
            NumeroDocumento = "30-12345678-9",
            CondicionIva = CondicionIvaEnum.ResponsableInscripto,
            TieneCuentaCorriente = true,
            LimiteCredito = 50000m,
            SaldoCuentaCorriente = 20000m
        };

        var sinCtaCte = conCtaCte with { TieneCuentaCorriente = false };

        // Assert
        conCtaCte.CreditoDisponible.Should().Be(30000m);
        sinCtaCte.CreditoDisponible.Should().Be(0m);
    }

    [Fact]
    public void DetalleVentaDto_SubtotalItem_DebeCalcularCantidadPorPrecio()
    {
        // Arrange
        var item = new DetalleVentaDto
        {
            IdArticulo = 1,
            Descripcion = "Lapicera Bic",
            Cantidad = 4,
            PrecioUnitario = 250m
        };

        // Assert
        item.SubtotalItem.Should().Be(1000m);
    }

    [Fact]
    public void DetallePresupuestoDto_SubtotalItem_DebeCalcularCantidadPorPrecioPactado()
    {
        // Arrange
        var item = new DetallePresupuestoDto
        {
            IdArticulo = 2,
            Descripcion = "Resma A4",
            Cantidad = 5,
            PrecioUnitarioPactado = 4000m
        };

        // Assert
        item.SubtotalItem.Should().Be(20000m);
    }

    [Fact]
    public void DetalleCompraDto_SubtotalItem_DebeCalcularCantidadPorCostoUnitario()
    {
        // Arrange
        var item = new DetalleCompraDto
        {
            IdArticulo = 3,
            Cantidad = 10,
            CostoUnitario = 1500m
        };

        // Assert
        item.SubtotalItem.Should().Be(15000m);
    }

    [Fact]
    public void ResultadoArqueoDto_HaySobranteYHayFaltante_DebenReflejarDiferencia()
    {
        // Arrange
        var sobrante = new ResultadoArqueoDto
        {
            IdTurno = 1,
            SaldoInicial = 10000m,
            TotalVentasEfectivo = 5000m,
            TotalIngresosEfectivo = 0m,
            TotalEgresosEfectivo = 0m,
            SaldoTeoricoEfectivo = 15000m,
            SaldoDeclaradoEfectivo = 15500m,
            DiferenciaEfectivo = 500m,
            TotalVentasElectronicas = 20000m,
            MontoRetenidoEnCaja = 10000m,
            FechaCierre = DateTime.UtcNow
        };

        var faltante = sobrante with
        {
            SaldoDeclaradoEfectivo = 14500m,
            DiferenciaEfectivo = -500m
        };

        // Assert
        sobrante.HaySobrante.Should().BeTrue();
        sobrante.HayFaltante.Should().BeFalse();

        faltante.HaySobrante.Should().BeFalse();
        faltante.HayFaltante.Should().BeTrue();
    }
}

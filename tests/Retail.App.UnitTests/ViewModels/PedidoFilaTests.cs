using FluentAssertions;
using Retail.App.UnitTests.Helpers;
using Retail.App.ViewModels.Pedidos;
using Retail.Application.DTOs.Pedidos;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class PedidoFilaTests
{
    // 2026-10-28 12:00 UTC = 09:00 en Argentina.
    private static readonly RelojDePrueba s_reloj = new(new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero));

    private static PedidoResumenDto CrearResumen(EstadoPedidoEnum estado, DateTime creacionUtc, DateTime? emisionUtc = null)
    {
        return new PedidoResumenDto
        {
            IdPedido = 1,
            Estado = estado,
            FechaCreacion = creacionUtc,
            FechaEmision = emisionUtc
        };
    }

    [Fact]
    public void Constructor_BorradorCreadoHoy_AntiguedadHoyYFechaLocal()
    {
        var fila = new PedidoFila(CrearResumen(EstadoPedidoEnum.Borrador, new DateTime(2026, 10, 28, 11, 0, 0)), s_reloj);

        fila.Antiguedad.Should().Be("Hoy");
        fila.Fecha.Should().Be(new DateTime(2026, 10, 28, 8, 0, 0), "se muestra en hora local (UTC-3)");
    }

    [Fact]
    public void Constructor_EmitidoHaceTresDias_AntiguedadDesdeLaEmision()
    {
        var resumen = CrearResumen(
            EstadoPedidoEnum.Emitido,
            creacionUtc: new DateTime(2026, 10, 20, 15, 0, 0),
            emisionUtc: new DateTime(2026, 10, 25, 15, 0, 0));

        var fila = new PedidoFila(resumen, s_reloj);

        fila.Antiguedad.Should().Be("3 días");
        fila.Fecha.Date.Should().Be(new DateTime(2026, 10, 25));
    }

    [Fact]
    public void Constructor_FechaUtcDeMadrugada_CuentaElDiaLocal()
    {
        // 02:00 UTC del 28 son las 23:00 del 27 en Argentina: para el usuario fue ayer.
        var fila = new PedidoFila(CrearResumen(EstadoPedidoEnum.Borrador, new DateTime(2026, 10, 28, 2, 0, 0)), s_reloj);

        fila.Antiguedad.Should().Be("1 día");
    }

    [Theory]
    [InlineData(EstadoPedidoEnum.Recibido)]
    [InlineData(EstadoPedidoEnum.Cancelado)]
    public void Constructor_PedidoCerrado_SinAntiguedad(EstadoPedidoEnum estado)
    {
        var fila = new PedidoFila(CrearResumen(estado, new DateTime(2026, 10, 1)), s_reloj);

        fila.EstaAbierto.Should().BeFalse();
        fila.Antiguedad.Should().BeEmpty();
    }
}

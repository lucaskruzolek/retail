using System.Globalization;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using Retail.Application.DTOs.Presupuestos;
using Retail.Domain.Enums;
using Retail.Infrastructure.Hardware;
using Retail.Infrastructure.Presupuestos;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Presupuestos;

/// <summary>
/// PDF del presupuesto (PS-07). QuestPDF comprime las páginas, así que el contenido se verifica sobre el modelo
/// intermedio <see cref="ContenidoPresupuestoPdf"/> (todo lo que se imprime), y el exportador real se prueba solo
/// para confirmar que escribe un PDF válido sin cerrar el flujo.
/// </summary>
public class ExportadorPresupuestoPdfTests
{
    private const string TituloInterno = "Nota interna reservada del comercio";

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");

    private static readonly TicketPrinterOptions Membrete = new()
    {
        NombreComercio = "LIBRERÍA RETAIL TEST",
        Direccion = "Calle Falsa 123",
        Cuit = "30-99887766-5",
        CondicionIva = "Responsable Inscripto"
    };

    [Fact]
    public async Task ExportarAsync_PresupuestoValido_EscribePdfSinCerrarElFlujo()
    {
        // Arrange
        var exportador = new ExportadorPresupuestoPdf(Options.Create(Membrete));
        await using var flujo = new MemoryStream();

        // Act
        await exportador.ExportarAsync(CrearPresupuesto(), flujo);

        // Assert
        flujo.CanWrite.Should().BeTrue("el exportador no cierra el flujo recibido");
        var bytes = flujo.ToArray();
        bytes.Length.Should().BeGreaterThan(500);
        Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public void Crear_PresupuestoConCliente_ContieneNumeroFechasClienteRenglonesTotalesYLeyendas()
    {
        // Arrange
        var presupuesto = CrearPresupuesto(clienteNombre: "Instituto Rivadavia", descuento: 400m);

        // Act
        var contenido = ContenidoPresupuestoPdf.Crear(presupuesto, Membrete);

        // Assert
        contenido.NombreComercio.Should().Be("LIBRERÍA RETAIL TEST");
        contenido.Direccion.Should().Be("Calle Falsa 123");
        contenido.Cuit.Should().Be("CUIT: 30-99887766-5");
        contenido.CondicionIva.Should().Be("Responsable Inscripto");
        contenido.Encabezado.Should().Be("PRESUPUESTO N.º 00000123");
        contenido.FechaEmision.Should().Be("Fecha de emisión: 05/11/2026");
        contenido.ValidoHasta.Should().Be("Válido hasta: 20/11/2026");
        contenido.Cliente.Should().Be("Instituto Rivadavia");

        var renglon = contenido.Renglones.Should().ContainSingle().Subject;
        renglon.Cantidad.Should().Be("3");
        renglon.Descripcion.Should().Be("Caja Marcadores al Agua x12");
        renglon.PrecioUnitario.Should().Be(1200m.ToString("C0", Cultura));
        renglon.Subtotal.Should().Be(3600m.ToString("C0", Cultura));

        contenido.Subtotal.Should().Be(3600m.ToString("C0", Cultura));
        contenido.Descuento.Should().Be("-" + 400m.ToString("C0", Cultura));
        contenido.Total.Should().Be(3200m.ToString("C0", Cultura));
        contenido.Leyendas.Should().Equal(
            "Documento no válido como factura",
            "Los precios se mantienen hasta la fecha de validez indicada");
    }

    [Fact]
    public void Crear_SinCliente_ImprimeConsumidorFinal()
    {
        // Arrange
        var presupuesto = CrearPresupuesto(clienteNombre: null);

        // Act
        var contenido = ContenidoPresupuestoPdf.Crear(presupuesto, Membrete);

        // Assert
        contenido.Cliente.Should().Be("Consumidor Final");
    }

    [Fact]
    public void Crear_PresupuestoConTitulo_NingunTextoImpresoLoContiene()
    {
        // Arrange: PS-20, el título es de uso interno y nunca se entrega al cliente
        var presupuesto = CrearPresupuesto(titulo: TituloInterno, clienteNombre: "Instituto Rivadavia", descuento: 400m);

        // Act
        var contenido = ContenidoPresupuestoPdf.Crear(presupuesto, Membrete);

        // Assert
        var textos = contenido.TodosLosTextos().ToList();
        textos.Should().NotBeEmpty();
        textos.Should().NotContain(t => t.Contains(TituloInterno, StringComparison.OrdinalIgnoreCase));
        textos.Should().NotContain(t => t.Contains("reservada", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Crear_SinDescuento_NoImprimeLaLineaDeDescuento()
    {
        // Arrange
        var presupuesto = CrearPresupuesto(descuento: 0m);

        // Act
        var contenido = ContenidoPresupuestoPdf.Crear(presupuesto, Membrete);

        // Assert
        contenido.Descuento.Should().BeNull();
        contenido.Total.Should().Be(contenido.Subtotal);
    }

    [Fact]
    public void CrearDocumento_ConContenido_ComponeUnaPaginaA4SinErrores()
    {
        // Arrange
        var contenido = ContenidoPresupuestoPdf.Crear(CrearPresupuesto(descuento: 400m), Membrete);

        // Act
        using var flujo = new MemoryStream();
        var act = () => ExportadorPresupuestoPdf.CrearDocumento(contenido).GeneratePdf(flujo);

        // Assert
        act.Should().NotThrow();
        flujo.Length.Should().BeGreaterThan(0);
    }

    private static PresupuestoDto CrearPresupuesto(string? titulo = null, string? clienteNombre = null, decimal descuento = 0m)
    {
        return new PresupuestoDto
        {
            IdPresupuesto = 123,
            IdUsuario = 2,
            UsuarioNombre = "Ana Perez",
            IdCliente = clienteNombre is null ? null : 3,
            ClienteNombre = clienteNombre,
            Titulo = titulo,
            FechaEmision = new DateTime(2026, 11, 5, 15, 0, 0, DateTimeKind.Utc),
            FechaVencimiento = new DateOnly(2026, 11, 20),
            Subtotal = 3600m,
            Descuento = descuento,
            Total = 3600m - descuento,
            Estado = EstadoPresupuestoEnum.Pendiente,
            EstaVigente = true,
            Items =
            [
                new DetallePresupuestoDto
                {
                    IdArticulo = 20,
                    Descripcion = "Caja Marcadores al Agua x12",
                    Cantidad = 3,
                    PrecioUnitarioPactado = 1200m
                }
            ]
        };
    }
}

using System.Globalization;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Retail.Application.DTOs.Listas;
using Retail.Infrastructure.Hardware;
using Retail.Infrastructure.Listas;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Listas;

/// <summary>
/// PDF de la lista de artículos (RF-30). QuestPDF comprime las páginas, así que el contenido se verifica sobre el
/// modelo intermedio <see cref="ContenidoListaPdf"/> y el exportador real se prueba solo para confirmar que escribe
/// un PDF válido sin cerrar el flujo.
/// </summary>
public class ExportadorListaPdfTests
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");

    private static readonly TicketPrinterOptions Membrete = new()
    {
        NombreComercio = "LIBRERÍA RETAIL TEST",
        Direccion = "Calle Falsa 123",
        Cuit = "30-99887766-5",
        CondicionIva = "Responsable Inscripto"
    };

    [Fact]
    public async Task ExportarAsync_ListaValida_EscribePdfSinCerrarElFlujo()
    {
        // Arrange
        var exportador = new ExportadorListaPdf(Options.Create(Membrete));
        await using var flujo = new MemoryStream();

        // Act
        await exportador.ExportarAsync(CrearLista(), new DateOnly(2026, 11, 19), flujo);

        // Assert
        flujo.CanWrite.Should().BeTrue("el exportador no cierra el flujo recibido");
        var bytes = flujo.ToArray();
        bytes.Length.Should().BeGreaterThan(500);
        Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public void Crear_ListaConArticuloNoDisponible_ImprimeSoloLosDisponiblesConLeyendaYTotal()
    {
        // Arrange
        var lista = CrearLista();

        // Act
        var contenido = ContenidoListaPdf.Crear(lista, new DateOnly(2026, 11, 19), Membrete);

        // Assert
        contenido.NombreComercio.Should().Be("LIBRERÍA RETAIL TEST");
        contenido.Cuit.Should().Be("CUIT: 30-99887766-5");
        contenido.Titulo.Should().Be("3.º Grado Colegio N");
        contenido.LeyendaPrecios.Should().Be("Precios al 19/11/2026, sujetos a modificación");

        var renglon = contenido.Renglones.Should().ContainSingle().Subject;
        renglon.Cantidad.Should().Be("3");
        renglon.Descripcion.Should().Be("Cuaderno rayado");
        renglon.PrecioUnitario.Should().Be(1200m.ToString("C0", Cultura));
        renglon.Subtotal.Should().Be(3600m.ToString("C0", Cultura));

        contenido.Total.Should().Be(3600m.ToString("C0", Cultura));
        contenido.Leyendas.Should().Equal("Documento no válido como factura");
        contenido.TodosLosTextos().Should().NotContain("Lápiz descatalogado");
    }

    private static ListaDto CrearLista()
    {
        return new ListaDto
        {
            IdLista = 7,
            Nombre = "3.º Grado Colegio N",
            FechaModificacion = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc),
            TotalEstimado = 3600m,
            Renglones =
            [
                new RenglonListaDto
                {
                    IdArticulo = 1,
                    Descripcion = "Cuaderno rayado",
                    Cantidad = 3,
                    PrecioActual = 1200m,
                    Subtotal = 3600m,
                    EstaDisponible = true
                },
                new RenglonListaDto
                {
                    IdArticulo = 2,
                    Descripcion = "Lápiz descatalogado",
                    Cantidad = 5,
                    PrecioActual = 100m,
                    Subtotal = 500m,
                    EstaDisponible = false
                }
            ]
        };
    }
}

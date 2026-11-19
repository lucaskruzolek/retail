using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Retail.Application.DTOs.Caja;
using Retail.Application.DTOs.Clientes;
using Retail.Application.DTOs.Listas;
using Retail.Application.DTOs.Presupuestos;
using Retail.Application.DTOs.Ventas;
using Retail.Domain.Enums;
using Retail.Infrastructure.Hardware;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

public class FileDebugTicketPrinterServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly FileDebugTicketPrinterService _sut;

    public FileDebugTicketPrinterServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "retail_test_tickets_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);

        var options = Options.Create(new TicketPrinterOptions
        {
            OutputDirectory = _testDirectory,
            PrintToConsole = false,
            AnchoCaracteres = 42,
            NombreComercio = "LIBRERÍA RETAIL TEST",
            Direccion = "Calle Falsa 123",
            Cuit = "30-99887766-5",
            CondicionIva = "Responsable Inscripto"
        });

        var logger = Substitute.For<ILogger<FileDebugTicketPrinterService>>();
        _sut = new FileDebugTicketPrinterService(options, logger);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Ignorar excepciones de limpieza en carpetas temporales
            }
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ImprimirTicketVentaAsync_VentaValida_GeneraArchivoTxtConFormatoCorrecto()
    {
        // Arrange
        var venta = new VentaResponseDto
        {
            IdVenta = 42,
            IdTurno = 1,
            IdUsuario = 3,
            ClienteNombre = "Juan Perez",
            IdPresupuestoOrigen = null,
            FechaHora = new DateTime(2026, 9, 8, 15, 30, 0, DateTimeKind.Utc),
            Subtotal = 3400.00m,
            Descuento = 400.00m,
            Total = 3000.00m,
            EstadoFiscal = EstadoFiscalEnum.NoAplica,
            Items = new List<DetalleVentaDto>
            {
                new()
                {
                    IdArticulo = 10,
                    Descripcion = "Cuaderno Espiral A4 Rivadavia",
                    Cantidad = 2,
                    PrecioUnitario = 1500.00m
                },
                new()
                {
                    IdArticulo = 11,
                    Descripcion = "Lapicera Azul BIC",
                    Cantidad = 1,
                    PrecioUnitario = 400.00m
                }
            },
            Pagos = new List<PagoVentaDto>
            {
                new()
                {
                    MedioPago = MedioPagoEnum.Efectivo,
                    Monto = 3000.00m,
                    ReferenciaPago = null,
                    MontoRecibido = 3500.00m,
                    Vuelto = 500.00m
                }
            }
        };

        // Act
        await _sut.ImprimirTicketVentaAsync(venta);

        // Assert
        _sut.UltimoArchivoGenerado.Should().NotBeNullOrWhiteSpace();
        File.Exists(_sut.UltimoArchivoGenerado).Should().BeTrue();

        string contenido = await File.ReadAllTextAsync(_sut.UltimoArchivoGenerado!);
        contenido.Should().Contain("LIBRERÍA RETAIL TEST");
        contenido.Should().Contain("TICKET DE VENTA");
        contenido.Should().Contain("#00000042");
        contenido.Should().Contain("Juan Perez");
        contenido.Should().Contain("Cuaderno Espiral A4 Rivadavia");
        contenido.Should().Contain("Lapicera Azul BIC");
        contenido.Should().Contain("TOTAL:");
        contenido.Should().Contain("Efectivo");
        contenido.Should().Contain("VUELTO ENTREGADO");
        contenido.Should().Contain("COMPROBANTE NO FISCAL");
    }

    [Fact]
    public async Task ImprimirReciboCobranzaAsync_CobranzaValida_GeneraReciboDuplicadoConSaldos()
    {
        // Arrange
        var cobranza = new CobranzaResultadoDto
        {
            IdCobranza = 15,
            IdCliente = 7,
            ClienteNombre = "Escuela Normal Nro 1",
            FechaHora = new DateTime(2026, 9, 8, 16, 0, 0, DateTimeKind.Utc),
            MedioPago = MedioPagoEnum.TransferenciaQr,
            MontoAbonado = 12000.00m,
            SaldoAnterior = 25000.00m,
            NuevoSaldo = 13000.00m,
            Referencia = "TRF-987654"
        };

        // Act
        await _sut.ImprimirReciboCobranzaAsync(cobranza);

        // Assert
        _sut.UltimoArchivoGenerado.Should().NotBeNullOrWhiteSpace();
        File.Exists(_sut.UltimoArchivoGenerado).Should().BeTrue();

        string contenido = await File.ReadAllTextAsync(_sut.UltimoArchivoGenerado!);
        contenido.Should().Contain("RECIBO OFICIAL DE COBRANZA");
        contenido.Should().Contain("#00000015");
        contenido.Should().Contain("Escuela Normal Nro 1");
        contenido.Should().Contain("TransferenciaQr");
        contenido.Should().Contain("TRF-987654");
        contenido.Should().Contain("SALDO ANTERIOR:");
        contenido.Should().Contain("MONTO ABONADO:");
        contenido.Should().Contain("NUEVO SALDO DEUDOR:");
        contenido.Should().Contain("Documento cancelatorio no fiscal");
    }

    [Fact]
    public async Task ImprimirActaArqueoAsync_ArqueoValido_GeneraActaConDiferenciasYVentasElectronicas()
    {
        // Arrange
        var arqueo = new ResultadoArqueoDto
        {
            IdTurno = 5,
            SaldoInicial = 5000.00m,
            TotalVentasEfectivo = 25000.00m,
            TotalIngresosEfectivo = 1500.00m,
            TotalEgresosEfectivo = 500.00m,
            SaldoTeoricoEfectivo = 31000.00m,
            SaldoDeclaradoEfectivo = 31000.00m,
            DiferenciaEfectivo = 0.00m,
            TotalVentasElectronicas = 18500.00m,
            MontoRetenidoEnCaja = 5000.00m,
            FechaCierre = new DateTime(2026, 9, 8, 22, 0, 0, DateTimeKind.Utc)
        };

        // Act
        await _sut.ImprimirActaArqueoAsync(arqueo);

        // Assert
        _sut.UltimoArchivoGenerado.Should().NotBeNullOrWhiteSpace();
        File.Exists(_sut.UltimoArchivoGenerado).Should().BeTrue();

        string contenido = await File.ReadAllTextAsync(_sut.UltimoArchivoGenerado!);
        contenido.Should().Contain("ACTA DE ARQUEO Y CIERRE DE CAJA");
        contenido.Should().Contain("#000005");
        contenido.Should().Contain("SALDO TEÓRICO EFECTIVO:");
        contenido.Should().Contain("SALDO DECLARADO:");
        contenido.Should().Contain("DIFERENCIA (EXACTO):");
        contenido.Should().Contain("Total Cobros Electrónicos:");
        contenido.Should().Contain("Monto Retenido en Gaveta:");
        contenido.Should().Contain("Firma Cajero:");
    }

    [Fact]
    public async Task ImprimirPresupuestoAsync_PresupuestoValido_GeneraCotizacionConPreciosPactados()
    {
        // Arrange
        var presupuesto = new PresupuestoDto
        {
            IdPresupuesto = 8,
            IdUsuario = 2,
            UsuarioNombre = "pfernandez",
            IdCliente = 3,
            ClienteNombre = "Instituto Rivadavia",
            FechaEmision = new DateTime(2026, 9, 8, 10, 0, 0, DateTimeKind.Utc),
            FechaVencimiento = new DateOnly(2026, 9, 23),
            EstaVigente = true,
            Titulo = "Nota interna del comercio",
            Subtotal = 15000.00m,
            Descuento = 0.00m,
            Total = 15000.00m,
            Estado = EstadoPresupuestoEnum.Pendiente,
            Items = new List<DetallePresupuestoDto>
            {
                new()
                {
                    IdArticulo = 20,
                    Descripcion = "Caja Marcadores al Agua x12",
                    Cantidad = 3,
                    PrecioUnitarioPactado = 5000.00m
                }
            }
        };

        // Act
        await _sut.ImprimirPresupuestoAsync(presupuesto);

        // Assert
        _sut.UltimoArchivoGenerado.Should().NotBeNullOrWhiteSpace();
        File.Exists(_sut.UltimoArchivoGenerado).Should().BeTrue();

        string contenido = await File.ReadAllTextAsync(_sut.UltimoArchivoGenerado!);
        contenido.Should().Contain("PRESUPUESTO COMERCIAL");
        contenido.Should().Contain("#00000008");
        contenido.Should().Contain("Instituto Rivadavia");
        contenido.Should().Contain("pfernandez");
        contenido.Should().Contain("Caja Marcadores al Agua x12");
        contenido.Should().Contain("DOCUMENTO NO VÁLIDO COMO FACTURA");
        contenido.Should().Contain("Precios pactados válidos hasta el 23/09/2026");
        contenido.Should().NotContain("Nota interna del comercio", "el título es de uso interno (PS-20)");
        contenido.Should().Contain("No reserva ni garantiza stock físico");
    }

    [Fact]
    public async Task ImprimirListaAsync_ConArticuloNoDisponible_ImprimeNombreLeyendaYSoloLosDisponibles()
    {
        // Arrange
        var lista = new ListaDto
        {
            IdLista = 4,
            Nombre = "3.º Grado Colegio N",
            FechaModificacion = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc),
            TotalEstimado = 3600.00m,
            Renglones = new List<RenglonListaDto>
            {
                new()
                {
                    IdArticulo = 1,
                    Descripcion = "Cuaderno rayado",
                    Cantidad = 3,
                    PrecioActual = 1200.00m,
                    Subtotal = 3600.00m,
                    EstaDisponible = true
                },
                new()
                {
                    IdArticulo = 2,
                    Descripcion = "Lápiz descatalogado",
                    Cantidad = 5,
                    PrecioActual = 100.00m,
                    Subtotal = 500.00m,
                    EstaDisponible = false
                }
            }
        };

        // Act
        await _sut.ImprimirListaAsync(lista, new DateOnly(2026, 11, 19));

        // Assert
        _sut.UltimoArchivoGenerado.Should().NotBeNullOrWhiteSpace();
        string contenido = await File.ReadAllTextAsync(_sut.UltimoArchivoGenerado!);
        contenido.Should().Contain("3.º Grado Colegio N");
        contenido.Should().Contain("Precios al 19/11/2026, sujetos a modificación");
        contenido.Should().Contain("Cuaderno rayado");
        contenido.Should().NotContain("Lápiz descatalogado", "los artículos dados de baja no se imprimen (L-13)");
        contenido.Should().Contain("DOCUMENTO NO VÁLIDO COMO FACTURA");
    }

    [Fact]
    public async Task ImprimirPresupuestoAsync_VigenciaDeTreintaDias_ImprimeLaFechaRealDeVencimientoYNoUnPlazoFijo()
    {
        // Arrange: con vigencia editable (PS-10), un plazo fijo de "15 días" sería falso
        var presupuesto = new PresupuestoDto
        {
            IdPresupuesto = 9,
            IdUsuario = 2,
            FechaEmision = new DateTime(2026, 11, 2, 15, 0, 0, DateTimeKind.Utc),
            FechaVencimiento = new DateOnly(2026, 12, 2),
            EstaVigente = true,
            Titulo = "Lista escolar reservada",
            Subtotal = 1000m,
            Descuento = 0m,
            Total = 1000m,
            Estado = EstadoPresupuestoEnum.Pendiente,
            Items = new List<DetallePresupuestoDto>
            {
                new() { IdArticulo = 1, Descripcion = "Cuaderno", Cantidad = 1, PrecioUnitarioPactado = 1000m }
            }
        };

        // Act
        await _sut.ImprimirPresupuestoAsync(presupuesto);

        // Assert
        string contenido = await File.ReadAllTextAsync(_sut.UltimoArchivoGenerado!);
        contenido.Should().Contain("Fecha Vencimiento: 02/12/2026");
        contenido.Should().Contain("Precios pactados válidos hasta el 02/12/2026");
        contenido.Should().NotContain("15 días");
        contenido.Should().Contain("DOCUMENTO NO VÁLIDO COMO FACTURA");
        contenido.Should().NotContain("Lista escolar reservada", "el título es de uso interno (PS-20)");
    }
}

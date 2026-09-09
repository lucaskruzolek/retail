using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Retail.Application.DTOs.Caja;
using Retail.Application.DTOs.Clientes;
using Retail.Application.DTOs.Presupuestos;
using Retail.Application.DTOs.Ventas;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Domain.Enums;

namespace Retail.Infrastructure.Hardware;

/// <summary>
/// Implementación de depuración y desarrollo para el despacho de tickets térmicos hacia consola y archivos de texto plano.
/// Cumple con la Ley 6 de AGENTS.md aislando el hardware físico de mostrador.
/// </summary>
public partial class FileDebugTicketPrinterService : ITicketPrinterService
{
    private static readonly CultureInfo CulturaArgentina = CultureInfo.GetCultureInfo("es-AR");
    private readonly TicketPrinterOptions _options;
    private readonly ILogger<FileDebugTicketPrinterService> _logger;

    public string? UltimoArchivoGenerado { get; private set; }

    public string? UltimoContenidoGenerado { get; private set; }

    public FileDebugTicketPrinterService(
        IOptions<TicketPrinterOptions> options,
        ILogger<FileDebugTicketPrinterService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task ImprimirTicketVentaAsync(VentaResponseDto venta, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        int ancho = _options.AnchoCaracteres;

        AgregarEncabezadoComercio(sb, ancho);
        sb.AppendLine(CulturaArgentina, $"{CentrarTexto("TICKET DE VENTA", ancho)}");
        sb.AppendLine(CulturaArgentina, $"{Separador('-', ancho)}");
        sb.AppendLine(CulturaArgentina, $"Comprobante: #{venta.IdVenta:D8}");
        sb.AppendLine(CulturaArgentina, $"Fecha/Hora:  {venta.FechaHora:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine(CulturaArgentina, $"Turno Caja:  #{venta.IdTurno} | Cajero: #{venta.IdUsuario}");
        sb.AppendLine(CulturaArgentina, $"Cliente:     {venta.ClienteNombre ?? "Consumidor Final"}");

        if (venta.IdPresupuestoOrigen.HasValue)
        {
            sb.AppendLine(CulturaArgentina, $"Presupuesto Origen: #{venta.IdPresupuestoOrigen.Value:D8}");
        }

        sb.AppendLine(CulturaArgentina, $"{Separador('-', ancho)}");
        sb.AppendLine(CulturaArgentina, $"{AlinearTresColumnas("CANT", "DESCRIPCIÓN", "SUBTOTAL", ancho)}");
        sb.AppendLine(CulturaArgentina, $"{Separador('-', ancho)}");

        foreach (var item in venta.Items)
        {
            string lineaItem = $"{item.Cantidad} x {FormatearMoneda(item.PrecioUnitario)}";
            sb.AppendLine(CulturaArgentina, $"{TruncarTexto(item.Descripcion, ancho)}");
            sb.AppendLine(CulturaArgentina, $"{AlinearDosExtremos($"  {lineaItem}", FormatearMoneda(item.SubtotalItem), ancho)}");
        }

        sb.AppendLine(CulturaArgentina, $"{Separador('-', ancho)}");
        sb.AppendLine(CulturaArgentina, $"{AlinearDosExtremos("SUBTOTAL:", FormatearMoneda(venta.Subtotal), ancho)}");

        if (venta.Descuento > 0)
        {
            sb.AppendLine(CulturaArgentina, $"{AlinearDosExtremos("DESCUENTO:", $"-{FormatearMoneda(venta.Descuento)}", ancho)}");
        }

        sb.AppendLine(CulturaArgentina, $"{AlinearDosExtremos("TOTAL:", FormatearMoneda(venta.Total), ancho)}");
        sb.AppendLine(CulturaArgentina, $"{Separador('-', ancho)}");

        sb.AppendLine(CulturaArgentina, $"FORMAS DE PAGO:");
        foreach (var pago in venta.Pagos)
        {
            string descPago = pago.MedioPago.ToString();
            if (!string.IsNullOrWhiteSpace(pago.ReferenciaPago))
            {
                descPago += $" ({pago.ReferenciaPago})";
            }

            sb.AppendLine(CulturaArgentina, $"{AlinearDosExtremos($" - {descPago}:", FormatearMoneda(pago.Monto), ancho)}");

            if (pago.Vuelto.HasValue && pago.Vuelto.Value > 0)
            {
                sb.AppendLine(CulturaArgentina, $"{AlinearDosExtremos("   (VUELTO ENTREGADO):", FormatearMoneda(pago.Vuelto.Value), ancho)}");
            }
        }

        sb.AppendLine(CulturaArgentina, $"{Separador('=', ancho)}");
        sb.AppendLine(CulturaArgentina, $"ESTADO FISCAL: {venta.EstadoFiscal}");

        if (venta.EstadoFiscal == EstadoFiscalEnum.NoAplica || venta.EstadoFiscal == EstadoFiscalEnum.ErrorFiscalReintentable)
        {
            sb.AppendLine(CulturaArgentina, $"{CentrarTexto("* COMPROBANTE NO FISCAL *", ancho)}");
            sb.AppendLine(CulturaArgentina, $"{CentrarTexto("DOCUMENTO INTERNO DE VENTA", ancho)}");
        }

        sb.AppendLine(CulturaArgentina, $"{Separador('=', ancho)}");

        string nombreArchivo = $"ticket_venta_{venta.IdVenta}_{DateTime.Now:yyyyMMdd_HHmmssfff}.txt";
        await GuardarYEmitirAsync(nombreArchivo, sb.ToString(), cancellationToken);
    }

    public async Task ImprimirReciboCobranzaAsync(CobranzaResultadoDto cobranza, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        int ancho = _options.AnchoCaracteres;

        AgregarEncabezadoComercio(sb, ancho);
        sb.AppendLine(CulturaArgentina, $"{CentrarTexto("RECIBO OFICIAL DE COBRANZA", ancho)}");
        sb.AppendLine(CulturaArgentina, $"{CentrarTexto("(CUENTA CORRIENTE - ORIGINAL)", ancho)}");
        sb.AppendLine(CulturaArgentina, $"{Separador('-', ancho)}");
        sb.AppendLine(CulturaArgentina, $"Recibo Nro:  #{cobranza.IdCobranza:D8}");
        sb.AppendLine(CulturaArgentina, $"Fecha/Hora:  {cobranza.FechaHora:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine(CulturaArgentina, $"Cliente:     {cobranza.ClienteNombre} (ID #{cobranza.IdCliente})");
        sb.AppendLine(CulturaArgentina, $"Medio Pago:  {cobranza.MedioPago}");

        if (!string.IsNullOrWhiteSpace(cobranza.Referencia))
        {
            sb.AppendLine(CulturaArgentina, $"Referencia:  {cobranza.Referencia}");
        }

        sb.AppendLine(CulturaArgentina, $"{Separador('-', ancho)}");
        sb.AppendLine(CulturaArgentina, $"{AlinearDosExtremos("SALDO ANTERIOR:", FormatearMoneda(cobranza.SaldoAnterior), ancho)}");
        sb.AppendLine(CulturaArgentina, $"{AlinearDosExtremos("MONTO ABONADO:", FormatearMoneda(cobranza.MontoAbonado), ancho)}");
        sb.AppendLine(CulturaArgentina, $"{AlinearDosExtremos("NUEVO SALDO DEUDOR:", FormatearMoneda(cobranza.NuevoSaldo), ancho)}");
        sb.AppendLine(CulturaArgentina, $"{Separador('=', ancho)}");
        sb.AppendLine(CulturaArgentina, $"{CentrarTexto("Documento cancelatorio no fiscal", ancho)}");
        sb.AppendLine(CulturaArgentina, $"{CentrarTexto("Firma / Sello de Recepción", ancho)}");
        sb.AppendLine();
        sb.AppendLine(CulturaArgentina, $"{CentrarTexto("___________________________________", ancho)}");
        sb.AppendLine(CulturaArgentina, $"{Separador('=', ancho)}");

        string nombreArchivo = $"recibo_cobranza_{cobranza.IdCobranza}_{DateTime.Now:yyyyMMdd_HHmmssfff}.txt";
        await GuardarYEmitirAsync(nombreArchivo, sb.ToString(), cancellationToken);
    }

    public async Task ImprimirActaArqueoAsync(ResultadoArqueoDto arqueo, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        int ancho = _options.AnchoCaracteres;

        AgregarEncabezadoComercio(sb, ancho);
        sb.AppendLine(CulturaArgentina, $"{CentrarTexto("ACTA DE ARQUEO Y CIERRE DE CAJA", ancho)}");
        sb.AppendLine(CulturaArgentina, $"{Separador('-', ancho)}");
        sb.AppendLine(CulturaArgentina, $"Turno Nro:    #{arqueo.IdTurno:D6}");
        sb.AppendLine(CulturaArgentina, $"Fecha Cierre: {arqueo.FechaCierre:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine(CulturaArgentina, $"{Separador('-', ancho)}");
        sb.AppendLine(CulturaArgentina, $"RESUMEN DE EFECTIVO:");
        sb.AppendLine(CulturaArgentina, $"{AlinearDosExtremos("(+) Saldo Inicial:", FormatearMoneda(arqueo.SaldoInicial), ancho)}");
        sb.AppendLine(CulturaArgentina, $"{AlinearDosExtremos("(+) Ventas Efectivo:", FormatearMoneda(arqueo.TotalVentasEfectivo), ancho)}");
        sb.AppendLine(CulturaArgentina, $"{AlinearDosExtremos("(+) Ingresos Efectivo:", FormatearMoneda(arqueo.TotalIngresosEfectivo), ancho)}");
        sb.AppendLine(CulturaArgentina, $"{AlinearDosExtremos("(-) Egresos Efectivo:", FormatearMoneda(arqueo.TotalEgresosEfectivo), ancho)}");
        sb.AppendLine(CulturaArgentina, $"{Separador('-', ancho)}");
        sb.AppendLine(CulturaArgentina, $"{AlinearDosExtremos("(=) SALDO TEÓRICO EFECTIVO:", FormatearMoneda(arqueo.SaldoTeoricoEfectivo), ancho)}");
        sb.AppendLine(CulturaArgentina, $"{AlinearDosExtremos("    SALDO DECLARADO:", FormatearMoneda(arqueo.SaldoDeclaradoEfectivo), ancho)}");

        string estadoDiferencia = arqueo.HaySobrante ? "(SOBRANTE)" : arqueo.HayFaltante ? "(FALTANTE)" : "(EXACTO)";
        sb.AppendLine(CulturaArgentina, $"{AlinearDosExtremos($"    DIFERENCIA {estadoDiferencia}:", FormatearMoneda(arqueo.DiferenciaEfectivo), ancho)}");
        sb.AppendLine(CulturaArgentina, $"{Separador('-', ancho)}");

        sb.AppendLine(CulturaArgentina, $"OPERACIONES ELECTRÓNICAS (POSNET / QR):");
        sb.AppendLine(CulturaArgentina, $"{AlinearDosExtremos("    Total Cobros Electrónicos:", FormatearMoneda(arqueo.TotalVentasElectronicas), ancho)}");
        sb.AppendLine(CulturaArgentina, $"{CentrarTexto("(Cotejar con cierre de lote POS físico)", ancho)}");
        sb.AppendLine(CulturaArgentina, $"{Separador('-', ancho)}");

        sb.AppendLine(CulturaArgentina, $"FONDO RESERVADO PRÓXIMO TURNO:");
        sb.AppendLine(CulturaArgentina, $"{AlinearDosExtremos("    Monto Retenido en Gaveta:", FormatearMoneda(arqueo.MontoRetenidoEnCaja), ancho)}");
        sb.AppendLine(CulturaArgentina, $"{Separador('=', ancho)}");
        sb.AppendLine(CulturaArgentina, $"Firma Cajero:    __________________________");
        sb.AppendLine(CulturaArgentina, $"Firma Encargado: __________________________");
        sb.AppendLine(CulturaArgentina, $"{Separador('=', ancho)}");

        string nombreArchivo = $"acta_arqueo_turno_{arqueo.IdTurno}_{DateTime.Now:yyyyMMdd_HHmmssfff}.txt";
        await GuardarYEmitirAsync(nombreArchivo, sb.ToString(), cancellationToken);
    }

    public async Task ImprimirPresupuestoAsync(PresupuestoDto presupuesto, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        int ancho = _options.AnchoCaracteres;

        AgregarEncabezadoComercio(sb, ancho);
        sb.AppendLine(CulturaArgentina, $"{CentrarTexto("PRESUPUESTO COMERCIAL", ancho)}");
        sb.AppendLine(CulturaArgentina, $"{Separador('-', ancho)}");
        sb.AppendLine(CulturaArgentina, $"Presupuesto: #{presupuesto.IdPresupuesto:D8}");
        sb.AppendLine(CulturaArgentina, $"Fecha Emisión:     {presupuesto.FechaEmision:dd/MM/yyyy}");
        sb.AppendLine(CulturaArgentina, $"Fecha Vencimiento: {presupuesto.FechaVencimiento:dd/MM/yyyy} (15 días)");
        sb.AppendLine(CulturaArgentina, $"Operador:          {presupuesto.UsuarioNombre ?? $"ID #{presupuesto.IdUsuario}"}");
        sb.AppendLine(CulturaArgentina, $"Cliente:           {presupuesto.ClienteNombre ?? "Consumidor Final"}");
        sb.AppendLine(CulturaArgentina, $"Estado:            {presupuesto.Estado}");
        sb.AppendLine(CulturaArgentina, $"{Separador('-', ancho)}");

        sb.AppendLine(CulturaArgentina, $"{AlinearTresColumnas("CANT", "DESCRIPCIÓN", "SUBTOTAL", ancho)}");
        sb.AppendLine(CulturaArgentina, $"{Separador('-', ancho)}");

        foreach (var item in presupuesto.Items)
        {
            string lineaItem = $"{item.Cantidad} x {FormatearMoneda(item.PrecioUnitarioPactado)}";
            sb.AppendLine(CulturaArgentina, $"{TruncarTexto(item.Descripcion, ancho)}");
            sb.AppendLine(CulturaArgentina, $"{AlinearDosExtremos($"  {lineaItem}", FormatearMoneda(item.SubtotalItem), ancho)}");
        }

        sb.AppendLine(CulturaArgentina, $"{Separador('-', ancho)}");
        sb.AppendLine(CulturaArgentina, $"{AlinearDosExtremos("SUBTOTAL:", FormatearMoneda(presupuesto.Subtotal), ancho)}");

        if (presupuesto.Descuento > 0)
        {
            sb.AppendLine(CulturaArgentina, $"{AlinearDosExtremos("DESCUENTO:", $"-{FormatearMoneda(presupuesto.Descuento)}", ancho)}");
        }

        sb.AppendLine(CulturaArgentina, $"{AlinearDosExtremos("TOTAL PRESUPUESTADO:", FormatearMoneda(presupuesto.Total), ancho)}");
        sb.AppendLine(CulturaArgentina, $"{Separador('=', ancho)}");
        sb.AppendLine(CulturaArgentina, $"{CentrarTexto("* DOCUMENTO NO VÁLIDO COMO FACTURA *", ancho)}");
        sb.AppendLine(CulturaArgentina, $"{CentrarTexto("Precios pactados válidos por 15 días.", ancho)}");
        sb.AppendLine(CulturaArgentina, $"{CentrarTexto("No reserva ni garantiza stock físico.", ancho)}");
        sb.AppendLine(CulturaArgentina, $"{Separador('=', ancho)}");

        string nombreArchivo = $"presupuesto_{presupuesto.IdPresupuesto}_{DateTime.Now:yyyyMMdd_HHmmssfff}.txt";
        await GuardarYEmitirAsync(nombreArchivo, sb.ToString(), cancellationToken);
    }

    private void AgregarEncabezadoComercio(StringBuilder sb, int ancho)
    {
        sb.AppendLine(CulturaArgentina, $"{Separador('=', ancho)}");
        sb.AppendLine(CulturaArgentina, $"{CentrarTexto(_options.NombreComercio, ancho)}");
        sb.AppendLine(CulturaArgentina, $"{CentrarTexto(_options.Direccion, ancho)}");
        sb.AppendLine(CulturaArgentina, $"{CentrarTexto($"CUIT: {_options.Cuit}", ancho)}");
        sb.AppendLine(CulturaArgentina, $"{CentrarTexto(_options.CondicionIva, ancho)}");
        sb.AppendLine(CulturaArgentina, $"{Separador('=', ancho)}");
    }

    private async Task GuardarYEmitirAsync(string nombreArchivo, string contenido, CancellationToken cancellationToken)
    {
        UltimoContenidoGenerado = contenido;

        string directorio = _options.OutputDirectory;
        if (!Directory.Exists(directorio))
        {
            Directory.CreateDirectory(directorio);
        }

        string rutaCompleta = Path.Combine(directorio, nombreArchivo);
        await File.WriteAllTextAsync(rutaCompleta, contenido, Encoding.UTF8, cancellationToken);
        UltimoArchivoGenerado = Path.GetFullPath(rutaCompleta);

        if (_options.PrintToConsole)
        {
            Console.WriteLine(contenido);
        }

        LogTicketEmitido(_logger, UltimoArchivoGenerado);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Ticket emitido correctamente en archivo simulado: {RutaArchivo}")]
    private static partial void LogTicketEmitido(ILogger logger, string rutaArchivo);

    private static string FormatearMoneda(decimal monto)
    {
        return monto.ToString("C2", CulturaArgentina);
    }

    private static string TruncarTexto(string texto, int longitudMaxima)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return string.Empty;
        }

        return texto.Length <= longitudMaxima ? texto : texto[..(longitudMaxima - 3)] + "...";
    }

    private static string CentrarTexto(string texto, int ancho)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return string.Empty;
        }

        if (texto.Length >= ancho)
        {
            return texto;
        }

        int espaciosIzquierda = (ancho - texto.Length) / 2;
        return texto.PadLeft(texto.Length + espaciosIzquierda).PadRight(ancho);
    }

    private static string Separador(char caracter, int ancho)
    {
        return new string(caracter, Math.Max(1, ancho));
    }

    private static string AlinearDosExtremos(string izquierda, string derecha, int ancho)
    {
        int espacioDisponible = ancho - izquierda.Length - derecha.Length;
        if (espacioDisponible <= 0)
        {
            return $"{izquierda} {derecha}";
        }

        return izquierda + new string(' ', espacioDisponible) + derecha;
    }

    private static string AlinearTresColumnas(string col1, string col2, string col3, int ancho)
    {
        int anchoCol1 = 6;
        int anchoCol3 = 12;
        int anchoCol2 = Math.Max(10, ancho - anchoCol1 - anchoCol3);

        string p1 = col1.PadRight(anchoCol1);
        string p2 = col2.Length > anchoCol2 ? col2[..(anchoCol2 - 1)] + " " : col2.PadRight(anchoCol2);
        string p3 = col3.PadLeft(anchoCol3);

        return $"{p1}{p2}{p3}";
    }
}

namespace Retail.Infrastructure.Hardware;

/// <summary>
/// Opciones de configuración para el despacho e impresión simulada de tickets térmicos en disco y consola.
/// </summary>
public class TicketPrinterOptions
{
    public const string SectionName = "TicketPrinterSettings";

    public string OutputDirectory { get; set; } = "debug-tickets";

    public bool PrintToConsole { get; set; } = true;

    public int AnchoCaracteres { get; set; } = 42;

    public string NombreComercio { get; set; } = "LIBRERÍA RETAIL";

    public string Direccion { get; set; } = "Av. Universitaria 1234, Ciudad";

    public string Cuit { get; set; } = "30-11223344-9";

    public string CondicionIva { get; set; } = "Responsable Inscripto";
}

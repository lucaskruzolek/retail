namespace Retail.Infrastructure.ExternalServices.ArcaSdk;

/// <summary>
/// Opciones de configuración para la integración con el microservicio fiscal arcasdk / Web Service ARCA (AFIP).
/// </summary>
public class ArcaOptions
{
    public const string SectionName = "ArcaSettings";

    public string BaseUrl { get; set; } = "http://localhost:8080";

    public int TimeoutSeconds { get; set; } = 10;

    public bool UseMockArca { get; set; } = true;

    public int PuntoVentaDefecto { get; set; } = 1;
}

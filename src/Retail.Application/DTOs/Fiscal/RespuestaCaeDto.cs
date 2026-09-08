namespace Retail.Application.DTOs.Fiscal;

/// <summary>
/// Respuesta devuelta por arcasdk tras procesar la solicitud con ARCA.
/// </summary>
public record class RespuestaCaeDto
{
    public required bool Exitoso { get; init; }
    public string? Cae { get; init; }
    public DateOnly? FechaVtoCae { get; init; }
    public int? NumeroComprobante { get; init; }
    public string? ResultadoArca { get; init; }
    public string? MotivoError { get; init; }
}

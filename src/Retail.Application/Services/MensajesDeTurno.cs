namespace Retail.Application.Services;

/// <summary>
/// Mensajes compartidos por los servicios que verifican que un turno de caja pertenece a quien lo opera, para que
/// CajaService, VentaService y ClienteService digan lo mismo (MT-14).
/// </summary>
internal static class MensajesDeTurno
{
    public static string TurnoAjeno(string? nombreCajero)
    {
        var cajero = string.IsNullOrWhiteSpace(nombreCajero) ? "otro cajero" : nombreCajero;

        return $"El turno de esta caja es de {cajero}. Solo esa persona puede operarlo; un Gerente puede cerrarlo.";
    }
}

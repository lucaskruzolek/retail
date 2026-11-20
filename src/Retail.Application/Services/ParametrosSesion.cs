namespace Retail.Application.Services;

/// <summary>
/// Tiempos de la sesión única por usuario (MT-3). Los usan el servicio, la implementación SQL y la UI, para que todos
/// hablen del mismo intervalo y del mismo vencimiento.
/// </summary>
public static class ParametrosSesion
{
    /// <summary>Cada cuánto renueva su latido una terminal con sesión abierta.</summary>
    public static readonly TimeSpan IntervaloLatido = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Sin latido durante este tiempo, la sesión se considera vencida y otra terminal puede tomarla. Es tres veces el
    /// intervalo: tolera dos latidos perdidos por un corte de red breve. Se mide siempre con el reloj del servidor.
    /// </summary>
    public static readonly TimeSpan Vencimiento = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Diferencia máxima tolerada entre el reloj de esta PC y el del servidor (H10). Por encima, el latido avisa: las
    /// fechas de las ventas se registran con la hora de la PC y un reloj corrido desordena el historial.
    /// </summary>
    public static readonly TimeSpan UmbralDesfaseReloj = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Cada cuántos latidos (unos 5 minutos) se comprueba si la base fue migrada a una versión más nueva que esta
    /// aplicación (H12).
    /// </summary>
    public const int LatidosEntreControlesDeEsquema = 5;
}

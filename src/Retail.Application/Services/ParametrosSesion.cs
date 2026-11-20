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
}

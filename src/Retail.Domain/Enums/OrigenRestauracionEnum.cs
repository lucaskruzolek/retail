namespace Retail.Domain.Enums;

/// <summary>
/// Desde dónde se pidió una restauración de la base (RF-22, RF-23). Cada origen se autoriza de forma distinta (D-21).
/// </summary>
public enum OrigenRestauracionEnum
{
    /// <summary>
    /// Un Gerente logueado en la aplicación, que se reautentica contra la base vigente (D-8).
    /// </summary>
    Aplicacion = 1,

    /// <summary>
    /// <c>Retail.Cli restaurar</c>: recuperación ante desastres, con la credencial de un Gerente verificada dentro de la
    /// copia (D-18).
    /// </summary>
    Consola = 2
}

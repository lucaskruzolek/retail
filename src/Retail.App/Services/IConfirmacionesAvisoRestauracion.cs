namespace Retail.App.Services;

/// <summary>
/// Registra qué operador ya leyó el aviso de una restauración (R-20), para no repetirlo en cada inicio de sesión.
/// </summary>
/// <remarks>
/// La confirmación se guarda por perfil de Windows (en un archivo de la PC, no en la base de datos). Limitación
/// aceptada: la sesión de Windows suele compartirse entre el personal del mostrador, así que el archivo es común a
/// todos; por eso la confirmación es por operador de la aplicación: que un Gerente cierre el aviso no lo oculta a
/// los demás, y una restauración nueva (otra referencia) vuelve a mostrarlo.
/// </remarks>
public interface IConfirmacionesAvisoRestauracion
{
    /// <summary>
    /// Indica si el operador ya confirmó el aviso de esa restauración.
    /// </summary>
    /// <param name="idUsuario">Operador de la aplicación que inició sesión.</param>
    /// <param name="referenciaRestauracion">Identificador único de la restauración avisada.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<bool> EstaConfirmadaAsync(int idUsuario, Guid referenciaRestauracion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra que el operador leyó el aviso de esa restauración. Solo interesa la última confirmada por operador.
    /// </summary>
    /// <param name="idUsuario">Operador de la aplicación que cerró el aviso.</param>
    /// <param name="referenciaRestauracion">Identificador único de la restauración avisada.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task ConfirmarAsync(int idUsuario, Guid referenciaRestauracion, CancellationToken cancellationToken = default);
}

namespace Retail.Application.DTOs.Sistema;

/// <summary>
/// Condiciones que la aplicación verifica antes de mostrar el login (RF-23): esquema al día y al menos un Gerente.
/// </summary>
public record class EstadoSistemaDto
{
    public required EstadoEsquemaDto Esquema { get; init; }

    /// <summary>
    /// Solo se consulta si el esquema está al día: con la base desactualizada, la tabla de usuarios puede no existir.
    /// </summary>
    public required bool ExisteGerenteActivo { get; init; }

    /// <summary>
    /// Nombres de usuario de los Gerentes sin email de recuperación (cuentas creadas antes de que fuera obligatorio).
    /// No impide operar, pero deja al Gerente sin canal de recuperación.
    /// </summary>
    public required IReadOnlyList<string> GerentesSinEmail { get; init; }

    public bool PuedeOperar => Esquema.EstaAlDia && ExisteGerenteActivo;
}

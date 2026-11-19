namespace Retail.Application.DTOs.Terminales;

/// <summary>
/// Resultado de verificar, al arrancar, si esta PC está activada como terminal.
/// </summary>
public record class EstadoTerminalDto
{
    public required EstadoTerminalEnum Estado { get; init; }

    /// <summary>
    /// La terminal guardada en esta PC. Es <c>null</c> cuando el estado es <see cref="EstadoTerminalEnum.NoActivada"/> o
    /// <see cref="EstadoTerminalEnum.Inexistente"/>.
    /// </summary>
    public TerminalDto? Terminal { get; init; }
}

/// <summary>
/// Situación de esta PC respecto de las terminales de la base.
/// </summary>
public enum EstadoTerminalEnum
{
    /// <summary>La terminal guardada existe y su equipo coincide con esta PC.</summary>
    Activa,

    /// <summary>Esta PC no guardó ninguna terminal: debe activarla un Gerente.</summary>
    NoActivada,

    /// <summary>La terminal guardada no existe o fue dada de baja (por ejemplo, tras restaurar una copia más vieja).</summary>
    Inexistente,

    /// <summary>La terminal existe, pero registrada con otro equipo: la pantalla solo avisa.</summary>
    EquipoDistinto
}

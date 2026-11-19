using Retail.Application.DTOs.Terminales;

namespace Retail.App.ViewModels.Terminales;

/// <summary>
/// Una terminal activa ofrecida como opción para reemplazar su equipo: el nombre del puesto y, debajo, el equipo de
/// Windows con el que quedó registrada (una terminal sin equipo asignado se dice con todas las letras).
/// </summary>
public sealed record OpcionTerminal(int Id, string Nombre, string TextoEquipo)
{
    public const string SinEquipoAsignado = "sin equipo asignado";

    public static OpcionTerminal Desde(TerminalDto terminal)
    {
        ArgumentNullException.ThrowIfNull(terminal);

        var equipo = string.IsNullOrWhiteSpace(terminal.NombreEquipo)
            ? SinEquipoAsignado
            : terminal.NombreEquipo.Trim();

        return new OpcionTerminal(terminal.Id, terminal.Nombre, equipo);
    }
}

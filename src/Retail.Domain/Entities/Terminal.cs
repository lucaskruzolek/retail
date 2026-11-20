using Retail.Domain.Common;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Entities;

/// <summary>
/// Raíz de Agregado que representa un puesto de trabajo del local (una caja de mostrador). Cada PC se activa como una
/// terminal y cada turno de caja pertenece a una (MT-1). El <see cref="Nombre"/> es el que ve el operador
/// ("Mostrador 1"); el <see cref="NombreEquipo"/> es el nombre de Windows de la PC y solo sirve de control: si se
/// cambia el hardware, la terminal sigue siendo la misma y su historial queda intacto (<see cref="ReemplazarEquipo"/>).
/// </summary>
/// <remarks>
/// La unicidad del nombre entre terminales activas la garantiza un índice único filtrado en la base, no el agregado.
/// </remarks>
public class Terminal : BaseEntity, IAggregateRoot
{
    public const int LongitudMaximaNombre = 60;
    public const int LongitudMaximaNombreEquipo = 64;

    // Lo usa EF Core para materializar la entidad desde la base; el código de negocio usa Crear.
    protected Terminal()
    {
    }

    public string Nombre { get; private set; } = string.Empty;

    public string NombreEquipo { get; private set; } = string.Empty;

    public static Terminal Crear(string nombre, string nombreEquipo)
    {
        var terminal = new Terminal();
        terminal.Renombrar(nombre);
        terminal.ReemplazarEquipo(nombreEquipo);
        return terminal;
    }

    public void Renombrar(string nombre)
    {
        var normalizado = string.IsNullOrWhiteSpace(nombre) ? string.Empty : ReglasTexto.NormalizarEspacios(nombre);
        if (normalizado.Length == 0 || normalizado.Length > LongitudMaximaNombre)
        {
            throw new DomainException(
                $"El nombre de la terminal es obligatorio y no puede superar los {LongitudMaximaNombre} caracteres.");
        }

        Nombre = normalizado;
    }

    /// <summary>
    /// Cambio de hardware: la terminal pasa a ser esta PC. El nombre de Windows se guarda tal cual (sin normalizar
    /// mayúsculas) para poder compararlo con <c>Environment.MachineName</c>.
    /// </summary>
    public void ReemplazarEquipo(string nombreEquipo)
    {
        var recortado = nombreEquipo?.Trim() ?? string.Empty;
        if (recortado.Length == 0 || recortado.Length > LongitudMaximaNombreEquipo)
        {
            throw new DomainException(
                $"El nombre del equipo es obligatorio y no puede superar los {LongitudMaximaNombreEquipo} caracteres.");
        }

        NombreEquipo = recortado;
    }
}

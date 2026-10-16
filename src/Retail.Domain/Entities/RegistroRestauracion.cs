using Retail.Domain.Common;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Entities;

/// <summary>
/// Raíz de Agregado que deja constancia de una restauración de la base (RF-22, D-22): quién la pidió, qué copia se
/// restauró y qué quedó como red de seguridad.
/// </summary>
/// <remarks>
/// Es un hecho consumado e inmutable: se crea recién cuando la copia ya pasó todas las validaciones, y se escribe en la
/// base restaurada antes del intercambio. Un intento fallido no deja registro aquí (la base temporal se descarta), sino
/// en el log. Igual que <see cref="RegistroBackup"/>, guarda al operador como foto y tiene una
/// <see cref="Referencia"/> estable para rehidratarse en restauraciones posteriores sin duplicarse (R-1).
/// </remarks>
public class RegistroRestauracion : BaseEntity, IAggregateRoot
{
    public const int LongitudMaximaNombreBase = 128;

    // Lo usa EF Core para materializar la entidad desde la base; el código de negocio usa Registrar.
    private RegistroRestauracion()
    {
    }

    /// <summary>
    /// Identidad estable entre bases (ver <see cref="RegistroBackup.Referencia"/>).
    /// </summary>
    public Guid Referencia { get; private set; }

    public OrigenRestauracionEnum Origen { get; private set; }

    /// <summary>
    /// Gerente que autorizó la restauración. Desde la consola es el Gerente verificado dentro de la copia (D-18).
    /// </summary>
    public int IdUsuario { get; private set; }

    public string NombreUsuario { get; private set; } = string.Empty;

    public DateTime Fecha { get; private set; }

    /// <summary>
    /// Copia restaurada (<see cref="RegistroBackup.Referencia"/>, o la del manifiesto si se restauró desde un archivo).
    /// </summary>
    public Guid ReferenciaCopia { get; private set; }

    public string RutaArchivo { get; private set; } = string.Empty;

    /// <summary>
    /// Última migración que traía la copia, antes de ponerla al día con esta versión de la aplicación.
    /// </summary>
    public string MigracionCopia { get; private set; } = string.Empty;

    /// <summary>
    /// Copia <c>PreRestauracion</c> de la base reemplazada (D-20); <c>null</c> si esa base no era accesible.
    /// </summary>
    public Guid? ReferenciaCopiaPrevia { get; private set; }

    /// <summary>
    /// Nombre con el que quedó la base reemplazada (<c>RetailDb_Anterior_&lt;fecha&gt;</c>); <c>null</c> si no había
    /// una base vigente (por ejemplo, en una PC nueva).
    /// </summary>
    public string? BaseAnterior { get; private set; }

    public static RegistroRestauracion Registrar(
        OrigenRestauracionEnum origen,
        int idUsuario,
        string nombreUsuario,
        Guid referenciaCopia,
        string rutaArchivo,
        string migracionCopia,
        Guid? referenciaCopiaPrevia,
        string? baseAnterior)
    {
        if (!Enum.IsDefined(origen))
        {
            throw new DomainException($"El origen de restauración '{origen}' no es válido.");
        }

        // Toda restauración la autoriza un Gerente con su contraseña, también desde la consola (D-18).
        if (idUsuario <= 0)
        {
            throw new DomainException("Una restauración debe registrar el Gerente que la autorizó.");
        }

        if (referenciaCopia == Guid.Empty)
        {
            throw new DomainException("Debe indicarse la referencia de la copia restaurada.");
        }

        if (referenciaCopiaPrevia == Guid.Empty)
        {
            throw new DomainException("La referencia de la copia previa no puede estar vacía: si no hubo copia, es nula.");
        }

        return new RegistroRestauracion
        {
            Referencia = Guid.NewGuid(),
            Origen = origen,
            IdUsuario = idUsuario,
            NombreUsuario = ExigirTexto(nombreUsuario, "el nombre del Gerente", ReglasTexto.LongitudMaximaNombreUsuario),
            Fecha = DateTime.UtcNow,
            ReferenciaCopia = referenciaCopia,
            RutaArchivo = ExigirTexto(rutaArchivo, "la ruta del archivo restaurado", RegistroBackup.LongitudMaximaRuta),
            MigracionCopia = ExigirTexto(migracionCopia, "la migración de la copia", RegistroBackup.LongitudMaximaMigracion),
            ReferenciaCopiaPrevia = referenciaCopiaPrevia,
            BaseAnterior = string.IsNullOrWhiteSpace(baseAnterior)
                ? null
                : ExigirTexto(baseAnterior, "el nombre de la base anterior", LongitudMaximaNombreBase)
        };
    }

    private static string ExigirTexto(string valor, string campo, int longitudMaxima)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new DomainException($"Debe indicarse {campo} en el registro de restauración.");
        }

        var texto = valor.Trim();
        if (texto.Length > longitudMaxima)
        {
            throw new DomainException($"{char.ToUpperInvariant(campo[0])}{campo[1..]} supera los {longitudMaxima} caracteres.");
        }

        return texto;
    }
}

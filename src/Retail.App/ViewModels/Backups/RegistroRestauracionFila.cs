using System.IO;
using Retail.App.Converters;
using Retail.Application.DTOs.Backups;

namespace Retail.App.ViewModels.Backups;

/// <summary>
/// Fila de la grilla de restauraciones (D-34): el DTO con los textos ya preparados para mostrar (hora local, origen
/// legible, responsables y nombre de archivo). La conversión vive acá y no en la vista para poder probarla.
/// </summary>
public sealed class RegistroRestauracionFila
{
    private const string SinDato = "—";

    public RegistroRestauracionFila(RegistroRestauracionDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        Dto = dto;

        // La base guarda UTC pero EF Core devuelve el valor sin zona (Kind Unspecified): se declara UTC antes de convertir.
        FechaLocal = DateTime.SpecifyKind(dto.Fecha, DateTimeKind.Utc).ToLocalTime();
    }

    public RegistroRestauracionDto Dto { get; }

    public DateTime FechaLocal { get; }

    public string OrigenTexto => EnumATextoConverter.ATexto(Dto.Origen);

    /// <summary>
    /// Quien ejecutó la restauración: el Administrador del control dual (D-30) o, sin operador, el Gerente o la consola.
    /// </summary>
    public string EjecutadaPor => Dto.NombreOperador ?? SinDato;

    /// <summary>
    /// El Gerente que la autorizó.
    /// </summary>
    public string AutorizadaPor => Dto.NombreUsuario;

    public string ArchivoCopia => Path.GetFileName(Dto.RutaArchivo);

    public string CopiaPreviaTexto => Dto.TuvoCopiaPrevia ? "Sí" : "No";

    public string BaseAnteriorTexto => string.IsNullOrWhiteSpace(Dto.BaseAnterior) ? SinDato : Dto.BaseAnterior;
}

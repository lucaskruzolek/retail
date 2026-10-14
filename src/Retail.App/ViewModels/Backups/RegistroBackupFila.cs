using System.Globalization;
using Retail.Application.DTOs.Backups;
using Retail.Domain.Enums;

namespace Retail.App.ViewModels.Backups;

/// <summary>
/// Fila de la grilla de respaldos: el DTO del historial con los textos ya preparados para mostrar (hora local, tamaño
/// legible). La conversión vive acá y no en la vista para poder probarla.
/// </summary>
public sealed class RegistroBackupFila
{
    public RegistroBackupFila(RegistroBackupDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        Dto = dto;

        // La base guarda UTC pero EF Core devuelve el valor sin zona (Kind Unspecified): se declara UTC antes de convertir.
        FechaLocal = DateTime.SpecifyKind(dto.FechaInicio, DateTimeKind.Utc).ToLocalTime();
    }

    public RegistroBackupDto Dto { get; }

    public Guid Referencia => Dto.Referencia;

    public DateTime FechaLocal { get; }

    public TipoBackupEnum Tipo => Dto.Tipo;

    public EstadoBackupEnum Estado => Dto.Estado;

    public string Operador => Dto.Operador;

    public bool EsRestaurable => Dto.EsRestaurable;

    public string? MensajeError => Dto.MensajeError;

    public string TamanoTexto => Dto.TamanoBytes is { } bytes ? FormatearTamano(bytes) : "—";

    /// <summary>
    /// Tamaño en la unidad más grande que lo deje por encima de 1 (la copia de una librería ronda los MB).
    /// </summary>
    public static string FormatearTamano(long bytes)
    {
        string[] unidades = ["B", "KB", "MB", "GB"];
        double valor = bytes;
        var indice = 0;

        while (valor >= 1024 && indice < unidades.Length - 1)
        {
            valor /= 1024;
            indice++;
        }

        return indice == 0
            ? $"{bytes.ToString(CultureInfo.CurrentCulture)} B"
            : $"{valor.ToString("N1", CultureInfo.CurrentCulture)} {unidades[indice]}";
    }
}

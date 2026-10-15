namespace Retail.Infrastructure.Backups;

/// <summary>
/// Sección <c>BackupSettings</c> de <c>appsettings.json</c> (RNF-08, D-15).
/// </summary>
public class BackupOptions
{
    public const string SectionName = "BackupSettings";

    /// <summary>
    /// Directorio dedicado, fuera del repositorio y del perfil de usuario: al pasar a SQL Server Express solo hay
    /// que darle permisos a la cuenta del servicio (D-15). Admite variables de entorno (<c>%PROGRAMDATA%</c>).
    /// </summary>
    public string Directorio { get; set; } = @"C:\RetailData\Backups";

    /// <summary>
    /// Copias con archivo que se conservan; las más viejas se purgan.
    /// </summary>
    public int CantidadMaximaCopias { get; set; } = 14;

    /// <summary>
    /// Días que se conserva cada copia previa a restaurar (D-24). No cuenta dentro de <see cref="CantidadMaximaCopias"/>.
    /// </summary>
    public int DiasRetencionPreRestauracion { get; set; } = 30;

    /// <summary>
    /// Espacio libre mínimo en el disco del directorio para iniciar una copia.
    /// </summary>
    public long EspacioLibreMinimoMb { get; set; } = 500;
}

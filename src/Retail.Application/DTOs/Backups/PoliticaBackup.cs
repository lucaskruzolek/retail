namespace Retail.Application.DTOs.Backups;

/// <summary>
/// Parámetros de negocio de los respaldos (RNF-08). Los arma la Infraestructura a partir de la sección
/// <c>BackupSettings</c> de <c>appsettings.json</c>: Application no depende del sistema de configuración.
/// </summary>
/// <param name="CantidadMaximaCopias">Copias con archivo que se conservan (sin contar las previas a restaurar); las más
/// viejas se purgan.</param>
/// <param name="VersionApp">Versión de la aplicación que se registra en cada copia.</param>
/// <param name="DiasRetencionPreRestauracion">Días que se conserva cada copia previa a restaurar (D-24): un restore
/// equivocado puede detectarse tarde.</param>
public sealed record PoliticaBackup(int CantidadMaximaCopias, string VersionApp, int DiasRetencionPreRestauracion = 30);

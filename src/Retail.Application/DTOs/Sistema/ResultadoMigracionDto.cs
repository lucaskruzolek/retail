using Retail.Application.DTOs.Backups;

namespace Retail.Application.DTOs.Sistema;

/// <summary>
/// Resultado de <c>Retail.Cli migrar</c>: las migraciones aplicadas y la copia que se tomó antes (D-10).
/// </summary>
/// <param name="Aplicadas">Migraciones aplicadas, en orden.</param>
/// <param name="CopiaPrevia">Copia <c>PreMigracion</c>; <c>null</c> si la base no existía o ya estaba al día.</param>
public sealed record ResultadoMigracionDto(IReadOnlyList<string> Aplicadas, ResultadoBackupDto? CopiaPrevia);

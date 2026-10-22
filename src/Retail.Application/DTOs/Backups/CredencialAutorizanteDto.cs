namespace Retail.Application.DTOs.Backups;

/// <summary>
/// Credencial del Gerente que autoriza una restauración ejecutada por un Administrador (control dual, D-30).
/// </summary>
public sealed record CredencialAutorizanteDto(string NombreUsuario, string Password);

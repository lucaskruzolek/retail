namespace Retail.App.ViewModels.Backups;

/// <summary>
/// Cómo terminó el diálogo de credencial: la operación se completó, el operador canceló (ambos nulos) o la operación
/// falló por un motivo que no es una contraseña incorrecta.
/// </summary>
/// <param name="Completada">La operación terminó con éxito.</param>
/// <param name="Error">Falla que cerró el diálogo; <c>null</c> si se completó o se canceló.</param>
public sealed record ResultadoCredencial(bool Completada, Exception? Error);

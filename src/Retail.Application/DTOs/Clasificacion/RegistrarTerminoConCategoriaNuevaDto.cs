namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Crea una categoría y registra en ella un término en una sola operación (Track 6.6, P6-4, M-62): la acción "crear
/// categoría y asignar" de un bloque de términos nuevos del modal de incorporación. Si el término ya existe, se
/// reasigna a la categoría nueva (T-3).
/// </summary>
public record class RegistrarTerminoConCategoriaNuevaDto
{
    public required string Termino { get; init; }

    public required string NombreCategoria { get; init; }

    public int IdRubro { get; init; }
}

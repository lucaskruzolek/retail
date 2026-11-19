namespace Retail.Application.DTOs.Presupuestos;

/// <summary>Un cliente que tiene al menos un presupuesto: una opción del filtro de la columna Cliente.</summary>
public record class ClienteConPresupuestosDto
{
    public int IdCliente { get; init; }

    public string Nombre { get; init; } = string.Empty;
}

namespace Retail.Application.DTOs.Listas;

/// <summary>
/// Un renglón de la lista tal como lo envía la pantalla: solo el artículo y la cantidad. La descripción y el precio
/// no viajan: la lista no guarda precios (L-08).
/// </summary>
public record class RenglonListaEntradaDto
{
    public required int IdArticulo { get; init; }

    public required int Cantidad { get; init; }
}

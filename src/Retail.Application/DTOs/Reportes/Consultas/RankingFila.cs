namespace Retail.Application.DTOs.Reportes.Consultas;

/// <summary>Un artículo del ranking de ventas, con el ingreso neto sin redondear.</summary>
public record RankingFila(
    int IdArticulo,
    string Descripcion,
    string? CodigoBarras,
    string? Categoria,
    string? Marca,
    int Unidades,
    decimal IngresoNeto);

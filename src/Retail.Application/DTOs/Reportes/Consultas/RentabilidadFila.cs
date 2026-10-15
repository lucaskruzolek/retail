namespace Retail.Application.DTOs.Reportes.Consultas;

/// <summary>
/// Un grupo de rentabilidad, sin redondear. El descuento global ya está prorrateado entre los ítems según su
/// subtotal (D-4), por lo que la suma de los grupos coincide con el margen total.
/// </summary>
/// <param name="IdGrupo">Id de la categoría, marca o artículo; <c>null</c> si el artículo no tiene.</param>
/// <param name="Grupo">Nombre del grupo; <c>null</c> si no tiene categoría o marca.</param>
/// <param name="Unidades">Unidades vendidas.</param>
/// <param name="IngresoNeto">Ingreso neto del grupo.</param>
/// <param name="Costo">Costo histórico del grupo.</param>
public record RentabilidadFila(int? IdGrupo, string? Grupo, int Unidades, decimal IngresoNeto, decimal Costo);

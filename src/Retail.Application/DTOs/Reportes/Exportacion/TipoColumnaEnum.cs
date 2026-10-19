namespace Retail.Application.DTOs.Reportes.Exportacion;

/// <summary>Tipo de dato de una columna; el exportador decide el formato de celda según este tipo.</summary>
public enum TipoColumnaEnum
{
    Texto,
    Entero,
    Moneda,
    Porcentaje,
    Fecha
}

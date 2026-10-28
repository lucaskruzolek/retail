namespace Retail.Infrastructure.Persistence.Configurations;

/// <summary>
/// Cotejamientos (collations) de SQL Server usados en columnas de texto con reglas de comparación propias.
/// </summary>
internal static class Cotejamientos
{
    /// <summary>
    /// Para columnas que el usuario busca por texto (descripciones y razones sociales): no distingue mayúsculas
    /// ni acentos ("lapiz" encuentra "Lápiz"), pero sí distingue la ñ como letra propia ("ano" no encuentra "año").
    /// <c>Latin1_General_CI_AI</c> se descartó porque trata la ñ como una n con tilde. La collation por defecto del
    /// servidor (<c>SQL_Latin1_General_CP1_CI_AS</c>) distingue acentos.
    /// </summary>
    public const string BusquedaEnCastellano = "Modern_Spanish_CI_AI";

    /// <summary>
    /// Para comparar un nombre guardado con su grafía contra un texto en la forma de comparación del motor de
    /// catalogación (<c>ReglasClasificacion.NormalizarTexto</c>: mayúsculas y sin diacríticos). Se aplica con
    /// <c>EF.Functions.Collate</c> en la consulta, no en la columna: justamente porque trata la ñ como una n con tilde,
    /// igual que <c>NormalizarTexto</c>, y <see cref="BusquedaEnCastellano"/> no ("ÑANDU" ≠ "NANDU").
    /// </summary>
    public const string FormaDeComparacion = "Latin1_General_CI_AI";
}

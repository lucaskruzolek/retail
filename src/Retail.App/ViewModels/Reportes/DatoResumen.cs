namespace Retail.App.ViewModels.Reportes;

/// <summary>Un dato de la franja de totales al pie de una grilla (por ejemplo, "Capital inmovilizado: $ 1.200,00").</summary>
/// <param name="Etiqueta">Nombre del dato.</param>
/// <param name="Valor">Valor ya formateado en es-AR.</param>
public sealed record DatoResumen(string Etiqueta, string Valor);

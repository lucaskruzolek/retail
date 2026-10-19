namespace Retail.App.ViewModels.Reportes;

/// <summary>Variación de un indicador lista para mostrar: el texto con flecha y signo, y el estado que elige el badge.</summary>
/// <param name="Texto">Por ejemplo, "▲ +12,50 % vs período anterior".</param>
/// <param name="Estado">Sentido de la variación.</param>
public sealed record VariacionIndicador(string Texto, EstadoVariacionEnum Estado);

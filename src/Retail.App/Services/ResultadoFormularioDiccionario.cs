namespace Retail.App.Services;

/// <summary>Lo que el usuario cargó en un formulario de diccionario: el texto y el Id de la opción elegida (si hay).</summary>
public sealed record ResultadoFormularioDiccionario(string Texto, int? IdOpcion);

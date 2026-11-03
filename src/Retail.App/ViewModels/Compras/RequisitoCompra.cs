namespace Retail.App.ViewModels.Compras;

/// <summary>
/// Un ítem de la lista de comprobación de la compra: qué falta o qué ya está cumplido para poder registrarla.
/// </summary>
/// <param name="Texto">Lo que se le pide al operador, ya redactado para mostrar.</param>
/// <param name="Cumplido">Verdadero si el requisito ya se satisface.</param>
public sealed record RequisitoCompra(string Texto, bool Cumplido);

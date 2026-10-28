namespace Retail.App.ViewModels.Proveedores;

/// <summary>
/// Opción del combo de destino de una equivalencia: una categoría o marca del comercio, o "Ignorar" cuando
/// <see cref="IdDestino"/> es nulo (M-32).
/// </summary>
public sealed record OpcionDestinoEquivalencia(int? IdDestino, string Nombre);

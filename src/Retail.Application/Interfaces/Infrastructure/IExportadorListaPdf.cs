using Retail.Application.DTOs.Listas;

namespace Retail.Application.Interfaces.Infrastructure;

/// <summary>
/// Genera el PDF A4 de una lista de artículos. Escribe en el <see cref="Stream"/> recibido y no lo cierra, igual que
/// <see cref="IExportadorPresupuestoPdf"/>: la ruta la elige la interfaz, de modo que un fallo de escritura se reintenta.
/// </summary>
public interface IExportadorListaPdf
{
    /// <summary>
    /// Escribe la lista en <paramref name="destino"/>. Solo se imprimen los renglones disponibles (L-13) y se agrega la
    /// leyenda "Precios al dd/mm/aaaa, sujetos a modificación" con <paramref name="fechaPrecios"/>.
    /// </summary>
    Task ExportarAsync(ListaDto lista, DateOnly fechaPrecios, Stream destino, CancellationToken cancellationToken = default);
}

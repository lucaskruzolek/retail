using Retail.Application.DTOs.Listas;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Casos de uso de las listas de artículos (RF-30). Vender una lista no está acá: el POS carga los renglones y cobra
/// como una venta común a precio de catálogo (L-08); la venta solo guarda <c>IdListaOrigen</c> (L-09).
/// </summary>
public interface IListaService
{
    /// <summary>Crea una lista. Rechaza artículos dados de baja y nombres repetidos entre listas activas (L-10).</summary>
    Task<ListaDto> CrearAsync(CrearListaDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reemplaza el nombre y los renglones de una lista. Se rechaza si la lista cambió desde que la pantalla la leyó
    /// (<see cref="ActualizarListaDto.FechaModificacionLeida"/>). Un artículo dado de baja que ya figuraba en la lista
    /// se conserva; no se puede agregar uno nuevo que esté dado de baja.
    /// </summary>
    Task<ListaDto> ActualizarAsync(int idLista, ActualizarListaDto dto, CancellationToken cancellationToken = default);

    /// <summary>Da de baja lógicamente la lista (Ley 3, L-17); las ventas ya registradas conservan su origen.</summary>
    Task EliminarAsync(int idLista, CancellationToken cancellationToken = default);

    /// <summary>Lista las listas con búsqueda y paginación en servidor.</summary>
    Task<ListasPaginadosDto> ListarAsync(ConsultaListasDto consulta, CancellationToken cancellationToken = default);

    /// <summary>Detalle de una lista, o <c>null</c> si no existe.</summary>
    Task<ListaDto?> ObtenerAsync(int idLista, CancellationToken cancellationToken = default);

    /// <summary>
    /// Arma lo que el POS necesita para vender la lista: renglones disponibles a precio de catálogo de hoy, faltantes
    /// de stock y artículos dados de baja. No modifica nada.
    /// </summary>
    Task<ListaParaVentaDto> ObtenerParaVentaAsync(int idLista, CancellationToken cancellationToken = default);

    /// <summary>Imprime la lista en el ticket térmico con los precios de hoy.</summary>
    Task ImprimirAsync(int idLista, CancellationToken cancellationToken = default);

    /// <summary>Exporta la lista a PDF en <paramref name="destino"/> sin cerrarlo.</summary>
    Task ExportarPdfAsync(int idLista, Stream destino, CancellationToken cancellationToken = default);
}

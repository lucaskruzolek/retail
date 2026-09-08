using Retail.Application.DTOs.Proveedores;

namespace Retail.Application.Interfaces.Infrastructure;

/// <summary>
/// Contrato para el procesamiento streaming en segundo plano de planillas de distribuidores (MiniExcel).
/// </summary>
public interface IExcelCatalogParser
{
    Task<IReadOnlyList<ItemCatalogoImportadoDto>> ParsearCatalogoAsync(
        Stream stream,
        MapeoColumnasDto mapeo,
        IProgress<int>? progreso = null,
        CancellationToken cancellationToken = default);
}

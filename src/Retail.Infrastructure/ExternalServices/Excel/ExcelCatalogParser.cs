using System.Globalization;
using MiniExcelLibs;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Infrastructure;

namespace Retail.Infrastructure.ExternalServices.Excel;

/// <summary>
/// Implementación streaming del analizador de planillas de catálogos mayoristas usando MiniExcel (RF-07, RNF-03).
/// Procesa de forma diferida las filas sin materializar estructuras innecesarias en memoria.
/// </summary>
public class ExcelCatalogParser : IExcelCatalogParser
{
    public async Task<IReadOnlyList<ItemCatalogoImportadoDto>> ParsearCatalogoAsync(
        Stream stream,
        MapeoColumnasDto mapeo,
        IProgress<int>? progreso = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(mapeo);

        var items = new List<ItemCatalogoImportadoDto>();

        await Task.Run(() =>
        {
            var rows = stream.Query(useHeaderRow: true);
            int totalFilasLeidas = 0;

            foreach (IDictionary<string, object> row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                totalFilasLeidas++;

                // 1. Extraer código de proveedor
                if (!row.TryGetValue(mapeo.ColumnaCodigo, out var valCodigo) || valCodigo is null)
                {
                    continue;
                }

                string codigo = valCodigo.ToString()?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(codigo))
                {
                    continue;
                }

                // 2. Extraer descripción
                if (!row.TryGetValue(mapeo.ColumnaDescripcion, out var valDesc) || valDesc is null)
                {
                    continue;
                }

                string descripcion = valDesc.ToString()?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(descripcion))
                {
                    continue;
                }

                // 3. Extraer costo de reposición
                if (!row.TryGetValue(mapeo.ColumnaPrecioCosto, out var valPrecio) || valPrecio is null)
                {
                    continue;
                }

                decimal precioCosto;
                if (valPrecio is double d)
                {
                    precioCosto = Convert.ToDecimal(d);
                }
                else if (valPrecio is decimal dec)
                {
                    precioCosto = dec;
                }
                else if (valPrecio is int i)
                {
                    precioCosto = i;
                }
                else if (valPrecio is long l)
                {
                    precioCosto = l;
                }
                else
                {
                    string precioStr = valPrecio.ToString()?.Trim().Replace("$", "").Trim() ?? string.Empty;
                    if (!decimal.TryParse(precioStr, NumberStyles.Any, CultureInfo.InvariantCulture, out precioCosto) &&
                        !decimal.TryParse(precioStr, NumberStyles.Any, new CultureInfo("es-AR"), out precioCosto))
                    {
                        continue;
                    }
                }

                if (precioCosto < 0m)
                {
                    continue;
                }

                // 4. Extraer código de barras opcional
                string? codigoBarras = null;
                if (!string.IsNullOrWhiteSpace(mapeo.ColumnaCodigoBarras) &&
                    row.TryGetValue(mapeo.ColumnaCodigoBarras, out var valBarras) && valBarras is not null)
                {
                    codigoBarras = valBarras.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(codigoBarras))
                    {
                        codigoBarras = null;
                    }
                }

                items.Add(new ItemCatalogoImportadoDto
                {
                    CodigoProveedor = codigo,
                    CodigoBarras = codigoBarras,
                    Descripcion = descripcion,
                    PrecioCosto = Math.Round(precioCosto, 2)
                });

                if (totalFilasLeidas % 250 == 0)
                {
                    progreso?.Report(totalFilasLeidas);
                }
            }

            progreso?.Report(totalFilasLeidas);
        }, cancellationToken);

        return items;
    }
}

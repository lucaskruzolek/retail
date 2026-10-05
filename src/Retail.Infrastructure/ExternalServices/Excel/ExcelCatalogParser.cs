using System.Globalization;
using System.Text;
using MiniExcelLibs;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Infrastructure;

namespace Retail.Infrastructure.ExternalServices.Excel;

/// <summary>
/// Implementación streaming del analizador de planillas de catálogos mayoristas usando MiniExcel (RF-07, RNF-03).
/// Procesa de forma diferida las filas sin materializar estructuras innecesarias en memoria.
/// </summary>
/// <remarks>
/// La planilla se lee sin cabecera automática: cada fila llega como un diccionario "letra de columna → valor".
/// Así el encabezado puede estar en cualquier fila (<see cref="MapeoColumnasDto.FilaEncabezado"/>) y cada dato
/// conserva su número de fila real, el mismo que el usuario ve en Excel.
/// </remarks>
public class ExcelCatalogParser : IExcelCatalogParser
{
    private const int IntervaloReporteProgreso = 250;

    public async Task<ResultadoParseoCatalogoDto> ParsearCatalogoAsync(
        Stream stream,
        MapeoColumnasDto mapeo,
        IProgress<int>? progreso = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(mapeo);

        var items = new List<ItemCatalogoImportadoDto>();
        var descartadas = new List<string>();

        await Task.Run(() =>
        {
            var tipoArchivo = mapeo.ExtensionArchivo.Equals(".csv", StringComparison.OrdinalIgnoreCase)
                ? ExcelType.CSV
                : ExcelType.XLSX;
            var rows = stream.Query(useHeaderRow: false, excelType: tipoArchivo);

            ColumnasPlanilla? columnas = null;
            int numeroFila = 0;

            foreach (IDictionary<string, object?> row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                numeroFila++;

                if (numeroFila < mapeo.FilaEncabezado)
                {
                    continue;
                }

                if (numeroFila == mapeo.FilaEncabezado)
                {
                    columnas = ResolverColumnas(row, mapeo);
                    continue;
                }

                if (columnas is not null)
                {
                    InterpretarFila(row, numeroFila, columnas, items, descartadas);
                }

                if (numeroFila % IntervaloReporteProgreso == 0)
                {
                    progreso?.Report(numeroFila);
                }
            }

            if (columnas is null)
            {
                throw new InvalidDataException(
                    $"La planilla no llega a la fila de encabezado {mapeo.FilaEncabezado} (tiene {numeroFila} filas).");
            }

            progreso?.Report(numeroFila);
        }, cancellationToken);

        return new ResultadoParseoCatalogoDto
        {
            Items = items,
            FilasDescartadas = descartadas
        };
    }

    /// <summary>
    /// Ubica en la fila de encabezado la letra de cada columna mapeada. La comparación ignora mayúsculas,
    /// acentos y espacios en los extremos ("Código " equivale a "CODIGO"). Si falta una columna obligatoria
    /// se aborta la lectura: sin ella, todas las filas se descartarían por el mismo motivo.
    /// </summary>
    private static ColumnasPlanilla ResolverColumnas(IDictionary<string, object?> filaEncabezado, MapeoColumnasDto mapeo)
    {
        var letraPorNombre = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var nombresOriginales = new List<string>();
        foreach (var (letra, valor) in filaEncabezado)
        {
            string nombre = valor?.ToString()?.Trim() ?? string.Empty;
            if (nombre.Length > 0)
            {
                letraPorNombre.TryAdd(NormalizarNombreColumna(nombre), letra);
                nombresOriginales.Add(nombre);
            }
        }

        string BuscarObligatoria(string nombreColumna)
        {
            if (letraPorNombre.TryGetValue(NormalizarNombreColumna(nombreColumna), out var letra))
            {
                return letra;
            }

            string encontradas = nombresOriginales.Count > 0 ? string.Join(", ", nombresOriginales) : "ninguna";
            throw new InvalidDataException(
                $"No se encontró la columna '{nombreColumna.Trim()}' en la fila de encabezado {mapeo.FilaEncabezado}. " +
                $"Columnas encontradas: {encontradas}.");
        }

        // El código de barras es opcional: si la columna no existe en la planilla, se importa sin él.
        string? letraCodigoBarras = null;
        if (!string.IsNullOrWhiteSpace(mapeo.ColumnaCodigoBarras) &&
            letraPorNombre.TryGetValue(NormalizarNombreColumna(mapeo.ColumnaCodigoBarras), out var letraBarras))
        {
            letraCodigoBarras = letraBarras;
        }

        return new ColumnasPlanilla(
            BuscarObligatoria(mapeo.ColumnaCodigo),
            BuscarObligatoria(mapeo.ColumnaDescripcion),
            BuscarObligatoria(mapeo.ColumnaPrecioCosto),
            letraCodigoBarras);
    }

    /// <summary>
    /// Convierte una fila de datos en un ítem, o registra el motivo por el que se descarta (H-03).
    /// Las filas completamente vacías se ignoran sin reportarlas: suelen ser el relleno al final de la hoja.
    /// </summary>
    private static void InterpretarFila(
        IDictionary<string, object?> row,
        int numeroFila,
        ColumnasPlanilla columnas,
        List<ItemCatalogoImportadoDto> items,
        List<string> descartadas)
    {
        string codigo = LeerTexto(row, columnas.Codigo);
        string descripcion = LeerTexto(row, columnas.Descripcion);
        string precioTexto = LeerTexto(row, columnas.PrecioCosto);

        if (codigo.Length == 0 && descripcion.Length == 0 && precioTexto.Length == 0)
        {
            return;
        }

        string? motivo = null;
        decimal precioCosto = 0m;

        if (codigo.Length == 0)
        {
            motivo = "el código está vacío.";
        }
        else if (descripcion.Length == 0)
        {
            motivo = "la descripción está vacía.";
        }
        else if (precioTexto.Length == 0)
        {
            motivo = "el precio de costo está vacío.";
        }
        else if (!TryLeerPrecio(row[columnas.PrecioCosto]!, out precioCosto))
        {
            motivo = $"el precio '{precioTexto}' no es un importe válido.";
        }
        else if (precioCosto < 0m)
        {
            motivo = $"el precio de costo es negativo ({precioTexto}).";
        }

        if (motivo is not null)
        {
            descartadas.Add($"Fila {numeroFila}: {motivo}");
            return;
        }

        string? codigoBarras = null;
        if (columnas.CodigoBarras is not null)
        {
            string barras = LeerTexto(row, columnas.CodigoBarras);
            codigoBarras = barras.Length > 0 ? barras : null;
        }

        items.Add(new ItemCatalogoImportadoDto
        {
            NumeroFila = numeroFila,
            CodigoProveedor = codigo,
            CodigoBarras = codigoBarras,
            Descripcion = descripcion,
            PrecioCosto = Math.Round(precioCosto, 2)
        });
    }

    /// <summary>
    /// Quita los espacios de los extremos y los acentos de un nombre de columna ("Código " → "Codigo").
    /// La descomposición Unicode (FormD) separa cada letra de su tilde; luego se descartan las tildes
    /// (categoría NonSpacingMark). Las mayúsculas las resuelve el comparador OrdinalIgnoreCase.
    /// </summary>
    private static string NormalizarNombreColumna(string nombre)
    {
        string descompuesto = nombre.Trim().Normalize(NormalizationForm.FormD);
        var sinAcentos = new StringBuilder(descompuesto.Length);
        foreach (char c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sinAcentos.Append(c);
            }
        }

        return sinAcentos.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string LeerTexto(IDictionary<string, object?> row, string letra)
    {
        return row.TryGetValue(letra, out var valor) && valor is not null
            ? valor.ToString()?.Trim() ?? string.Empty
            : string.Empty;
    }

    /// <summary>
    /// Interpreta la celda de precio: las celdas numéricas de Excel llegan tipadas; las de texto (y todas las
    /// de un CSV) se delegan a <see cref="ParseadorPrecioTexto"/> con la convención es-AR.
    /// </summary>
    private static bool TryLeerPrecio(object valor, out decimal precio)
    {
        switch (valor)
        {
            case double d:
                precio = Convert.ToDecimal(d);
                return true;
            case decimal dec:
                precio = dec;
                return true;
            case int i:
                precio = i;
                return true;
            case long l:
                precio = l;
                return true;
            default:
                return ParseadorPrecioTexto.TryParse(valor.ToString(), out precio);
        }
    }

    /// <summary>Letras de columna (A, B, C…) donde la planilla tiene cada dato mapeado.</summary>
    private sealed record ColumnasPlanilla(string Codigo, string Descripcion, string PrecioCosto, string? CodigoBarras);
}

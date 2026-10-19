using System.Data;
using System.Globalization;
using MiniExcelLibs;
using MiniExcelLibs.OpenXml;
using Retail.Application.DTOs.Reportes.Exportacion;

namespace Retail.Infrastructure.Reportes;

/// <summary>
/// Genera el .xlsx de un <see cref="DocumentoReporte"/> con MiniExcel (RF-26): una hoja "Reporte" con el encabezado y
/// una hoja por tabla. Los importes, cantidades y porcentajes se escriben como números reales (nunca como texto) para
/// que el usuario pueda operar con ellos. Limitación de MiniExcel 1.34.2: <c>DynamicColumns.Format</c> convierte el valor
/// a texto (o se ignora con <see cref="DataTable"/>) y es global al libro, así que no se usa; las fechas salen con
/// formato dd/MM/yyyy (formato por defecto de MiniExcel).
/// </summary>
internal static class ExportadorExcel
{
    internal const string NombreHojaEncabezado = "Reporte";
    private const int MaximoCaracteresNombreHoja = 31;

    public static void Exportar(DocumentoReporte documento, Stream destino)
    {
        var configuracion = new OpenXmlConfiguration
        {
            AutoFilter = false
        };

        var hojas = new Dictionary<string, object>();
        var nombresUsados = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { NombreHojaEncabezado };

        hojas[NombreHojaEncabezado] = ConstruirHojaEncabezado(documento);

        foreach (var tabla in documento.Tablas)
        {
            var nombre = ObtenerNombreHojaUnico(tabla.Titulo, nombresUsados);
            hojas[nombre] = ConstruirHojaTabla(tabla);
        }

        MiniExcel.SaveAs(destino, hojas, printHeader: true, configuration: configuracion);
    }

    /// <summary>
    /// Nombre válido de hoja de Excel: máximo 31 caracteres, sin <c>[ ] : * ? / \</c>, y único dentro del libro
    /// (el segundo repetido recibe el sufijo " (2)", el tercero " (3)", etc.).
    /// </summary>
    internal static string ObtenerNombreHojaUnico(string titulo, ISet<string> nombresUsados)
    {
        var saneado = new string(titulo.Where(c => "[]:*?/\\".IndexOf(c, StringComparison.Ordinal) < 0).ToArray()).Trim();
        if (saneado.Length == 0)
        {
            saneado = "Tabla";
        }

        var candidato = Recortar(saneado, MaximoCaracteresNombreHoja);
        var contador = 2;
        while (!nombresUsados.Add(candidato))
        {
            var sufijo = $" ({contador.ToString(CultureInfo.InvariantCulture)})";
            candidato = Recortar(saneado, MaximoCaracteresNombreHoja - sufijo.Length) + sufijo;
            contador++;
        }

        return candidato;
    }

    private static string Recortar(string texto, int maximo)
    {
        return texto.Length <= maximo ? texto : texto[..maximo].TrimEnd();
    }

    private static DataTable ConstruirHojaEncabezado(DocumentoReporte documento)
    {
        var hoja = new DataTable(NombreHojaEncabezado);
        hoja.Columns.Add("Campo", typeof(string));
        hoja.Columns.Add("Valor", typeof(string));

        hoja.Rows.Add("Reporte", documento.Titulo);
        hoja.Rows.Add("Generado el", documento.GeneradoEl.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture));
        hoja.Rows.Add("Generado por", documento.GeneradoPor);
        foreach (var filtro in documento.Filtros)
        {
            hoja.Rows.Add(filtro.Nombre, filtro.Valor);
        }

        return hoja;
    }

    /// <summary>
    /// Una <see cref="DataTable"/> tipada por hoja: los nombres de las columnas son los encabezados (únicos dentro de la
    /// tabla; si se repiten, sufijo " (2)") y, aunque no haya filas, MiniExcel escribe la fila de encabezados.
    /// </summary>
    private static DataTable ConstruirHojaTabla(TablaReporte tabla)
    {
        var hoja = new DataTable();
        var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var columna in tabla.Columnas)
        {
            var nombre = columna.Encabezado;
            var contador = 2;
            while (!nombres.Add(nombre))
            {
                nombre = $"{columna.Encabezado} ({contador.ToString(CultureInfo.InvariantCulture)})";
                contador++;
            }

            hoja.Columns.Add(nombre, TipoClr(columna.Tipo));
        }

        foreach (var fila in tabla.Filas)
        {
            var valores = new object[tabla.Columnas.Count];
            for (var i = 0; i < valores.Length; i++)
            {
                valores[i] = Convertir(tabla.Columnas[i].Tipo, i < fila.Count ? fila[i] : null);
            }

            hoja.Rows.Add(valores);
        }

        return hoja;
    }

    private static Type TipoClr(TipoColumnaEnum tipo)
    {
        return tipo switch
        {
            TipoColumnaEnum.Entero => typeof(int),
            TipoColumnaEnum.Moneda or TipoColumnaEnum.Porcentaje => typeof(decimal),
            TipoColumnaEnum.Fecha => typeof(DateTime),
            _ => typeof(string)
        };
    }

    /// <summary>Convierte la celda tipada del modelo al valor CLR de la columna; <c>null</c> queda como celda vacía.</summary>
    private static object Convertir(TipoColumnaEnum tipo, object? valor)
    {
        if (valor is null)
        {
            return DBNull.Value;
        }

        return tipo switch
        {
            TipoColumnaEnum.Entero => Convert.ToInt32(valor, CultureInfo.InvariantCulture),
            TipoColumnaEnum.Moneda => Convert.ToDecimal(valor, CultureInfo.InvariantCulture),

            // Sin formato de celda no hay "%": se escribe tal como viene del modelo (25,5 = 25,5 %); el encabezado ya dice "%".
            TipoColumnaEnum.Porcentaje => Convert.ToDecimal(valor, CultureInfo.InvariantCulture),
            TipoColumnaEnum.Fecha => valor is DateOnly fecha ? fecha.ToDateTime(TimeOnly.MinValue) : Convert.ToDateTime(valor, CultureInfo.InvariantCulture),
            _ => Convert.ToString(valor, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }
}

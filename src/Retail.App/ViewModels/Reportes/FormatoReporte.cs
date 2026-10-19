using System.Globalization;
using Retail.Application.DTOs.Reportes;

namespace Retail.App.ViewModels.Reportes;

/// <summary>
/// Formato de presentación de los reportes con cultura es-AR explícita, igual que el PDF (D-23): lo que se ve en
/// pantalla no depende de la configuración regional del equipo.
/// </summary>
public static class FormatoReporte
{
    // Flechas y signo menos tipográfico (U+2212): el color nunca es la única señal de una variación.
    private const string FlechaSube = "▲";
    private const string FlechaBaja = "▼";
    private const string SignoMenos = "−";

    public const string SinBase = "Sin datos del período anterior";

    public static CultureInfo Cultura { get; } = new("es-AR");

    public static string Moneda(decimal valor) => valor.ToString("$ #,##0.00", Cultura);

    // Umbrales del formato compacto de los ejes de valores: en un gráfico angosto "20.000.000" no entra.
    private const double UmbralMillones = 1_000_000;
    private const double UmbralMiles = 10_000;

    // Con algún dato no entero y todos por debajo de este umbral las etiquetas del eje llevan dos decimales.
    private const double RangoConDecimales = 10;

    /// <summary>
    /// Etiqueta de un valor del eje de valores de un gráfico en pantalla. Con un máximo de un millón o más es compacta
    /// ("20 M", "2,5 M"); desde diez mil, en miles ("15 mil"); por debajo, completa: dos decimales solo si algún dato no
    /// es entero y el máximo es menor a 10, y enteros en cualquier otro caso.
    /// </summary>
    /// <param name="valor">Valor del separador del eje.</param>
    /// <param name="maximoAbsoluto">Mayor valor absoluto de los datos; decide la escala de todas las etiquetas del eje.</param>
    /// <param name="datosEnteros">Todos los datos son enteros.</param>
    public static string EtiquetaEje(double valor, double maximoAbsoluto, bool datosEnteros)
    {
        if (maximoAbsoluto >= UmbralMillones)
        {
            return Escalar(valor / UmbralMillones, " M");
        }

        if (maximoAbsoluto >= UmbralMiles)
        {
            return Escalar(valor / 1000, " mil");
        }

        var formato = !datosEnteros && maximoAbsoluto < RangoConDecimales ? "N2" : "N0";
        return valor.ToString(formato, Cultura);
    }

    // Cero va sin sufijo ("0"); un valor entero en la escala, sin decimales ("20 M"); si no, un decimal ("2,5 M").
    private static string Escalar(double valor, string sufijo)
    {
        if (valor == 0)
        {
            return "0";
        }

        var redondeado = Math.Round(valor, 1);
        var formato = redondeado == Math.Floor(redondeado) ? "N0" : "N1";
        return redondeado.ToString(formato, Cultura) + sufijo;
    }

    public static string Entero(long valor) => valor.ToString("N0", Cultura);

    public static string Porcentaje(decimal valor) => valor.ToString("N2", Cultura) + " %";

    /// <summary>Etiqueta de un punto del gráfico de evolución: "dd/MM" para días y semanas (lunes), "MM/yyyy" para meses.</summary>
    public static string EtiquetaPeriodo(DateOnly inicio, AgrupacionTemporalEnum agrupacion)
    {
        return inicio.ToString(agrupacion == AgrupacionTemporalEnum.Mes ? "MM/yyyy" : "dd/MM", Cultura);
    }

    /// <summary>Variación porcentual relativa de un indicador; <c>null</c> significa que el período anterior fue cero.</summary>
    public static VariacionIndicador Variacion(decimal? porcentaje)
    {
        if (porcentaje is null)
        {
            return new VariacionIndicador(SinBase, EstadoVariacionEnum.SinBase);
        }

        var magnitud = Math.Abs(porcentaje.Value).ToString("N2", Cultura);

        if (porcentaje > 0)
        {
            return new VariacionIndicador($"{FlechaSube} +{magnitud} % vs período anterior", EstadoVariacionEnum.Sube);
        }

        if (porcentaje < 0)
        {
            return new VariacionIndicador($"{FlechaBaja} {SignoMenos}{magnitud} % vs período anterior", EstadoVariacionEnum.Baja);
        }

        return new VariacionIndicador($"= {magnitud} % vs período anterior", EstadoVariacionEnum.SinBase);
    }
}

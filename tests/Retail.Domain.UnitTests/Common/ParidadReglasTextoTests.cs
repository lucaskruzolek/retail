using System.Text.Json;
using FluentAssertions;
using Retail.Domain.Common;
using Xunit;

namespace Retail.Domain.UnitTests.Common;

/// <summary>
/// Paridad entre Python y C# de las reglas de texto de la clasificación (Track 6.6, sección 10, R-5). Los casos los
/// genera <c>ml/scripts/reglas_paridad.py</c> con descripciones inventadas: si una regla cambia en un lado y no en el
/// otro, estos tests fallan. Para regenerarlos: <c>python reglas_paridad.py --salida ../paridad/reglas-texto-referencia.json</c>
/// desde <c>ml/scripts</c>.
/// </summary>
public class ParidadReglasTextoTests
{
    private static readonly JsonElement Referencia = CargarReferencia();

    public static TheoryData<string> Descripciones()
    {
        var datos = new TheoryData<string>();
        foreach (var caso in Referencia.GetProperty("descripciones").EnumerateArray())
        {
            datos.Add(caso.GetProperty("descripcion").GetString()!);
        }

        return datos;
    }

    public static TheoryData<string, string> CodigosDeBarras()
    {
        var datos = new TheoryData<string, string>();
        foreach (var caso in Referencia.GetProperty("codigos_de_barras").EnumerateArray())
        {
            datos.Add(caso.GetProperty("codigo_interno").GetString()!, caso.GetProperty("codigo").GetString()!);
        }

        return datos;
    }

    [Theory]
    [MemberData(nameof(Descripciones))]
    public void ReglasDeDescripcion_CasoDeReferencia_CoincidenConPython(string descripcion)
    {
        var esperado = BuscarCaso("descripciones", "descripcion", descripcion);

        ReglasClasificacion.LimpiarDescripcion(descripcion).Should().Be(esperado.GetProperty("limpia").GetString());
        ReglasClasificacion.PrimeraPalabra(descripcion).Should().Be(esperado.GetProperty("primera_palabra").GetString());
        ReglasClasificacion.TerminoDeDescripcion(descripcion).Should().Be(esperado.GetProperty("termino").GetString());
        ReglasClasificacion.PalabrasParaMarca(descripcion).Should().Equal(
            esperado.GetProperty("palabras_para_marca").EnumerateArray().Select(p => p.GetString()!));
    }

    [Theory]
    [MemberData(nameof(CodigosDeBarras))]
    public void NormalizarGtin_CasoDeReferencia_CoincideConPython(string codigoInterno, string codigo)
    {
        var esperado = BuscarCaso("codigos_de_barras", "codigo", codigo).GetProperty("gtin");

        // Python también descarta el EAN igual al código interno; en C# esa regla es de Aplicación (depende de la
        // planilla), así que ese caso se compara aplicándola acá.
        var gtin = codigo == codigoInterno ? null : ReglasCodigoBarras.NormalizarGtin(codigo);

        gtin.Should().Be(esperado.ValueKind == JsonValueKind.Null ? null : esperado.GetString());
    }

    private static JsonElement BuscarCaso(string coleccion, string clave, string valor)
    {
        return Referencia.GetProperty(coleccion).EnumerateArray().Single(c => c.GetProperty(clave).GetString() == valor);
    }

    private static JsonElement CargarReferencia()
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "Paridad", "reglas-texto-referencia.json");
        using var documento = JsonDocument.Parse(File.ReadAllText(ruta));
        return documento.RootElement.Clone();
    }
}

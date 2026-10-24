using FluentAssertions;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Services.Clasificacion;
using Retail.Domain.Common;
using Xunit;

namespace Retail.Application.UnitTests.Services.Clasificacion;

public class BuscadorDeMarcasTests
{
    private static readonly BuscadorDeMarcas Buscador = new(new[]
    {
        Marca(1, "Bic"),
        Marca(2, "Artística Dibu"),
        Marca(3, "Husares", "HUSAR"),
        Marca(4, "Koby"),
        Marca(5, "Data")
    });

    [Theory]
    [InlineData("LAPIZ NEGRO BIC", 1)]
    [InlineData("SET ESCOLAR ARTISTICA DIBU X 6", 2)]
    [InlineData("REGLA HUSAR 30 CM", 3)]
    public void MarcasEn_NombreAliasOFraseCompleta_EncuentraLaMarca(string descripcion, int esperada)
    {
        Buscador.MarcasEn(ReglasClasificacion.PalabrasParaMarca(descripcion)).Should().Equal(esperada);
    }

    [Theory]
    [InlineData("BIC LAPIZ NEGRO")]
    [InlineData("LAPIZ BICOLOR AZUL")]
    [InlineData("SET ESCOLAR DIBU X 6")]
    public void MarcasEn_PrimeraPalabraParteDeOtraOFraseIncompleta_NoEncuentraNada(string descripcion)
    {
        // La primera palabra es el término; "BIC" no está en "BICOLOR"; "DIBU" solo no es "ARTISTICA DIBU".
        Buscador.MarcasEn(ReglasClasificacion.PalabrasParaMarca(descripcion)).Should().BeEmpty();
    }

    [Fact]
    public void MarcasEn_DosMarcasPegadasConBarra_EncuentraAmbas()
    {
        Buscador.MarcasEn(ReglasClasificacion.PalabrasParaMarca("LAPIZ NEGRO KOBY/DATA.")).Should().HaveCount(2).And.Contain(4).And.Contain(5);
    }

    [Fact]
    public void BuscarExacta_FormaNormalizada_DevuelveLaMarca()
    {
        Buscador.BuscarExacta("ARTISTICA DIBU").Should().Be(2);
        Buscador.BuscarExacta("DIBU").Should().BeNull();
    }

    [Fact]
    public void Constructor_FormaCompartidaPorDosMarcas_NoSugiereNinguna()
    {
        var buscador = new BuscadorDeMarcas(new[] { Marca(1, "Norte"), Marca(2, "Papelera Sur", "NORTE") });

        buscador.BuscarExacta("NORTE").Should().BeNull();
        buscador.MarcasEn(ReglasClasificacion.PalabrasParaMarca("CUADERNO NORTE")).Should().BeEmpty();
        buscador.BuscarExacta("PAPELERA SUR").Should().Be(2);
    }

    private static MarcaDiccionarioDto Marca(int id, string nombre, params string[] alias)
    {
        return new MarcaDiccionarioDto { IdMarca = id, NombreMarca = nombre, Alias = alias };
    }
}

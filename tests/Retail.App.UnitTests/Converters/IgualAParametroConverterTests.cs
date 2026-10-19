using System.Globalization;
using System.Windows.Data;
using FluentAssertions;
using Retail.App.Converters;
using Retail.App.ViewModels.Reportes;
using Retail.Application.DTOs.Reportes;
using Xunit;

namespace Retail.App.UnitTests.Converters;

public class IgualAParametroConverterTests
{
    private readonly IgualAParametroConverter _converter = new();

    [Fact]
    public void Convert_ValorIgualAlParametro_MarcaElBoton()
    {
        // Act
        var marcado = _converter.Convert(TipoReporteEnum.Ranking, typeof(bool), TipoReporteEnum.Ranking, CultureInfo.InvariantCulture);

        // Assert
        marcado.Should().Be(true);
    }

    [Fact]
    public void Convert_ValorDistintoDelParametro_DejaElBotonSinMarcar()
    {
        // Act
        var marcado = _converter.Convert(TipoReporteEnum.Tablero, typeof(bool), TipoReporteEnum.Ranking, CultureInfo.InvariantCulture);

        // Assert
        marcado.Should().Be(false);
    }

    [Fact]
    public void ConvertBack_AlMarcarse_EscribeElParametroEnLaPropiedad()
    {
        // Act
        var valor = _converter.ConvertBack(true, typeof(TipoReporteEnum), TipoReporteEnum.Rentabilidad, CultureInfo.InvariantCulture);

        // Assert
        valor.Should().Be(TipoReporteEnum.Rentabilidad);
    }

    [Fact]
    public void ConvertBack_AlDesmarcarse_NoEscribeNada()
    {
        // Act
        var valor = _converter.ConvertBack(false, typeof(TipoReporteEnum), TipoReporteEnum.Rentabilidad, CultureInfo.InvariantCulture);

        // Assert: otro botón del grupo se marcó, y es ese el que escribe
        valor.Should().Be(Binding.DoNothing);
    }

    [Theory]
    [InlineData(AgrupacionTemporalEnum.Dia, "Día")]
    [InlineData(AgrupacionTemporalEnum.Semana, "Semana")]
    [InlineData(AgrupacionTemporalEnum.Mes, "Mes")]
    [InlineData(CriterioRankingEnum.Unidades, "Unidades")]
    [InlineData(CriterioRankingEnum.Importe, "Importe")]
    [InlineData(AgrupacionRentabilidadEnum.Categoria, "Categoría")]
    [InlineData(AgrupacionRentabilidadEnum.Marca, "Marca")]
    [InlineData(AgrupacionRentabilidadEnum.Articulo, "Artículo")]
    public void ATexto_EnumsDeReportes_DevuelvenTextoLegible(Enum valor, string esperado)
    {
        // Act + Assert
        EnumATextoConverter.ATexto(valor).Should().Be(esperado);
    }
}

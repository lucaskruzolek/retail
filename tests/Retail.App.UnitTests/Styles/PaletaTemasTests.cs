using System.Windows;
using System.Windows.Media;
using FluentAssertions;
using Retail.App.UnitTests.Helpers;
using Xunit;

namespace Retail.App.UnitTests.Styles;

/// <summary>
/// Verifica que los dos temas definan exactamente las mismas claves de color y que las fuentes embebidas se
/// resuelvan desde el ensamblado (y no caigan silenciosamente en una fuente instalada en el equipo).
/// </summary>
public class PaletaTemasTests
{
    private const string RutaEstilos = "pack://application:,,,/Retail.App;component/Styles/";

    [Fact]
    public void ColorsDark_ClavesIgualesALight_NoFaltaNinguna()
    {
        var (soloClaro, soloOscuro) = WpfTestHelper.Run(() =>
        {
            var claro = CargarClaves("Colors.xaml");
            var oscuro = CargarClaves("Colors.Dark.xaml");
            return (claro.Except(oscuro).Order().ToList(), oscuro.Except(claro).Order().ToList());
        });

        soloClaro.Should().BeEmpty("faltan en Colors.Dark.xaml: {0}", string.Join(", ", soloClaro));
        soloOscuro.Should().BeEmpty("sobran en Colors.Dark.xaml: {0}", string.Join(", ", soloOscuro));
    }

    [Theory]
    [InlineData("FontFamilyText", "DM Sans")]
    [InlineData("FontFamilyDisplay", "DM Sans Display")]
    [InlineData("FontFamilyMonospace", "JetBrains Mono")]
    public void Typography_FuenteEmbebida_SeResuelveDesdeElEnsamblado(string clave, string familiaEsperada)
    {
        var (encontrada, nombres) = WpfTestHelper.Run(() =>
        {
            var familia = (FontFamily)System.Windows.Application.Current.Resources[clave];
            var tipografia = new Typeface(familia, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var ok = tipografia.TryGetGlyphTypeface(out var glifos);
            return (ok, ok ? glifos.FamilyNames.Values.ToList() : new List<string>());
        });

        encontrada.Should().BeTrue("la fuente embebida debe poder cargarse");
        nombres.Should().Contain(familiaEsperada, "si no, WPF cayó en una fuente de respaldo");
    }

    private static HashSet<object> CargarClaves(string archivo)
    {
        var diccionario = new ResourceDictionary { Source = new Uri(RutaEstilos + archivo, UriKind.Absolute) };
        return diccionario.Keys.Cast<object>().ToHashSet();
    }
}

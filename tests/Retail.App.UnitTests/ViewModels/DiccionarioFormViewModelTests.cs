using FluentAssertions;
using Retail.App.Services;
using Retail.App.ViewModels.Articulos;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class DiccionarioFormViewModelTests
{
    private static readonly OpcionDiccionario Ambiguo = new(null, "Ambiguo (sin categoría)");
    private static readonly OpcionDiccionario Cuadernos = new(10, "Cuadernos");

    private static DiccionarioFormViewModel Crear(bool opcionObligatoria, int? idOpcionInicial = null, string textoInicial = "")
    {
        return new DiccionarioFormViewModel(
            "Título",
            "Término *",
            textoInicial,
            textoEditable: true,
            "Categoría",
            opcionObligatoria ? [Cuadernos] : [Ambiguo, Cuadernos],
            idOpcionInicial,
            opcionObligatoria);
    }

    [Fact]
    public void Constructor_SinOpcionInicialYOpcionOpcional_PreseleccionaLaOpcionSinDestino()
    {
        var sut = Crear(opcionObligatoria: false);

        sut.OpcionSeleccionada.Should().Be(Ambiguo);
    }

    [Fact]
    public void Constructor_ConOpcionInicial_LaPreselecciona()
    {
        var sut = Crear(opcionObligatoria: false, idOpcionInicial: 10);

        sut.OpcionSeleccionada.Should().Be(Cuadernos);
    }

    [Fact]
    public void Aceptar_SinTexto_MuestraErrorYNoAcepta()
    {
        var sut = Crear(opcionObligatoria: false);

        sut.AceptarCommand.Execute(null);

        sut.DialogResult.Should().BeFalse();
        sut.MensajeError.Should().Be("Término es obligatorio.");
    }

    [Fact]
    public void Aceptar_ConOpcionObligatoriaSinElegir_MuestraErrorYNoAcepta()
    {
        var sut = Crear(opcionObligatoria: true, textoInicial: "RIV");

        sut.AceptarCommand.Execute(null);

        sut.DialogResult.Should().BeFalse();
        sut.TieneError.Should().BeTrue();
    }

    [Fact]
    public void Aceptar_ConDatosValidos_DevuelveElTextoRecortadoYLaOpcion()
    {
        var sut = Crear(opcionObligatoria: false, idOpcionInicial: 10, textoInicial: "  CUADERNO ");

        sut.AceptarCommand.Execute(null);

        sut.DialogResult.Should().BeTrue();
        sut.Resultado.Should().Be(new ResultadoFormularioDiccionario("CUADERNO", 10));
    }
}

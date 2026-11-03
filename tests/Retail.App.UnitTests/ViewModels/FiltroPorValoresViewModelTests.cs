using FluentAssertions;
using Retail.App.ViewModels.Comun;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class FiltroPorValoresViewModelTests
{
    [Fact]
    public void Actualizar_ConValoresRepetidos_ArmaOpcionesDistintasYOrdenadas()
    {
        var filtro = new FiltroPorValoresViewModel("Operador");

        filtro.Actualizar(["Sistema", "gerente", "Sistema", "Ana"]);

        filtro.Filtro.Grupos.SelectMany(g => g.Opciones).Select(o => o.Nombre).Should().Equal("Ana", "gerente", "Sistema");
    }

    [Fact]
    public void Coincide_SinFiltroAplicado_AceptaCualquierValor()
    {
        var filtro = new FiltroPorValoresViewModel("Operador");
        filtro.Actualizar(["Ana", "Sistema"]);

        filtro.Coincide("Ana").Should().BeTrue();
        filtro.Coincide("otro").Should().BeTrue();
    }

    [Fact]
    public void Coincide_ConValorAplicado_AceptaSoloEseValorSinDistinguirMayusculas()
    {
        var filtro = new FiltroPorValoresViewModel("Operador");
        filtro.Actualizar(["Ana", "Sistema"]);
        filtro.Filtro.AplicarIds([1]);

        filtro.Coincide("sistema").Should().BeTrue();
        filtro.Coincide("Ana").Should().BeFalse();
    }

    [Fact]
    public void Actualizar_DespuesDeAplicar_ConservaLoAplicadoPorNombreAunqueCambieLaPosicion()
    {
        var filtro = new FiltroPorValoresViewModel("Operador");
        filtro.Actualizar(["Ana", "Sistema"]);
        filtro.Filtro.AplicarIds([1]);

        // Aparece un valor nuevo que se ordena antes: "Sistema" pasa de la posición 1 a la 2
        filtro.Actualizar(["Ana", "Beto", "Sistema"]);

        filtro.Coincide("Sistema").Should().BeTrue();
        filtro.Coincide("Beto").Should().BeFalse();
        filtro.Filtro.HayFiltroActivo.Should().BeTrue();
    }
}

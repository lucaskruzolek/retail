using System.Windows;
using System.Windows.Controls;
using FluentAssertions;
using Retail.App.UnitTests.Helpers;
using Retail.App.Views.Components;
using Xunit;

namespace Retail.App.UnitTests;

/// <summary>El círculo de carga no ocupa ni libera lugar al aparecer: su cuadrado está siempre reservado.</summary>
public class IndicadorCargaTests
{
    private static Size Medir(FrameworkElement elemento)
    {
        if (!elemento.IsInitialized)
        {
            elemento.BeginInit();
            elemento.EndInit();
        }

        elemento.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        elemento.Arrange(new Rect(elemento.DesiredSize));
        elemento.UpdateLayout();

        return elemento.DesiredSize;
    }

    [Theory]
    [InlineData(false, Visibility.Hidden)]
    [InlineData(true, Visibility.Visible)]
    public void IndicadorCarga_SegunEstaCargando_SeOcultaSinColapsarse(bool cargando, Visibility esperada)
    {
        Visibility visibilidad = Visibility.Collapsed;

        WpfTestHelper.Run(() =>
        {
            var indicador = new IndicadorCarga { EstaCargando = cargando };
            Medir(indicador);
            visibilidad = indicador.Visibility;
        });

        visibilidad.Should().Be(esperada, "oculto reserva su lugar; colapsado lo liberaría y correría lo demás");
    }

    [Fact]
    public void IndicadorCarga_ConYSinCarga_OcupaElMismoLugar()
    {
        Size sinCarga = default;
        Size conCarga = default;

        WpfTestHelper.Run(() =>
        {
            sinCarga = Medir(new IndicadorCarga { EstaCargando = false });
            conCarga = Medir(new IndicadorCarga { EstaCargando = true });
        });

        sinCarga.Should().Be(new Size(20, 20));
        conCarga.Should().Be(sinCarga);
    }

    [Fact]
    public void PageHeader_AlCargar_NoCambiaSuTamano()
    {
        // El encabezado mide lo mismo cargando que sin cargar: el círculo no empuja nada
        Size sinCarga = default;
        Size conCarga = default;

        WpfTestHelper.Run(() =>
        {
            sinCarga = Medir(new PageHeader { Titulo = "Inventario", Descripcion = "Artículos · Catálogo", Content = new Button { Content = "Nuevo" }, EstaCargando = false });
            conCarga = Medir(new PageHeader { Titulo = "Inventario", Descripcion = "Artículos · Catálogo", Content = new Button { Content = "Nuevo" }, EstaCargando = true });
        });

        conCarga.Should().Be(sinCarga);
    }
}

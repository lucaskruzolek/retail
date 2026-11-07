using System.Windows.Controls;
using FluentAssertions;
using Retail.App.UnitTests.Helpers;
using Retail.App.Views.Components;
using Xunit;

namespace Retail.App.UnitTests;

public class SelectorFechaUnicaTests
{
    [Fact]
    public void ElegirDia_EnElCalendario_ActualizaLaFechaYCierraElPanel()
    {
        // Arrange + Act
        var (fecha, abierto) = WpfTestHelper.Run(() =>
        {
            var selector = new SelectorFechaUnica { Fecha = new DateTime(2026, 11, 3) };
            var calendario = (Calendar)selector.FindName("Calendario");
            selector.Estado.EstaAbierto = true;

            calendario.SelectedDate = new DateTime(2026, 11, 20);

            return (selector.Fecha, selector.Estado.EstaAbierto);
        });

        // Assert
        fecha.Should().Be(new DateTime(2026, 11, 20));
        abierto.Should().BeFalse("elegir un día cierra el calendario");
    }

    [Fact]
    public void Fecha_AlCambiarDesdeElViewModel_MueveLaSeleccionDelCalendario()
    {
        // Arrange + Act
        var seleccion = WpfTestHelper.Run(() =>
        {
            var selector = new SelectorFechaUnica { Fecha = new DateTime(2026, 11, 3) };
            selector.Fecha = new DateTime(2026, 12, 15);

            return ((Calendar)selector.FindName("Calendario")).SelectedDate;
        });

        // Assert
        seleccion.Should().Be(new DateTime(2026, 12, 15));
    }
}

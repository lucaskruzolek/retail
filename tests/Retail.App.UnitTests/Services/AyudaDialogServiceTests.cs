using FluentAssertions;
using NSubstitute;
using Retail.App.Services;
using Retail.App.ViewModels.Ayuda;
using Xunit;

namespace Retail.App.UnitTests.Services;

public class AyudaDialogServiceTests
{
    [Fact]
    public void ObtenerRutaManual_DebeRetornarRutaConNombreEsperado()
    {
        // Arrange
        var service = new AyudaDialogService();

        // Act
        var ruta = service.ObtenerRutaManual();

        // Assert
        ruta.Should().NotBeNullOrWhiteSpace();
        ruta.Should().EndWith("Manual_Usuario_Retail_POS.pdf");
    }

    [Fact]
    public void AyudaViewModel_Instanciacion_DebeCargarAtajosYVersion()
    {
        // Act
        var vm = new AyudaViewModel();

        // Assert
        vm.Titulo.Should().Be("Ayuda y Soporte");
        vm.VersionInfo.Should().Contain("Retail POS");
        vm.Atajos.Should().NotBeEmpty();
        vm.Atajos.Should().Contain(a => a.Tecla == "F1" && a.Accion == "Punto de Venta");
        vm.Atajos.Should().Contain(a => a.Tecla == "Ctrl+H");
        vm.TieneError.Should().BeFalse();
    }

    [Fact]
    public void AyudaViewModel_AbrirManualPdf_SinServicio_DebeEstablecerTieneError()
    {
        // Arrange
        var vm = new AyudaViewModel(null);

        // Act
        vm.AbrirManualPdfCommand.Execute(null);

        // Assert
        vm.TieneError.Should().BeTrue();
        vm.MensajeError.Should().Contain("servicio de ayuda");
    }

    [Fact]
    public void AyudaViewModel_AbrirManualPdf_SiServicioFalla_DebeEstablecerTieneError()
    {
        // Arrange
        var servicio = Substitute.For<IAyudaDialogService>();
        servicio.AbrirManualPdf().Returns(false);
        var vm = new AyudaViewModel(servicio);

        // Act
        vm.AbrirManualPdfCommand.Execute(null);

        // Assert
        vm.TieneError.Should().BeTrue();
        vm.MensajeError.Should().Contain("No se pudo abrir el manual");
    }

    [Fact]
    public void AyudaViewModel_AbrirManualPdf_SiServicioTieneExito_NoDebeTenerError()
    {
        // Arrange
        var servicio = Substitute.For<IAyudaDialogService>();
        servicio.AbrirManualPdf().Returns(true);
        var vm = new AyudaViewModel(servicio);

        // Act
        vm.AbrirManualPdfCommand.Execute(null);

        // Assert
        vm.TieneError.Should().BeFalse();
        vm.MensajeError.Should().BeNull();
    }

    [Fact]
    public void AyudaViewModel_Cerrar_DebeDispararEventoSolicitarCerrar()
    {
        // Arrange
        var vm = new AyudaViewModel();
        var eventoDisparado = false;
        vm.SolicitarCerrar += () => eventoDisparado = true;

        // Act
        vm.CerrarCommand.Execute(null);

        // Assert
        eventoDisparado.Should().BeTrue();
    }
}


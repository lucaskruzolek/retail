using FluentAssertions;
using NSubstitute;
using Retail.App.Services;
using Retail.App.ViewModels.Ayuda;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.App.UnitTests.Services;

public class AyudaDialogServiceTests
{
    [Fact]
    public void ObtenerRutaManual_DebeRetornarRutaConNombreEsperado()
    {
        // Arrange
        var service = new AyudaDialogService(Substitute.For<ICurrentUserSession>());

        // Act
        var ruta = service.ObtenerRutaManual();

        // Assert
        ruta.Should().NotBeNullOrWhiteSpace();
        ruta.Should().EndWith("Manual_Usuario_Retail_POS.pdf");
    }

    [Fact]
    public void Constructor_SinSesion_LanzaArgumentNullException()
    {
        var crear = () => new AyudaDialogService(null!);

        crear.Should().Throw<ArgumentNullException>().WithParameterName("sesion");
    }

    [Fact]
    public void AyudaViewModel_Instanciacion_DebeCargarAtajosYVersion()
    {
        // Act
        var vm = new AyudaViewModel(RolUsuarioEnum.Cajero);

        // Assert
        vm.Titulo.Should().Be("Ayuda y Soporte");
        vm.VersionInfo.Should().Contain("Retail POS");
        vm.Atajos.Should().Contain(a => a.Tecla == "F1" && a.Accion == "Punto de venta");
        vm.Atajos.Should().Contain(a => a.Tecla == "Ctrl+H");
        vm.TieneError.Should().BeFalse();
    }

    [Theory]
    [InlineData(RolUsuarioEnum.Gerente, "F1,F2,F4,F5,F6,F9,F10,F11,Ctrl+B,Ctrl+H,Esc")]
    [InlineData(RolUsuarioEnum.Cajero, "F1,F2,F4,F5,F6,Ctrl+B,Ctrl+H,Esc")]
    [InlineData(RolUsuarioEnum.Administrador, "F10,F11,Ctrl+B,Ctrl+H,Esc")]
    public void AyudaViewModel_PorRol_ListaSoloLosAtajosPermitidosYLosDelShell(RolUsuarioEnum rol, string esperados)
    {
        var vm = new AyudaViewModel(rol);

        vm.Atajos.Select(a => a.Tecla).Should().Equal(esperados.Split(','));
    }

    [Fact]
    public void AyudaViewModel_AtajoDePagina_DetalleEsLaSeccionSalvoQueRepitaElTitulo()
    {
        var vm = new AyudaViewModel(RolUsuarioEnum.Gerente);

        vm.Atajos.Single(a => a.Tecla == "F4").Detalle.Should().Be("Ventas");
        vm.Atajos.Single(a => a.Tecla == "F1").Detalle.Should().BeEmpty("la sección y la página se llaman igual");
    }

    [Fact]
    public void AyudaViewModel_AbrirManualPdf_SinServicio_DebeEstablecerTieneError()
    {
        // Arrange
        var vm = new AyudaViewModel(RolUsuarioEnum.Cajero);

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
        var vm = new AyudaViewModel(RolUsuarioEnum.Cajero, servicio);

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
        var vm = new AyudaViewModel(RolUsuarioEnum.Cajero, servicio);

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
        var vm = new AyudaViewModel(RolUsuarioEnum.Cajero);
        var eventoDisparado = false;
        vm.SolicitarCerrar += () => eventoDisparado = true;

        // Act
        vm.CerrarCommand.Execute(null);

        // Assert
        eventoDisparado.Should().BeTrue();
    }
}

using FluentAssertions;
using NSubstitute;
using Retail.App.Services;
using Retail.App.ViewModels;
using Retail.App.Views.Dev;
using Retail.App.Views.Pages;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class MainViewModelTests
{
    private readonly ICurrentUserSession _sessionMock;
    private readonly INavigationService _navigationMock;

    public MainViewModelTests()
    {
        _sessionMock = Substitute.For<ICurrentUserSession>();
        _navigationMock = Substitute.For<INavigationService>();
    }

    [Fact]
    public void Constructor_Inicializacion_DebeCargarDatosDeSesion()
    {
        // Arrange
        _sessionMock.NombreCompleto.Returns("Juan Pérez");
        _sessionMock.Rol.Returns(RolUsuarioEnum.Gerente);
        _sessionMock.EsGerente.Returns(true);
        _sessionMock.EsEncargado.Returns(false);
        _sessionMock.EsCajero.Returns(false);

        // Act
        var vm = new MainViewModel(_sessionMock, _navigationMock);

        // Assert
        vm.OperadorNombre.Should().Be("Juan Pérez");
        vm.OperadorRol.Should().Be("Gerente");
        vm.EsGerente.Should().BeTrue();
        vm.EsEncargado.Should().BeFalse();
        vm.EsCajero.Should().BeFalse();
        vm.IsSidebarExpanded.Should().BeFalse();
        vm.MostrarCabeceraAdmin.Should().BeFalse();
        vm.ModuloActivo.Should().Be("Articulos");
    }

    [Fact]
    public void ToggleSidebarCommand_Ejecucion_DebeAlternarEstadoDeExpansion()
    {
        // Arrange
        var vm = new MainViewModel(_sessionMock, _navigationMock);
        vm.IsSidebarExpanded.Should().BeFalse();

        // Act & Assert (primer toggle: expandir)
        vm.ToggleSidebarCommand.Execute(null);
        vm.IsSidebarExpanded.Should().BeTrue();

        // Act & Assert (segundo toggle: colapsar)
        vm.ToggleSidebarCommand.Execute(null);
        vm.IsSidebarExpanded.Should().BeFalse();
    }

    [Fact]
    public void MostrarCabeceraAdmin_CondicionadaAEstatusGerenteYExpansion()
    {
        // Arrange: Gerente
        _sessionMock.EsGerente.Returns(true);
        var vm = new MainViewModel(_sessionMock, _navigationMock);

        // Colapsado: false
        vm.MostrarCabeceraAdmin.Should().BeFalse();

        // Expandido con Gerente: true
        vm.ToggleSidebarCommand.Execute(null);
        vm.MostrarCabeceraAdmin.Should().BeTrue();

        // Cajero expandido: false
        _sessionMock.EsGerente.Returns(false);
        _sessionMock.SessionChanged += Raise.Event<Action>();
        vm.MostrarCabeceraAdmin.Should().BeFalse();
    }

    [Fact]
    public void NavegarArticulosCommand_Ejecucion_DebeNavegarHaciaArticulosView()
    {
        // Arrange
        var vm = new MainViewModel(_sessionMock, _navigationMock);

        // Act
        vm.NavegarArticulosCommand.Execute(null);

        // Assert
        _navigationMock.Received(1).NavigateTo<ArticulosView>();
        vm.ModuloActivo.Should().Be("Articulos");
    }

    [Fact]
    public void NavegarClientesCommand_Ejecucion_DebeNavegarHaciaClientesView()
    {
        // Arrange
        var vm = new MainViewModel(_sessionMock, _navigationMock);

        // Act
        vm.NavegarClientesCommand.Execute(null);

        // Assert
        _navigationMock.Received(1).NavigateTo<ClientesView>();
        vm.ModuloActivo.Should().Be("Clientes");
    }

    [Fact]
    public void NavegarUsuariosCommand_UsuarioGerente_DebeNavegarHaciaUsuariosView()
    {
        // Arrange
        _sessionMock.EsGerente.Returns(true);
        var vm = new MainViewModel(_sessionMock, _navigationMock);

        // Act
        vm.NavegarUsuariosCommand.Execute(null);

        // Assert
        _navigationMock.Received(1).NavigateTo<UsuariosView>();
        vm.ModuloActivo.Should().Be("Usuarios");
    }

    [Fact]
    public void NavegarUsuariosCommand_UsuarioNoGerente_NoDebeNavegar()
    {
        // Arrange
        _sessionMock.EsGerente.Returns(false);
        var vm = new MainViewModel(_sessionMock, _navigationMock);

        // Act
        vm.NavegarUsuariosCommand.Execute(null);

        // Assert
        _navigationMock.DidNotReceive().NavigateTo<UsuariosView>();
    }

    [Fact]
    public void NavegarGaleriaDevCommand_Ejecucion_DebeNavegarHaciaStyleGalleryView()
    {
        // Arrange
        var vm = new MainViewModel(_sessionMock, _navigationMock);

        // Act
        vm.NavegarGaleriaDevCommand.Execute(null);

        // Assert
        _navigationMock.Received(1).NavigateTo<StyleGalleryView>();
        vm.ModuloActivo.Should().Be("Galeria");
    }

    [Fact]
    public void NavegarPosCommand_Ejecucion_DebeNavegarHaciaPosView()
    {
        // Arrange
        var vm = new MainViewModel(_sessionMock, _navigationMock);

        // Act
        vm.NavegarPosCommand.Execute(null);

        // Assert
        _navigationMock.Received(1).NavigateTo<PosView>();
        vm.ModuloActivo.Should().Be("Pos");
    }

    [Fact]
    public void NavegarModulosEnConstruccion_CajaYPresupuestos_DebeInvocarModuloEnConstruccion()
    {
        // Arrange
        var vm = new MainViewModel(_sessionMock, _navigationMock);

        // Act Caja
        vm.NavegarCajaCommand.Execute(null);
        vm.ModuloActivo.Should().Be("Caja");

        // Act Presupuestos
        vm.NavegarPresupuestosCommand.Execute(null);
        vm.ModuloActivo.Should().Be("Presupuestos");

        // Assert: 2 llamadas a NavigateTo<ModuloEnConstruccionView> con configuración
        _navigationMock.Received(2).NavigateTo(Arg.Any<Action<ModuloEnConstruccionView>>());
    }

    [Fact]
    public void NavegarComprasCommand_DebeNavegarHaciaComprasView()
    {
        // Arrange
        var vm = new MainViewModel(_sessionMock, _navigationMock);

        // Act
        vm.NavegarComprasCommand.Execute(null);

        // Assert
        vm.ModuloActivo.Should().Be("Compras");
        vm.TituloModuloActual.Should().Be("Compras a Proveedores y Recálculo de Precios");
        _navigationMock.Received(1).NavigateTo<ComprasView>();
    }

    [Fact]
    public void OnNavigated_ConComprasView_DebeActualizarModuloActivoYTitulo()
    {
        // Arrange
        var vm = new MainViewModel(_sessionMock, _navigationMock);

        // Act
        _navigationMock.Navigated += Raise.Event<Action<Type>>(typeof(ComprasView));

        // Assert
        vm.ModuloActivo.Should().Be("Compras");
        vm.TituloModuloActual.Should().Be("Compras a Proveedores y Recálculo de Precios");
    }

    [Fact]
    public void NavegarProveedoresCommand_DebeNavegarHaciaProveedoresView()
    {
        // Arrange
        var vm = new MainViewModel(_sessionMock, _navigationMock);

        // Act
        vm.NavegarProveedoresCommand.Execute(null);

        // Assert
        vm.ModuloActivo.Should().Be("Proveedores");
        _navigationMock.Received(1).NavigateTo<ProveedoresView>();
    }

    [Fact]
    public void NavegarCatalogosCommand_DebeNavegarHaciaImportadorCatalogosView()
    {
        // Arrange
        var vm = new MainViewModel(_sessionMock, _navigationMock);

        // Act
        vm.NavegarCatalogosCommand.Execute(null);

        // Assert
        vm.ModuloActivo.Should().Be("Catalogos");
        vm.TituloModuloActual.Should().Be("Catálogos de Proveedores e Importación Masiva");
        _navigationMock.Received(1).NavigateTo<ImportadorCatalogosView>();
    }

    [Fact]
    public void OnNavigated_ConImportadorCatalogosView_DebeActualizarModuloActivoYTitulo()
    {
        // Arrange
        var vm = new MainViewModel(_sessionMock, _navigationMock);

        // Act
        _navigationMock.Navigated += Raise.Event<Action<Type>>(typeof(ImportadorCatalogosView));

        // Assert
        vm.ModuloActivo.Should().Be("Catalogos");
        vm.TituloModuloActual.Should().Be("Catálogos de Proveedores e Importación Masiva");
    }

    [Fact]
    public void NavegarConsolaFiscalCommand_Gerente_DebeNavegarHaciaModuloEnConstruccion()
    {
        // Arrange
        _sessionMock.EsGerente.Returns(true);
        var vm = new MainViewModel(_sessionMock, _navigationMock);

        // Act
        vm.NavegarConsolaFiscalCommand.Execute(null);

        // Assert
        vm.ModuloActivo.Should().Be("Fiscal");
        _navigationMock.Received(1).NavigateTo(Arg.Any<Action<ModuloEnConstruccionView>>());
    }

    [Fact]
    public void NavegarConsolaFiscalCommand_NoGerente_NoDebeNavegar()
    {
        // Arrange
        _sessionMock.EsGerente.Returns(false);
        var vm = new MainViewModel(_sessionMock, _navigationMock);

        // Act
        vm.NavegarConsolaFiscalCommand.Execute(null);

        // Assert
        _navigationMock.DidNotReceive().NavigateTo(Arg.Any<Action<ModuloEnConstruccionView>>());
    }

    [Fact]
    public async Task CambiarUsuarioAsync_Invocacion_DebeDispararEvento()
    {
        // Arrange
        var vm = new MainViewModel(_sessionMock, _navigationMock);
        var eventoDisparado = false;
        vm.SolicitarCambioUsuario += () =>
        {
            eventoDisparado = true;
            return Task.CompletedTask;
        };

        // Act
        await vm.CambiarUsuarioAsync();

        // Assert
        eventoDisparado.Should().BeTrue();
    }
}

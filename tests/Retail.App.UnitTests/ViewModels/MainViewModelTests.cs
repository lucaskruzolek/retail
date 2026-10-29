using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Services;
using Retail.App.ViewModels;
using Retail.App.Views.Dev;
using Retail.App.Views.Pages;
using Retail.Application.DTOs.Backups;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class MainViewModelTests
{
    private readonly ICurrentUserSession _sessionMock;
    private readonly INavigationService _navigationMock;
    private readonly IServiceScopeFactory _scopeFactoryMock;

    public MainViewModelTests()
    {
        _sessionMock = Substitute.For<ICurrentUserSession>();
        _navigationMock = Substitute.For<INavigationService>();
        _scopeFactoryMock = Substitute.For<IServiceScopeFactory>();
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
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

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
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);
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
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

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
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

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
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

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
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

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
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

        // Act
        vm.NavegarUsuariosCommand.Execute(null);

        // Assert
        _navigationMock.DidNotReceive().NavigateTo<UsuariosView>();
    }

    [Fact]
    public void NavegarGaleriaDevCommand_Ejecucion_DebeNavegarHaciaStyleGalleryView()
    {
        // Arrange
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

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
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

        // Act
        vm.NavegarPosCommand.Execute(null);

        // Assert
        _navigationMock.Received(1).NavigateTo<PosView>();
        vm.ModuloActivo.Should().Be("Pos");
    }

    [Fact]
    public void NavegarPresupuestosCommand_ModuloEnConstruccion_DebeInvocarModuloEnConstruccion()
    {
        // Arrange
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

        // Act
        vm.NavegarPresupuestosCommand.Execute(null);

        // Assert
        vm.ModuloActivo.Should().Be("Presupuestos");
        _navigationMock.Received(1).NavigateTo(Arg.Any<Action<ModuloEnConstruccionView>>());
    }

    [Fact]
    public void NavegarCajaCommand_DebeNavegarHaciaCajaView()
    {
        // Arrange
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

        // Act
        vm.NavegarCajaCommand.Execute(null);

        // Assert: el módulo 3.1 ya está implementado y no usa la vista "en construcción"
        vm.ModuloActivo.Should().Be("Caja");
        _navigationMock.Received(1).NavigateTo<CajaView>();
        _navigationMock.DidNotReceive().NavigateTo(Arg.Any<Action<ModuloEnConstruccionView>>());
    }

    [Fact]
    public void NavegarComprasCommand_DebeNavegarHaciaComprasView()
    {
        // Arrange
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

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
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

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
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

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
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

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
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

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
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

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
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

        // Act
        vm.NavegarConsolaFiscalCommand.Execute(null);

        // Assert
        _navigationMock.DidNotReceive().NavigateTo(Arg.Any<Action<ModuloEnConstruccionView>>());
    }

    // ───────────────────── Tablero gerencial (Track 6.4) ─────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NavegarTableroCommand_SegunElRol_SoloNavegaElGerente(bool esGerente)
    {
        // Arrange
        _sessionMock.EsGerente.Returns(esGerente);
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

        // Act
        vm.NavegarTableroCommand.Execute(null);

        // Assert
        if (esGerente)
        {
            _navigationMock.Received(1).NavigateTo<DashboardView>();
            vm.ModuloActivo.Should().Be("Tablero");
        }
        else
        {
            _navigationMock.DidNotReceive().NavigateTo<DashboardView>();
        }
    }

    [Fact]
    public void Navigated_HaciaElTablero_ActualizaElModuloYElTitulo()
    {
        // Arrange
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

        // Act
        _navigationMock.Navigated += Raise.Event<Action<Type>>(typeof(DashboardView));

        // Assert
        vm.ModuloActivo.Should().Be("Tablero");
        vm.TituloModuloActual.Should().Be("Tablero gerencial");
    }

    [Fact]
    public async Task CambiarUsuarioAsync_Invocacion_DebeDispararEvento()
    {
        // Arrange
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);
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

    [Fact]
    public void Administrador_GestionaUsuariosPeroNoOperaElNegocio()
    {
        // Arrange
        _sessionMock.EsAdministrador.Returns(true);

        // Act
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);
        vm.NavegarPosCommand.Execute(null);
        vm.NavegarCajaCommand.Execute(null);
        vm.NavegarUsuariosCommand.Execute(null);

        // Assert: los módulos operativos se ignoran aunque se invoquen por atajo de teclado.
        vm.PuedeOperarNegocio.Should().BeFalse();
        vm.PuedeGestionarUsuarios.Should().BeTrue();
        _navigationMock.DidNotReceive().NavigateTo<PosView>();
        _navigationMock.DidNotReceive().NavigateTo<CajaView>();
        _navigationMock.Received(1).NavigateTo<UsuariosView>();
    }

    [Theory]
    [InlineData(RolUsuarioEnum.Gerente, true)]
    [InlineData(RolUsuarioEnum.Administrador, true)]
    [InlineData(RolUsuarioEnum.Encargado, false)]
    [InlineData(RolUsuarioEnum.Cajero, false)]
    public void PuedeGestionarUsuarios_SegunRol_HabilitaElModuloDeUsuarios(RolUsuarioEnum rol, bool esperado)
    {
        _sessionMock.EsGerente.Returns(rol == RolUsuarioEnum.Gerente);
        _sessionMock.EsAdministrador.Returns(rol == RolUsuarioEnum.Administrador);

        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

        vm.PuedeGestionarUsuarios.Should().Be(esperado);
    }

    [Theory]
    [InlineData(false, typeof(ArticulosView))]
    [InlineData(true, typeof(UsuariosView))]
    public void NavegarModuloInicial_SegunRol_AbreLaPaginaPermitida(bool esAdministrador, Type paginaEsperada)
    {
        // Arrange
        _sessionMock.EsAdministrador.Returns(esAdministrador);
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

        // Act
        vm.NavegarModuloInicial();

        // Assert
        if (paginaEsperada == typeof(UsuariosView))
        {
            _navigationMock.Received(1).NavigateTo<UsuariosView>();
            _navigationMock.DidNotReceive().NavigateTo<ArticulosView>();
        }
        else
        {
            _navigationMock.Received(1).NavigateTo<ArticulosView>();
        }
    }

    [Fact]
    public void SessionChanged_CambioAUnAdministrador_SaleDelModuloOperativoHaciaUsuarios()
    {
        // Arrange: el Gerente estaba en el POS y cambia de operador (RF-02).
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);
        vm.NavegarPosCommand.Execute(null);

        _sessionMock.EstaAutenticado.Returns(true);
        _sessionMock.EsAdministrador.Returns(true);

        // Act
        _sessionMock.SessionChanged += Raise.Event<Action>();

        // Assert
        _navigationMock.Received(1).NavigateTo<UsuariosView>();
        vm.ModuloActivo.Should().Be("Usuarios");
    }

    // ───────────────────── Respaldos (Track 6.3) ─────────────────────

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void NavegarRespaldosCommand_SegunElRol_SoloNavegaElGerenteOElAdministrador(bool esGerente, bool esAdministrador, bool debeNavegar)
    {
        // Arrange
        _sessionMock.EsGerente.Returns(esGerente);
        _sessionMock.EsAdministrador.Returns(esAdministrador);
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

        // Act
        vm.NavegarRespaldosCommand.Execute(null);

        // Assert
        if (debeNavegar)
        {
            _navigationMock.Received(1).NavigateTo<BackupView>();
            vm.ModuloActivo.Should().Be("Respaldos");
        }
        else
        {
            _navigationMock.DidNotReceive().NavigateTo<BackupView>();
        }
    }

    [Fact]
    public void Navigated_HaciaRespaldos_ActualizaElModuloYElTitulo()
    {
        // Arrange
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

        // Act
        _navigationMock.Navigated += Raise.Event<Action<Type>>(typeof(BackupView));

        // Assert
        vm.ModuloActivo.Should().Be("Respaldos");
        vm.TituloModuloActual.Should().Contain("Copias de Seguridad");
    }

    private static RegistroRestauracionDto Restauracion(DateTime fechaUtc) => new()
    {
        Referencia = Guid.NewGuid(),
        Fecha = fechaUtc,
        Origen = OrigenRestauracionEnum.Consola,
        NombreUsuario = "gerente",
        ReferenciaCopia = Guid.NewGuid(),
        RutaArchivo = @"C:\RetailData\Backups\copia.bak"
    };

    /// <summary>
    /// Fábrica de scopes de prueba: el Shell es Singleton y resuelve <see cref="IRestauracionService"/> en un scope propio.
    /// </summary>
    private static IServiceScopeFactory FabricaConServicio(IRestauracionService servicio)
    {
        var proveedor = Substitute.For<IServiceProvider>();
        proveedor.GetService(typeof(IRestauracionService)).Returns(servicio);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(proveedor);

        var fabrica = Substitute.For<IServiceScopeFactory>();
        fabrica.CreateScope().Returns(scope);
        return fabrica;
    }

    private MainViewModel CrearConAvisoComoGerente(IRestauracionService servicio)
    {
        _sessionMock.EstaAutenticado.Returns(true);
        _sessionMock.IdUsuario.Returns(5);
        _sessionMock.EsGerente.Returns(true);
        return new MainViewModel(_sessionMock, _navigationMock, FabricaConServicio(servicio));
    }

    [Fact]
    public async Task CargarAvisoRestauracionAsync_RestauracionDeHaceDosDias_MuestraElAviso()
    {
        // Arrange
        var servicio = Substitute.For<IRestauracionService>();
        servicio.ObtenerUltimaRestauracionAsync(5, Arg.Any<CancellationToken>())
            .Returns(Restauracion(DateTime.UtcNow.AddDays(-2)));
        var vm = CrearConAvisoComoGerente(servicio);

        // Act
        await vm.CargarAvisoRestauracionAsync();

        // Assert
        vm.MostrarAvisoRestauracion.Should().BeTrue();
        vm.AvisoRestauracion.Should().Contain("Se restauró la base").And.Contain("gerente").And.Contain("consola");
    }

    [Fact]
    public async Task CargarAvisoRestauracionAsync_RestauracionDeHaceDiezDias_NoMuestraAviso()
    {
        // Arrange
        var servicio = Substitute.For<IRestauracionService>();
        servicio.ObtenerUltimaRestauracionAsync(5, Arg.Any<CancellationToken>())
            .Returns(Restauracion(DateTime.UtcNow.AddDays(-10)));
        var vm = CrearConAvisoComoGerente(servicio);

        // Act
        await vm.CargarAvisoRestauracionAsync();

        // Assert
        vm.MostrarAvisoRestauracion.Should().BeFalse();
    }

    [Fact]
    public async Task CargarAvisoRestauracionAsync_CajeroSinPermiso_NoConsultaElServicio()
    {
        // Arrange
        var servicio = Substitute.For<IRestauracionService>();
        _sessionMock.EstaAutenticado.Returns(true);
        _sessionMock.EsCajero.Returns(true);
        var vm = new MainViewModel(_sessionMock, _navigationMock, FabricaConServicio(servicio));

        // Act
        await vm.CargarAvisoRestauracionAsync();

        // Assert
        vm.MostrarAvisoRestauracion.Should().BeFalse();
        await servicio.DidNotReceiveWithAnyArgs().ObtenerUltimaRestauracionAsync(default, default);
    }

    [Fact]
    public async Task CargarAvisoRestauracionAsync_ElServicioFalla_NoPropagaLaExcepcionNiMuestraAviso()
    {
        // Arrange
        var servicio = Substitute.For<IRestauracionService>();
        servicio.ObtenerUltimaRestauracionAsync(5, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("sin conexión"));
        var vm = CrearConAvisoComoGerente(servicio);

        // Act
        var act = async () => await vm.CargarAvisoRestauracionAsync();

        // Assert
        await act.Should().NotThrowAsync();
        vm.MostrarAvisoRestauracion.Should().BeFalse();
    }

    [Fact]
    public async Task CerrarAvisoRestauracionCommand_ConAvisoVisible_LoOculta()
    {
        // Arrange
        var servicio = Substitute.For<IRestauracionService>();
        servicio.ObtenerUltimaRestauracionAsync(5, Arg.Any<CancellationToken>())
            .Returns(Restauracion(DateTime.UtcNow));
        var vm = CrearConAvisoComoGerente(servicio);
        await vm.CargarAvisoRestauracionAsync();

        // Act
        vm.CerrarAvisoRestauracionCommand.Execute(null);

        // Assert
        vm.MostrarAvisoRestauracion.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, false)]
    public void PuedeAbastecer_SegunRol_SoloEncargadoYGerente(bool esGerente, bool esEncargado, bool esCajero, bool esperado)
    {
        // Arrange
        _sessionMock.EsGerente.Returns(esGerente);
        _sessionMock.EsEncargado.Returns(esEncargado);
        _sessionMock.EsCajero.Returns(esCajero);

        // Act
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

        // Assert: A-14 del Track 4.3
        vm.PuedeAbastecer.Should().Be(esperado);
    }

    [Fact]
    public void NavegarPedidosCommand_UsuarioEncargado_DebeNavegarHaciaPedidosView()
    {
        // Arrange
        _sessionMock.EsEncargado.Returns(true);
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

        // Act
        vm.NavegarPedidosCommand.Execute(null);

        // Assert
        _navigationMock.Received(1).NavigateTo<PedidosView>();
        vm.ModuloActivo.Should().Be("Pedidos");
    }

    [Fact]
    public void NavegarPedidosCommand_UsuarioCajero_NoDebeNavegar()
    {
        // Arrange
        _sessionMock.EsCajero.Returns(true);
        var vm = new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);

        // Act
        vm.NavegarPedidosCommand.Execute(null);

        // Assert
        _navigationMock.DidNotReceive().NavigateTo<PedidosView>();
    }
}

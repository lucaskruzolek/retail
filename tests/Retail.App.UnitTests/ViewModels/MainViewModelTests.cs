using System.IO;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Navegacion;
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

    private MainViewModel CrearConRol(RolUsuarioEnum rol)
    {
        _sessionMock.Rol.Returns(rol);
        _sessionMock.EstaAutenticado.Returns(true);
        _sessionMock.EsGerente.Returns(rol == RolUsuarioEnum.Gerente);
        _sessionMock.EsEncargado.Returns(rol == RolUsuarioEnum.Encargado);
        _sessionMock.EsCajero.Returns(rol == RolUsuarioEnum.Cajero);
        _sessionMock.EsAdministrador.Returns(rol == RolUsuarioEnum.Administrador);

        return new MainViewModel(_sessionMock, _navigationMock, _scopeFactoryMock);
    }

    private void DebeHaberNavegadoA(Type vista, int veces = 1)
    {
        _navigationMock.Received(veces).NavigateTo(vista, Arg.Any<Action<object>?>());
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
        vm.GaleriaActiva.Should().BeFalse();
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

    [Theory]
    [InlineData(RolUsuarioEnum.Gerente, "tablero,pos,ventas,inventario,compras,administracion")]
    [InlineData(RolUsuarioEnum.Encargado, "pos,ventas,inventario,compras")]
    [InlineData(RolUsuarioEnum.Cajero, "pos,ventas,inventario,compras")]
    [InlineData(RolUsuarioEnum.Administrador, "administracion")]
    public void SeccionesMenu_SegunElRol_SonLasQueDefineElRegistro(RolUsuarioEnum rol, string esperadas)
    {
        var vm = CrearConRol(rol);

        vm.SeccionesMenu.Select(s => s.Id).Should().Equal(esperadas.Split(','));
    }

    [Fact]
    public void SeccionesMenu_UnAccesoDeUnaSolaPagina_MuestraSuAtajoYUnoDeVarias_SuDetalle()
    {
        var vm = CrearConRol(RolUsuarioEnum.Cajero);

        var pos = vm.SeccionesMenu.Single(s => s.Id == "pos");
        var ventas = vm.SeccionesMenu.Single(s => s.Id == "ventas");

        pos.Atajo.Should().Be("F1");
        pos.TieneDetalle.Should().BeFalse();
        ventas.Detalle.Should().Be("Clientes · Caja y turnos · Presupuestos");
        ventas.TieneAtajo.Should().BeFalse();
    }

    [Fact]
    public void IrASeccionCommand_Ejecucion_AbreLaPrimeraPaginaDeLaSeccion()
    {
        var vm = CrearConRol(RolUsuarioEnum.Gerente);

        vm.IrASeccionCommand.Execute("ventas");

        DebeHaberNavegadoA(typeof(ClientesView));
        vm.SeccionesMenu.Single(s => s.EsActiva).Id.Should().Be("ventas");
    }

    [Fact]
    public void IrASeccionCommand_VolverASeccion_AbreLaUltimaPaginaVisitada()
    {
        var vm = CrearConRol(RolUsuarioEnum.Gerente);
        vm.IrAPaginaCommand.Execute("caja");
        vm.IrAPaginaCommand.Execute("articulos");

        vm.IrASeccionCommand.Execute("ventas");

        DebeHaberNavegadoA(typeof(CajaView), 2);
    }

    [Fact]
    public void IrAPaginaCommand_PaginaConConfiguracion_LaPreparaAlNavegar()
    {
        var vm = CrearConRol(RolUsuarioEnum.Gerente);

        vm.IrAPaginaCommand.Execute("presupuestos");

        Type vista = typeof(ModuloEnConstruccionView);
        _navigationMock.Received(1).NavigateTo(vista, Arg.Is<Action<object>?>(configurar => configurar != null));
    }

    [Theory]
    [InlineData(RolUsuarioEnum.Gerente, "pos", true)]
    [InlineData(RolUsuarioEnum.Cajero, "pos", true)]
    [InlineData(RolUsuarioEnum.Administrador, "pos", false)]
    [InlineData(RolUsuarioEnum.Administrador, "caja", false)]
    [InlineData(RolUsuarioEnum.Administrador, "usuarios", true)]
    [InlineData(RolUsuarioEnum.Cajero, "usuarios", false)]
    [InlineData(RolUsuarioEnum.Cajero, "pedidos", false)]
    [InlineData(RolUsuarioEnum.Encargado, "pedidos", true)]
    [InlineData(RolUsuarioEnum.Cajero, "tablero", false)]
    [InlineData(RolUsuarioEnum.Cajero, "fiscal", false)]
    [InlineData(RolUsuarioEnum.Gerente, "fiscal", true)]
    [InlineData(RolUsuarioEnum.Encargado, "copias", false)]
    [InlineData(RolUsuarioEnum.Administrador, "copias", true)]
    public void IrAPaginaCommand_SegunElRol_SoloNavegaSiLaPoliticaLoPermite(RolUsuarioEnum rol, string paginaId, bool debeNavegar)
    {
        var vm = CrearConRol(rol);

        vm.IrAPaginaCommand.Execute(paginaId);

        var vista = RegistroNavegacion.Buscar(paginaId)!.Value.Pagina.TipoVista;
        DebeHaberNavegadoA(vista, debeNavegar ? 1 : 0);
    }

    [Theory]
    [InlineData(RolUsuarioEnum.Cajero, "F4", true, typeof(ClientesView))]
    [InlineData(RolUsuarioEnum.Cajero, "F9", false, typeof(ModuloEnConstruccionView))]
    [InlineData(RolUsuarioEnum.Gerente, "F9", true, typeof(ModuloEnConstruccionView))]
    [InlineData(RolUsuarioEnum.Administrador, "F1", false, typeof(PosView))]
    [InlineData(RolUsuarioEnum.Gerente, "F12", false, typeof(PosView))]
    public void AbrirPorAtajo_SegunElRol_AtiendeSoloLoQueLaPoliticaPermite(RolUsuarioEnum rol, string atajo, bool atendido, Type vista)
    {
        var vm = CrearConRol(rol);

        vm.AbrirPorAtajo(atajo).Should().Be(atendido);

        DebeHaberNavegadoA(vista, atendido ? 1 : 0);
    }

    [Theory]
    [InlineData(RolUsuarioEnum.Gerente, typeof(DashboardView))]
    [InlineData(RolUsuarioEnum.Encargado, typeof(PosView))]
    [InlineData(RolUsuarioEnum.Cajero, typeof(PosView))]
    [InlineData(RolUsuarioEnum.Administrador, typeof(UsuariosView))]
    public void NavegarModuloInicial_SegunRol_AbreLaPaginaDeAterrizaje(RolUsuarioEnum rol, Type paginaEsperada)
    {
        var vm = CrearConRol(rol);

        vm.NavegarModuloInicial();

        DebeHaberNavegadoA(paginaEsperada);
    }

    [Fact]
    public void SessionChanged_CambioAUnAdministrador_ReconstruyeElMenuYSaleDelModuloOperativo()
    {
        // Arrange: el Gerente estaba en el POS y cambia de operador (RF-02).
        var vm = CrearConRol(RolUsuarioEnum.Gerente);
        vm.IrAPaginaCommand.Execute("pos");

        _sessionMock.Rol.Returns(RolUsuarioEnum.Administrador);
        _sessionMock.EsGerente.Returns(false);
        _sessionMock.EsAdministrador.Returns(true);

        // Act
        _sessionMock.SessionChanged += Raise.Event<Action>();

        // Assert
        DebeHaberNavegadoA(typeof(UsuariosView));
        vm.SeccionesMenu.Select(s => s.Id).Should().Equal("administracion");
        vm.SeccionesMenu.Single().EsActiva.Should().BeTrue();
    }

    [Fact]
    public void Navigated_PorOtraVia_MarcaLaSeccionDeEsaPagina()
    {
        // Arrange: una pantalla navega directo con INavigationService (un enlace entre pantallas)
        var vm = CrearConRol(RolUsuarioEnum.Gerente);

        // Act
        _navigationMock.Navigated += Raise.Event<Action<Type>>(typeof(ProveedoresView));

        // Assert
        vm.SeccionesMenu.Single(s => s.EsActiva).Id.Should().Be("compras");
    }

    [Fact]
    public void IrASeccionCommand_Ajustes_MarcaElAccesoFijoYNoLasSeccionesDelMenu()
    {
        var vm = CrearConRol(RolUsuarioEnum.Cajero);

        vm.IrASeccionCommand.Execute("ajustes");

        DebeHaberNavegadoA(typeof(AjustesView));
        vm.ItemAjustes.EsActiva.Should().BeTrue();
        vm.SeccionesMenu.Should().OnlyContain(s => !s.EsActiva);
    }

    [Fact]
    public void NavegarGaleriaDevCommand_Ejecucion_DebeNavegarHaciaStyleGalleryView()
    {
        // Arrange
        var vm = CrearConRol(RolUsuarioEnum.Gerente);
        vm.IrAPaginaCommand.Execute("articulos");

        // Act
        vm.NavegarGaleriaDevCommand.Execute(null);

        // Assert: la galería no es parte del registro, así que ninguna sección queda activa
        _navigationMock.Received(1).NavigateTo<StyleGalleryView>();
        vm.GaleriaActiva.Should().BeTrue();
    }

    [Fact]
    public void GaleriaActiva_AlVolverAUnaSeccion_SeApaga()
    {
        var vm = CrearConRol(RolUsuarioEnum.Gerente);
        vm.NavegarGaleriaDevCommand.Execute(null);

        vm.IrASeccionCommand.Execute("inventario");

        vm.GaleriaActiva.Should().BeFalse();
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

    private MainViewModel CrearConAvisoComoGerente(
        IRestauracionService servicio,
        IConfirmacionesAvisoRestauracion? confirmaciones = null)
    {
        _sessionMock.EstaAutenticado.Returns(true);
        _sessionMock.IdUsuario.Returns(5);
        _sessionMock.EsGerente.Returns(true);
        return new MainViewModel(_sessionMock, _navigationMock, FabricaConServicio(servicio), confirmaciones);
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
        await vm.CerrarAvisoRestauracionCommand.ExecuteAsync(null);

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
    public async Task CargarAvisoRestauracionAsync_RestauracionYaConfirmadaPorElOperador_NoMuestraAviso()
    {
        // Arrange
        var restauracion = Restauracion(DateTime.UtcNow.AddDays(-1));
        var servicio = Substitute.For<IRestauracionService>();
        servicio.ObtenerUltimaRestauracionAsync(5, Arg.Any<CancellationToken>()).Returns(restauracion);
        var confirmaciones = Substitute.For<IConfirmacionesAvisoRestauracion>();
        confirmaciones.EstaConfirmadaAsync(5, restauracion.Referencia, Arg.Any<CancellationToken>()).Returns(true);
        var vm = CrearConAvisoComoGerente(servicio, confirmaciones);

        // Act
        await vm.CargarAvisoRestauracionAsync();

        // Assert
        vm.MostrarAvisoRestauracion.Should().BeFalse();
    }

    [Fact]
    public async Task CargarAvisoRestauracionAsync_RestauracionNoConfirmada_MuestraAviso()
    {
        // Arrange
        var restauracion = Restauracion(DateTime.UtcNow.AddDays(-1));
        var servicio = Substitute.For<IRestauracionService>();
        servicio.ObtenerUltimaRestauracionAsync(5, Arg.Any<CancellationToken>()).Returns(restauracion);
        var confirmaciones = Substitute.For<IConfirmacionesAvisoRestauracion>();
        confirmaciones.EstaConfirmadaAsync(5, restauracion.Referencia, Arg.Any<CancellationToken>()).Returns(false);
        var vm = CrearConAvisoComoGerente(servicio, confirmaciones);

        // Act
        await vm.CargarAvisoRestauracionAsync();

        // Assert
        vm.MostrarAvisoRestauracion.Should().BeTrue();
    }

    [Fact]
    public async Task CargarAvisoRestauracionAsync_FallaLaLecturaDeConfirmaciones_MuestraAvisoIgual()
    {
        // Arrange
        var servicio = Substitute.For<IRestauracionService>();
        servicio.ObtenerUltimaRestauracionAsync(5, Arg.Any<CancellationToken>())
            .Returns(Restauracion(DateTime.UtcNow.AddDays(-1)));
        var confirmaciones = Substitute.For<IConfirmacionesAvisoRestauracion>();
        confirmaciones.EstaConfirmadaAsync(Arg.Any<int>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("archivo bloqueado"));
        var vm = CrearConAvisoComoGerente(servicio, confirmaciones);

        // Act
        await vm.CargarAvisoRestauracionAsync();

        // Assert
        vm.MostrarAvisoRestauracion.Should().BeTrue();
    }

    [Fact]
    public async Task CerrarAvisoRestauracionCommand_ConAvisoVisible_ConfirmaLaRestauracionDelOperador()
    {
        // Arrange
        var restauracion = Restauracion(DateTime.UtcNow);
        var servicio = Substitute.For<IRestauracionService>();
        servicio.ObtenerUltimaRestauracionAsync(5, Arg.Any<CancellationToken>()).Returns(restauracion);
        var confirmaciones = Substitute.For<IConfirmacionesAvisoRestauracion>();
        var vm = CrearConAvisoComoGerente(servicio, confirmaciones);
        await vm.CargarAvisoRestauracionAsync();

        // Act
        await vm.CerrarAvisoRestauracionCommand.ExecuteAsync(null);

        // Assert
        vm.MostrarAvisoRestauracion.Should().BeFalse();
        await confirmaciones.Received(1).ConfirmarAsync(5, restauracion.Referencia, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CerrarAvisoRestauracionCommand_FallaLaEscritura_OcultaElAvisoSinPropagar()
    {
        // Arrange
        var servicio = Substitute.For<IRestauracionService>();
        servicio.ObtenerUltimaRestauracionAsync(5, Arg.Any<CancellationToken>()).Returns(Restauracion(DateTime.UtcNow));
        var confirmaciones = Substitute.For<IConfirmacionesAvisoRestauracion>();
        confirmaciones.ConfirmarAsync(Arg.Any<int>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("disco lleno"));
        var vm = CrearConAvisoComoGerente(servicio, confirmaciones);
        await vm.CargarAvisoRestauracionAsync();

        // Act
        var act = async () => await vm.CerrarAvisoRestauracionCommand.ExecuteAsync(null);

        // Assert
        await act.Should().NotThrowAsync();
        vm.MostrarAvisoRestauracion.Should().BeFalse();
    }
}

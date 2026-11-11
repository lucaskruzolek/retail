using FluentAssertions;
using NSubstitute;
using Retail.App.Services;
using Retail.App.ViewModels.Ajustes;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class AjustesViewModelTests
{
    private readonly IServicioTema _servicioTema = Substitute.For<IServicioTema>();
    private readonly IPreferenciasUsuario _preferencias = Substitute.For<IPreferenciasUsuario>();

    private AjustesViewModel CrearViewModel()
    {
        return new AjustesViewModel(_servicioTema, _preferencias);
    }

    [Fact]
    public void Constructor_PreferenciaOscura_ArrancaConTemaOscuroSinReaplicar()
    {
        // Arrange
        _preferencias.Tema = TemaVisual.Oscuro;

        // Act
        var viewModel = CrearViewModel();

        // Assert
        viewModel.TemaOscuro.Should().BeTrue();
        viewModel.TemaClaro.Should().BeFalse();
        _servicioTema.DidNotReceive().Aplicar(Arg.Any<TemaVisual>());
        _preferencias.DidNotReceive().Guardar();
    }

    [Fact]
    public void TemaOscuro_CambiaAVerdadero_AplicaYGuardaElTemaOscuro()
    {
        // Arrange
        var viewModel = CrearViewModel();

        // Act
        viewModel.TemaOscuro = true;

        // Assert
        _preferencias.Tema.Should().Be(TemaVisual.Oscuro);
        _servicioTema.Received(1).Aplicar(TemaVisual.Oscuro);
        _preferencias.Received(1).Guardar();
    }

    [Fact]
    public void TemaClaro_SeMarca_AplicaYGuardaElTemaClaro()
    {
        // Arrange
        _preferencias.Tema = TemaVisual.Oscuro;
        var viewModel = CrearViewModel();

        // Act
        viewModel.TemaClaro = true;

        // Assert
        viewModel.TemaOscuro.Should().BeFalse();
        _preferencias.Tema.Should().Be(TemaVisual.Claro);
        _servicioTema.Received(1).Aplicar(TemaVisual.Claro);
        _preferencias.Received(1).Guardar();
    }

    [Fact]
    public void TemaOscuro_MismoValor_NoVuelveAAplicar()
    {
        // Arrange
        var viewModel = CrearViewModel();

        // Act
        viewModel.TemaOscuro = false;

        // Assert
        _servicioTema.DidNotReceive().Aplicar(Arg.Any<TemaVisual>());
    }

    // ------------------------------------------------------------------ Precios (D-38)

    private const int IdGerente = 7;

    private readonly IConfiguracionComercialService _configuracion = Substitute.For<IConfiguracionComercialService>();
    private readonly ICurrentUserSession _session = Substitute.For<ICurrentUserSession>();

    private AjustesViewModel CrearViewModelConPrecios(bool esGerente, RedondeoPrecioEnum vigente = RedondeoPrecioEnum.EnteroMasCercano)
    {
        _session.EsGerente.Returns(esGerente);
        _session.IdUsuario.Returns(IdGerente);
        _configuracion.ObtenerRedondeoPrecioAsync(Arg.Any<CancellationToken>()).Returns(vigente);
        return new AjustesViewModel(_servicioTema, _preferencias, configuracionComercial: _configuracion, session: _session);
    }

    [Fact]
    public async Task CargarAsync_NoGerente_OcultaLaSeccionYNoConsulta()
    {
        // Arrange
        var viewModel = CrearViewModelConPrecios(esGerente: false);

        // Act
        await viewModel.CargarAsync();

        // Assert
        viewModel.MostrarPrecios.Should().BeFalse();
        await _configuracion.DidNotReceive().ObtenerRedondeoPrecioAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CargarAsync_Gerente_SeleccionaLaPoliticaVigenteSinCambiosPendientes()
    {
        // Arrange
        var viewModel = CrearViewModelConPrecios(esGerente: true, RedondeoPrecioEnum.MultiploDe10Superior);

        // Act
        await viewModel.CargarAsync();

        // Assert
        viewModel.MostrarPrecios.Should().BeTrue();
        viewModel.RedondeoSeleccionado!.Valor.Should().Be(RedondeoPrecioEnum.MultiploDe10Superior);
        viewModel.HayCambioDeRedondeo.Should().BeFalse();
        viewModel.AplicarRedondeoCommand.CanExecute(null).Should().BeFalse();
        viewModel.EjemploRedondeo.Should().Be("Ejemplo: $ 1.663,40 → $ 1.670");
    }

    [Fact]
    public async Task AplicarRedondeo_ConOtraPoliticaElegida_LaGuardaConElActorYQuedaSinCambiosPendientes()
    {
        // Arrange
        var viewModel = CrearViewModelConPrecios(esGerente: true);
        await viewModel.CargarAsync();
        viewModel.RedondeoSeleccionado = viewModel.OpcionesRedondeo.Single(o => o.Valor == RedondeoPrecioEnum.MultiploDe100Inferior);

        // Act
        await viewModel.AplicarRedondeoCommand.ExecuteAsync(null);

        // Assert
        await _configuracion.Received(1).CambiarRedondeoPrecioAsync(IdGerente, RedondeoPrecioEnum.MultiploDe100Inferior, Arg.Any<CancellationToken>());
        viewModel.HayCambioDeRedondeo.Should().BeFalse();
        viewModel.MensajeRedondeoEsError.Should().BeFalse();
        viewModel.MensajeRedondeo.Should().StartWith("Guardado.");
    }

    [Fact]
    public async Task AplicarRedondeo_ElServicioLoRechaza_MuestraElErrorYConservaElCambioPendiente()
    {
        // Arrange
        var viewModel = CrearViewModelConPrecios(esGerente: true);
        await viewModel.CargarAsync();
        viewModel.RedondeoSeleccionado = viewModel.OpcionesRedondeo.Single(o => o.Valor == RedondeoPrecioEnum.MultiploDe5Superior);
        _configuracion.CambiarRedondeoPrecioAsync(Arg.Any<int>(), Arg.Any<RedondeoPrecioEnum>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new OperacionNoAutorizadaException("Solo un Gerente puede cambiar el redondeo de los precios.")));

        // Act
        await viewModel.AplicarRedondeoCommand.ExecuteAsync(null);

        // Assert
        viewModel.MensajeRedondeoEsError.Should().BeTrue();
        viewModel.MensajeRedondeo.Should().Be("Solo un Gerente puede cambiar el redondeo de los precios.");
        viewModel.HayCambioDeRedondeo.Should().BeTrue();
    }
}

using FluentAssertions;
using NSubstitute;
using Retail.App.Services;
using Retail.App.ViewModels.Ajustes;
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
}

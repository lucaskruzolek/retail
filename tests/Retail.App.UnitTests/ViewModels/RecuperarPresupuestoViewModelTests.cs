using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Helpers;
using Retail.App.UnitTests.Snapshots;
using Retail.App.ViewModels.Presupuestos;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class RecuperarPresupuestoViewModelTests : IDisposable
{
    private readonly IPresupuestoService _presupuestoService = Substitute.For<IPresupuestoService>();
    private readonly CargaSerializada _acceso = new();
    private readonly RecuperarPresupuestoViewModel _sut;

    public RecuperarPresupuestoViewModelTests()
    {
        _sut = new RecuperarPresupuestoViewModel(_presupuestoService, _acceso, NullLogger.Instance);
    }

    public void Dispose()
    {
        _acceso.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void RecuperarCommand_SinNumero_EstaDeshabilitado()
    {
        _sut.RecuperarCommand.CanExecute(null).Should().BeFalse();

        _sut.Numero = "123";

        _sut.RecuperarCommand.CanExecute(null).Should().BeTrue();
    }

    [Theory]
    [InlineData("123", 123)]
    [InlineData("00000123", 123)]
    [InlineData("  45 ", 45)]
    public async Task Recuperar_ConUnNumeroValido_DevuelveElPresupuesto(string texto, int id)
    {
        // Arrange
        var presupuesto = DatosDeMuestra.PresupuestoParaVenta(vigente: true);
        _presupuestoService.RecuperarParaVentaAsync(id, Arg.Any<CancellationToken>()).Returns(presupuesto);
        _sut.Numero = texto;

        // Act
        await _sut.RecuperarCommand.ExecuteAsync(null);

        // Assert
        _sut.Resultado.Should().BeSameAs(presupuesto);
        _sut.Recuperado.Should().BeTrue();
        _sut.TieneMensajeError.Should().BeFalse();
        _sut.IsBusy.Should().BeFalse();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("0")]
    [InlineData("12.5")]
    public async Task Recuperar_ConUnNumeroInvalido_MuestraElErrorSinLlamarAlServicio(string texto)
    {
        _sut.Numero = texto;

        await _sut.RecuperarCommand.ExecuteAsync(null);

        _sut.TieneMensajeError.Should().BeTrue();
        _sut.Recuperado.Should().BeFalse();
        await _presupuestoService.DidNotReceiveWithAnyArgs().RecuperarParaVentaAsync(default, default);
    }

    [Fact]
    public async Task Recuperar_NoExiste_MuestraElMotivoDentroDelDialogo()
    {
        _presupuestoService.RecuperarParaVentaAsync(999, Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("No se encontró el presupuesto 999."));
        _sut.Numero = "999";

        await _sut.RecuperarCommand.ExecuteAsync(null);

        _sut.MensajeError.Should().Be("No se encontró el presupuesto 999.");
        _sut.Recuperado.Should().BeFalse();
        _sut.IsBusy.Should().BeFalse();
    }

    [Theory]
    [InlineData(EstadoPresupuestoEnum.Convertido, "ya se convirtió en una venta")]
    [InlineData(EstadoPresupuestoEnum.Anulado, "está anulado")]
    public async Task Recuperar_YaConvertidoOAnulado_ExplicaPorQueNoSePuede(EstadoPresupuestoEnum estado, string fragmento)
    {
        _presupuestoService.RecuperarParaVentaAsync(123, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TransicionPresupuestoInvalidaException(estado, "recuperar para la venta"));
        _sut.Numero = "123";

        await _sut.RecuperarCommand.ExecuteAsync(null);

        _sut.MensajeError.Should().Contain("00000123").And.Contain(fragmento);
        _sut.Recuperado.Should().BeFalse();
    }

    [Fact]
    public async Task Recuperar_ErrorInesperado_MuestraUnMensajeGenericoYPermiteReintentar()
    {
        _presupuestoService.RecuperarParaVentaAsync(123, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Timeout de SQL"));
        _sut.Numero = "123";

        await _sut.RecuperarCommand.ExecuteAsync(null);

        _sut.MensajeError.Should().NotBeNullOrEmpty().And.NotContain("Timeout");
        _sut.RecuperarCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task CambiarElNumero_DespuesDeUnError_LimpiaElMensaje()
    {
        _presupuestoService.RecuperarParaVentaAsync(999, Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("No se encontró el presupuesto 999."));
        _sut.Numero = "999";
        await _sut.RecuperarCommand.ExecuteAsync(null);

        _sut.Numero = "123";

        _sut.TieneMensajeError.Should().BeFalse();
    }
}

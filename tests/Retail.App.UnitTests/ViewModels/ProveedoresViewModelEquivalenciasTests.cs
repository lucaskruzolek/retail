using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Retail.App.Services;
using Retail.App.ViewModels.Proveedores;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class ProveedoresViewModelEquivalenciasTests : IDisposable
{
    private readonly IProveedorDialogService _dialogService = Substitute.For<IProveedorDialogService>();
    private readonly ProveedoresViewModel _sut;

    public ProveedoresViewModelEquivalenciasTests()
    {
        _sut = new ProveedoresViewModel(
            Substitute.For<IProveedorService>(),
            _dialogService,
            NullLogger<ProveedoresViewModel>.Instance);
    }

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task AbrirEquivalenciasCommand_ConProveedorSeleccionado_AbreElDialogoParaEseProveedor()
    {
        var proveedor = new ProveedorDto { IdProveedor = 4, RazonSocial = "Mayorista Sur", Cuit = "30-22222222-2" };
        _sut.ProveedorSeleccionado = proveedor;

        await _sut.AbrirEquivalenciasCommand.ExecuteAsync(null);

        await _dialogService.Received(1).AbrirEquivalenciasProveedorAsync(proveedor);
        _sut.MensajeError.Should().BeNull();
    }

    [Fact]
    public async Task AbrirEquivalenciasCommand_ConParametro_AbreElDialogoParaElProveedorDeLaFila()
    {
        var seleccionado = new ProveedorDto { IdProveedor = 1, RazonSocial = "Uno", Cuit = "30-11111111-1" };
        var deLaFila = new ProveedorDto { IdProveedor = 2, RazonSocial = "Dos", Cuit = "30-22222222-2" };
        _sut.ProveedorSeleccionado = seleccionado;

        await _sut.AbrirEquivalenciasCommand.ExecuteAsync(deLaFila);

        await _dialogService.Received(1).AbrirEquivalenciasProveedorAsync(deLaFila);
        await _dialogService.DidNotReceive().AbrirEquivalenciasProveedorAsync(seleccionado);
    }

    [Fact]
    public async Task AbrirEquivalenciasCommand_SinProveedor_MuestraErrorYNoAbreElDialogo()
    {
        _sut.ProveedorSeleccionado = null;

        await _sut.AbrirEquivalenciasCommand.ExecuteAsync(null);

        await _dialogService.DidNotReceive().AbrirEquivalenciasProveedorAsync(Arg.Any<ProveedorDto>());
        _sut.MensajeError.Should().NotBeNullOrWhiteSpace();
    }
}

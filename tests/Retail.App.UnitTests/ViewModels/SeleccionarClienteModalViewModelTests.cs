using FluentAssertions;
using NSubstitute;
using Retail.App.ViewModels.Ventas;
using Retail.Application.DTOs.Clientes;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class SeleccionarClienteModalViewModelTests
{
    private readonly IClienteService _clienteServiceMock = Substitute.For<IClienteService>();

    [Fact]
    public async Task CargarInicialAsync_AlAbrir_BuscaConElTerminoVacioYMuestraLosClientes()
    {
        // Arrange
        _clienteServiceMock.BuscarClientesAsync(string.Empty, Arg.Any<CancellationToken>())
            .Returns([Cliente(1, "Escuela N.º 12"), Cliente(2, "Papelera San Martín S.R.L.")]);
        using var viewModel = new SeleccionarClienteModalViewModel(_clienteServiceMock);

        // Act
        await viewModel.CargarInicialAsync();

        // Assert
        await _clienteServiceMock.Received(1).BuscarClientesAsync(string.Empty, Arg.Any<CancellationToken>());
        viewModel.Clientes.Select(c => c.IdCliente).Should().Equal(1, 2);
    }

    [Fact]
    public async Task CargarInicialAsync_ConClientesYaCargados_NoVuelveABuscar()
    {
        // Arrange
        _clienteServiceMock.BuscarClientesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([Cliente(1, "Escuela N.º 12")]);
        using var viewModel = new SeleccionarClienteModalViewModel(_clienteServiceMock);
        await viewModel.CargarInicialAsync();

        // Act
        await viewModel.CargarInicialAsync();

        // Assert
        await _clienteServiceMock.Received(1).BuscarClientesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static ClienteDto Cliente(int id, string nombre)
    {
        return new ClienteDto
        {
            IdCliente = id,
            RazonSocialONombre = nombre,
            TipoDocumento = TipoDocumentoEnum.Cuit,
            NumeroDocumento = "30711111114",
            CondicionIva = CondicionIvaEnum.ResponsableInscripto,
            TieneCuentaCorriente = false,
            LimiteCredito = 0m,
            SaldoCuentaCorriente = 0m
        };
    }
}

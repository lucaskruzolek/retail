using FluentAssertions;
using NSubstitute;
using Retail.App.Services;
using Retail.App.ViewModels.Clientes;
using Retail.Application.DTOs.Clientes;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class ClientesViewModelTests
{
    private readonly IClienteService _clienteService;
    private readonly IClienteDialogService _dialogService;
    private readonly ClientesViewModel _sut;

    private readonly List<ClienteDto> _clientesEjemplo =
    [
        new()
        {
            IdCliente = 1,
            RazonSocialONombre = "Librería San Martín",
            TipoDocumento = TipoDocumentoEnum.Cuit,
            NumeroDocumento = "30-11111111-1",
            CondicionIva = CondicionIvaEnum.ResponsableInscripto,
            TieneCuentaCorriente = true,
            LimiteCredito = 50000m,
            SaldoCuentaCorriente = 15000m
        },
        new()
        {
            IdCliente = 2,
            RazonSocialONombre = "Juan Pérez",
            TipoDocumento = TipoDocumentoEnum.Dni,
            NumeroDocumento = "20-22222222-2",
            CondicionIva = CondicionIvaEnum.ConsumidorFinal,
            TieneCuentaCorriente = false,
            LimiteCredito = 0m,
            SaldoCuentaCorriente = 0m
        },
        new()
        {
            IdCliente = 3,
            RazonSocialONombre = "Escuela Nro 5",
            TipoDocumento = TipoDocumentoEnum.Cuit,
            NumeroDocumento = "30-33333333-3",
            CondicionIva = CondicionIvaEnum.Exento,
            TieneCuentaCorriente = true,
            LimiteCredito = 80000m,
            SaldoCuentaCorriente = 0m
        }
    ];

    public ClientesViewModelTests()
    {
        _clienteService = Substitute.For<IClienteService>();
        _dialogService = Substitute.For<IClienteDialogService>();

        _sut = new ClientesViewModel(
            _clienteService,
            _dialogService);
    }

    [Fact]
    public async Task CargarClientesAsync_DebeCargarClientesYCalcularMetricas()
    {
        // Arrange
        _clienteService.BuscarClientesAsync(string.Empty, Arg.Any<CancellationToken>())
            .Returns(_clientesEjemplo);

        // Act
        await _sut.CargarClientesCommand.ExecuteAsync(null);

        // Assert
        _sut.Clientes.Should().HaveCount(3);
        _sut.TotalClientes.Should().Be(3);
        _sut.TotalClientesConDeuda.Should().Be(1);
        _sut.TotalDeudaCartera.Should().Be(15000m);
        _sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task TextoBusqueda_FiltraPorNombreYDocumento()
    {
        // Arrange
        _clienteService.BuscarClientesAsync(string.Empty, Arg.Any<CancellationToken>())
            .Returns(_clientesEjemplo);
        await _sut.CargarClientesCommand.ExecuteAsync(null);

        // Act - Buscar por documento
        _sut.TextoBusqueda = "22222222";

        // Assert
        _sut.Clientes.Should().ContainSingle(c => c.RazonSocialONombre == "Juan Pérez");

        // Act - Buscar por nombre
        _sut.TextoBusqueda = "Escuela";

        // Assert
        _sut.Clientes.Should().ContainSingle(c => c.RazonSocialONombre == "Escuela Nro 5");
    }

    [Fact]
    public async Task SoloConDeuda_FiltraSoloClientesConSaldoDeudor()
    {
        // Arrange
        _clienteService.BuscarClientesAsync(string.Empty, Arg.Any<CancellationToken>())
            .Returns(_clientesEjemplo);
        await _sut.CargarClientesCommand.ExecuteAsync(null);

        // Act
        _sut.SoloConDeuda = true;

        // Assert
        _sut.Clientes.Should().ContainSingle(c => c.RazonSocialONombre == "Librería San Martín");
    }

    [Fact]
    public async Task SoloConCuentaCorriente_FiltraSoloClientesConCuentaCorriente()
    {
        // Arrange
        _clienteService.BuscarClientesAsync(string.Empty, Arg.Any<CancellationToken>())
            .Returns(_clientesEjemplo);
        await _sut.CargarClientesCommand.ExecuteAsync(null);

        // Act
        _sut.SoloConCuentaCorriente = true;

        // Assert
        _sut.Clientes.Should().HaveCount(2);
        _sut.Clientes.Should().NotContain(c => c.RazonSocialONombre == "Juan Pérez");
    }

    [Fact]
    public async Task NuevoCliente_AbreDialogoYAgregaALaLista()
    {
        // Arrange
        _clienteService.BuscarClientesAsync(string.Empty, Arg.Any<CancellationToken>())
            .Returns(new List<ClienteDto>(_clientesEjemplo));
        await _sut.CargarClientesCommand.ExecuteAsync(null);

        var nuevoClienteDto = new ClienteDto
        {
            IdCliente = 4,
            RazonSocialONombre = "Biblioteca Popular",
            TipoDocumento = TipoDocumentoEnum.Cuit,
            NumeroDocumento = "30-44444444-4",
            CondicionIva = CondicionIvaEnum.Exento,
            TieneCuentaCorriente = true,
            LimiteCredito = 30000m,
            SaldoCuentaCorriente = 0m
        };

        _clienteService.CrearClienteAsync(Arg.Any<CrearClienteDto>(), Arg.Any<CancellationToken>())
            .Returns(nuevoClienteDto);

        _dialogService.MostrarDialogoCrear(Arg.Do<Func<CrearClienteDto, Task>>(async callback =>
        {
            var dto = new CrearClienteDto
            {
                RazonSocialONombre = "Biblioteca Popular",
                TipoDocumento = TipoDocumentoEnum.Cuit,
                NumeroDocumento = "30-44444444-4",
                CondicionIva = CondicionIvaEnum.Exento,
                TieneCuentaCorriente = true,
                LimiteCredito = 30000m
            };
            await callback(dto);
        }));

        // Act
        _sut.NuevoClienteCommand.Execute(null);

        // Assert
        _sut.Clientes.Should().Contain(c => c.RazonSocialONombre == "Biblioteca Popular");
        _sut.TotalClientes.Should().Be(4);
    }

    [Fact]
    public void EditarCliente_SinSeleccion_NoAbreDialogo()
    {
        // Arrange
        _sut.ClienteSeleccionado = null;

        // Act
        _sut.EditarClienteCommand.Execute(null);

        // Assert
        _dialogService.DidNotReceiveWithAnyArgs().MostrarDialogoModificar(default!, default!);
    }

    [Fact]
    public async Task EditarCliente_ConSeleccion_AbreDialogoYActualizaCliente()
    {
        // Arrange
        _clienteService.BuscarClientesAsync(string.Empty, Arg.Any<CancellationToken>())
            .Returns(new List<ClienteDto>(_clientesEjemplo));
        await _sut.CargarClientesCommand.ExecuteAsync(null);

        _sut.ClienteSeleccionado = _sut.Clientes.First(c => c.IdCliente == 2);

        var clienteActualizado = new ClienteDto
        {
            IdCliente = 2,
            RazonSocialONombre = "Juan Pérez Modificado",
            TipoDocumento = TipoDocumentoEnum.Dni,
            NumeroDocumento = "20-22222222-2",
            CondicionIva = CondicionIvaEnum.ConsumidorFinal,
            TieneCuentaCorriente = true,
            LimiteCredito = 15000m,
            SaldoCuentaCorriente = 0m
        };

        _clienteService.ObtenerClientePorIdAsync(2, Arg.Any<CancellationToken>())
            .Returns(clienteActualizado);

        _dialogService.MostrarDialogoModificar(Arg.Any<ClienteDto>(), Arg.Do<Func<ActualizarClienteDto, Task>>(async callback =>
        {
            var dto = new ActualizarClienteDto
            {
                IdCliente = 2,
                RazonSocialONombre = "Juan Pérez Modificado",
                TipoDocumento = TipoDocumentoEnum.Dni,
                NumeroDocumento = "20-22222222-2",
                CondicionIva = CondicionIvaEnum.ConsumidorFinal,
                TieneCuentaCorriente = true,
                LimiteCredito = 15000m
            };
            await callback(dto);
        }));

        // Act
        _sut.EditarClienteCommand.Execute(null);

        // Assert
        await _clienteService.Received(1).ActualizarClienteAsync(Arg.Any<ActualizarClienteDto>(), Arg.Any<CancellationToken>());
        _sut.Clientes.Should().Contain(c => c.RazonSocialONombre == "Juan Pérez Modificado");
    }

    [Fact]
    public async Task DarDeBajaClienteAsync_ConSaldoDeudor_MuestraErrorYNoElimina()
    {
        // Arrange
        _clienteService.BuscarClientesAsync(string.Empty, Arg.Any<CancellationToken>())
            .Returns(new List<ClienteDto>(_clientesEjemplo));
        await _sut.CargarClientesCommand.ExecuteAsync(null);

        // Cliente 1 tiene SaldoCuentaCorriente = 15000m
        _sut.ClienteSeleccionado = _sut.Clientes.First(c => c.IdCliente == 1);

        // Act
        await _sut.DarDeBajaClienteCommand.ExecuteAsync(null);

        // Assert
        _dialogService.Received(1).MostrarError(Arg.Is<string>(s => s.Contains("Denegada")), Arg.Any<string>());
        await _clienteService.DidNotReceive().BajaClienteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DarDeBajaClienteAsync_SinSaldoYConfirmado_EliminaYActualizaLista()
    {
        // Arrange
        _clienteService.BuscarClientesAsync(string.Empty, Arg.Any<CancellationToken>())
            .Returns(new List<ClienteDto>(_clientesEjemplo));
        await _sut.CargarClientesCommand.ExecuteAsync(null);

        // Cliente 2 tiene SaldoCuentaCorriente = 0
        _sut.ClienteSeleccionado = _sut.Clientes.First(c => c.IdCliente == 2);
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        // Act
        await _sut.DarDeBajaClienteCommand.ExecuteAsync(null);

        // Assert
        await _clienteService.Received(1).BajaClienteAsync(2, Arg.Any<CancellationToken>());
        _sut.Clientes.Should().NotContain(c => c.IdCliente == 2);
        _sut.TotalClientes.Should().Be(2);
    }

    [Fact]
    public async Task DarDeBajaClienteAsync_CanceladoPorUsuario_NoElimina()
    {
        // Arrange
        _clienteService.BuscarClientesAsync(string.Empty, Arg.Any<CancellationToken>())
            .Returns(new List<ClienteDto>(_clientesEjemplo));
        await _sut.CargarClientesCommand.ExecuteAsync(null);

        _sut.ClienteSeleccionado = _sut.Clientes.First(c => c.IdCliente == 2);
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        // Act
        await _sut.DarDeBajaClienteCommand.ExecuteAsync(null);

        // Assert
        await _clienteService.DidNotReceive().BajaClienteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        _sut.Clientes.Should().Contain(c => c.IdCliente == 2);
    }

    [Fact]
    public async Task CobrarCuentaCorrienteAsync_ConClienteYSaldoDeudor_InvocaDialogoYActualizaSaldoLocal()
    {
        // Arrange
        _clienteService.BuscarClientesAsync(string.Empty, Arg.Any<CancellationToken>())
            .Returns(new List<ClienteDto>(_clientesEjemplo));
        await _sut.CargarClientesCommand.ExecuteAsync(null);

        // Cliente 1 tiene Deuda = 15000
        _sut.ClienteSeleccionado = _sut.Clientes.First(c => c.IdCliente == 1);

        var resultadoCobranza = new CobranzaResultadoDto
        {
            IdCobranza = 50,
            IdCliente = 1,
            ClienteNombre = "Librería San Martín",
            FechaHora = DateTime.UtcNow,
            MedioPago = MedioPagoEnum.Efectivo,
            MontoAbonado = 5000m,
            SaldoAnterior = 15000m,
            NuevoSaldo = 10000m
        };

        _dialogService.MostrarCobranzaModalAsync(Arg.Is<ClienteDto>(c => c.IdCliente == 1))
            .Returns(resultadoCobranza);

        // Act
        await _sut.CobrarCuentaCorrienteCommand.ExecuteAsync(null);

        // Assert
        await _dialogService.Received(1).MostrarCobranzaModalAsync(Arg.Is<ClienteDto>(c => c.IdCliente == 1));
        var clienteActualizado = _sut.Clientes.First(c => c.IdCliente == 1);
        clienteActualizado.SaldoCuentaCorriente.Should().Be(10000m);
        _sut.TotalDeudaCartera.Should().Be(10000m);
    }

    [Fact]
    public async Task CobrarCuentaCorrienteAsync_SinClienteSeleccionado_MuestraAviso()
    {
        // Arrange
        _sut.ClienteSeleccionado = null;

        // Act
        await _sut.CobrarCuentaCorrienteCommand.ExecuteAsync(null);

        // Assert
        _dialogService.Received(1).MostrarInformacion("Atención", Arg.Any<string>());
        await _dialogService.DidNotReceive().MostrarCobranzaModalAsync(Arg.Any<ClienteDto>());
    }

    [Fact]
    public async Task CobrarCuentaCorrienteAsync_ClienteSinDeuda_MuestraAvisoSinAbrirModal()
    {
        // Arrange
        var clienteSinDeuda = new ClienteDto
        {
            IdCliente = 20,
            RazonSocialONombre = "Cliente Al Día",
            TipoDocumento = TipoDocumentoEnum.Dni,
            NumeroDocumento = "11223344",
            CondicionIva = CondicionIvaEnum.ConsumidorFinal,
            TieneCuentaCorriente = true,
            LimiteCredito = 50000m,
            SaldoCuentaCorriente = 0m
        };
        _sut.ClienteSeleccionado = clienteSinDeuda;

        // Act
        await _sut.CobrarCuentaCorrienteCommand.ExecuteAsync(null);

        // Assert
        _dialogService.Received(1).MostrarInformacion("Sin Saldo Deudor", Arg.Any<string>());
        await _dialogService.DidNotReceive().MostrarCobranzaModalAsync(Arg.Any<ClienteDto>());
    }

    [Fact]
    public async Task Paginacion_ConTamanoInferior_DivideEnPaginasYNavega()
    {
        // Arrange
        _clienteService.BuscarClientesAsync(string.Empty, Arg.Any<CancellationToken>())
            .Returns(_clientesEjemplo);
        await _sut.CargarClientesCommand.ExecuteAsync(null);

        // Act - Ajustar tamaño a 2 (con 3 clientes => 2 páginas)
        _sut.TamanoPagina = 2;

        // Assert página 1
        _sut.TotalPaginas.Should().Be(2);
        _sut.PaginaActual.Should().Be(1);
        _sut.Clientes.Should().HaveCount(2);
        _sut.PuedeAvanzarPagina.Should().BeTrue();
        _sut.PuedeRetrocederPagina.Should().BeFalse();

        // Act - Avanzar
        _sut.PaginaSiguienteCommand.Execute(null);

        // Assert página 2
        _sut.PaginaActual.Should().Be(2);
        _sut.Clientes.Should().HaveCount(1);
        _sut.PuedeAvanzarPagina.Should().BeFalse();
        _sut.PuedeRetrocederPagina.Should().BeTrue();

        // Act - Retroceder
        _sut.PaginaAnteriorCommand.Execute(null);
        _sut.PaginaActual.Should().Be(1);
        _sut.Clientes.Should().HaveCount(2);

        // Act - Última página
        _sut.UltimaPaginaCommand.Execute(null);
        _sut.PaginaActual.Should().Be(2);

        // Act - Primera página
        _sut.PrimeraPaginaCommand.Execute(null);
        _sut.PaginaActual.Should().Be(1);
    }

    [Fact]
    public async Task Paginacion_CambioDeFiltro_ReiniciaAPaginaUno()
    {
        // Arrange
        _clienteService.BuscarClientesAsync(string.Empty, Arg.Any<CancellationToken>())
            .Returns(_clientesEjemplo);
        await _sut.CargarClientesCommand.ExecuteAsync(null);

        _sut.TamanoPagina = 1;
        _sut.PaginaSiguienteCommand.Execute(null);
        _sut.PaginaActual.Should().Be(2);

        // Act - Cambiar filtro de búsqueda
        _sut.TextoBusqueda = "Escuela";

        // Assert
        _sut.PaginaActual.Should().Be(1);
        _sut.TotalRegistrosFiltrados.Should().Be(1);
        _sut.Clientes.Should().ContainSingle(c => c.RazonSocialONombre == "Escuela Nro 5");
    }
}

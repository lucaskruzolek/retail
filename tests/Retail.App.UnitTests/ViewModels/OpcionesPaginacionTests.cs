using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Retail.App.Services;
using Retail.App.ViewModels.Articulos;
using Retail.App.ViewModels.Clientes;
using Retail.App.ViewModels.Comun;
using Retail.App.ViewModels.Pedidos;
using Retail.App.ViewModels.Proveedores;
using Retail.App.ViewModels.Usuarios;
using Retail.App.Views.Dev;
using Retail.Application.Interfaces.Services;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class OpcionesPaginacionTests
{
    [Fact]
    public void Tamanos_Definidos_SonQuinceTreintaYSesenta()
    {
        OpcionesPaginacion.Tamanos.Should().Equal(15, 30, 60);
    }

    [Fact]
    public void ArticulosViewModel_TamanoInicial_EstaEntreLasOpcionesDisponibles()
    {
        var viewModel = new ArticulosViewModel(Substitute.For<IInventarioService>(), Substitute.For<IArticuloDialogService>());

        viewModel.TamanosPaginaDisponibles.Should().Contain(viewModel.TamanoPagina);
    }

    [Fact]
    public void ClientesViewModel_TamanoInicial_EstaEntreLasOpcionesDisponibles()
    {
        var viewModel = new ClientesViewModel(Substitute.For<IClienteService>(), Substitute.For<IClienteDialogService>());

        viewModel.TamanosPaginaDisponibles.Should().Contain(viewModel.TamanoPagina);
    }

    [Fact]
    public void PedidosViewModel_TamanoInicial_EstaEntreLasOpcionesDisponibles()
    {
        var viewModel = new PedidosViewModel(
            Substitute.For<IPedidoService>(),
            Substitute.For<IProveedorService>(),
            Substitute.For<IPedidoDialogService>(),
            TimeProvider.System,
            Substitute.For<ILogger<PedidosViewModel>>());

        viewModel.TamanosPaginaDisponibles.Should().Contain(viewModel.TamanoPagina);
    }

    [Fact]
    public void ProveedoresViewModel_TamanoInicial_EstaEntreLasOpcionesDisponibles()
    {
        var viewModel = new ProveedoresViewModel(
            Substitute.For<IProveedorService>(),
            Substitute.For<IProveedorDialogService>(),
            Substitute.For<ILogger<ProveedoresViewModel>>());

        viewModel.TamanosPaginaDisponibles.Should().Contain(viewModel.TamanoPagina);
    }

    [Fact]
    public void UsuariosViewModel_TamanoInicial_EstaEntreLasOpcionesDisponibles()
    {
        var viewModel = new UsuariosViewModel(
            Substitute.For<IUsuarioService>(),
            Substitute.For<IUsuarioDialogService>(),
            Substitute.For<ICurrentUserSession>());

        viewModel.TamanosPaginaDisponibles.Should().Contain(viewModel.TamanoPagina);
    }

    [Fact]
    public void PaginacionMuestra_TamanoInicial_EstaEntreLasOpcionesDisponibles()
    {
        var muestra = new PaginacionMuestra();

        muestra.TamanosPaginaDisponibles.Should().Contain(muestra.TamanoPagina);
    }
}

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Retail.App.Services;
using Retail.App.ViewModels.Proveedores;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class ProveedoresViewModelTests
{
    private readonly IProveedorService _proveedorService;
    private readonly IProveedorDialogService _dialogService;
    private readonly ProveedoresViewModel _sut;

    public ProveedoresViewModelTests()
    {
        _proveedorService = Substitute.For<IProveedorService>();
        _dialogService = Substitute.For<IProveedorDialogService>();

        _sut = new ProveedoresViewModel(
            _proveedorService,
            _dialogService,
            NullLogger<ProveedoresViewModel>.Instance);
    }

    [Fact]
    public async Task CargarProveedoresCommand_Exito_LlenaColeccionYEstablecePaginacion()
    {
        // Arrange
        var proveedores = new List<ProveedorDto>
        {
            new() { IdProveedor = 1, RazonSocial = "Librería del Norte", Cuit = "30-11111111-1" },
            new() { IdProveedor = 2, RazonSocial = "Papelera Sur", Cuit = "30-22222222-2" }
        };

        _proveedorService.ListarProveedoresAsync(Arg.Any<CancellationToken>())
            .Returns(proveedores);

        // Act
        await _sut.CargarProveedoresCommand.ExecuteAsync(null);

        // Assert
        _sut.Proveedores.Should().HaveCount(2);
        _sut.TotalProveedores.Should().Be(2);
        _sut.TotalRegistrosFiltrados.Should().Be(2);
        _sut.PaginaActual.Should().Be(1);
        _sut.IsBusy.Should().BeFalse();
        _sut.MensajeError.Should().BeNull();
    }

    [Fact]
    public async Task TextoBusqueda_AlFiltrar_FiltraEnMemoriaYReiniciaPagina()
    {
        // Arrange
        var proveedores = new List<ProveedorDto>
        {
            new() { IdProveedor = 1, RazonSocial = "Librería del Norte", Cuit = "30-11111111-1", Email = "norte@test.com" },
            new() { IdProveedor = 2, RazonSocial = "Papelera Sur", Cuit = "30-22222222-2", Email = "sur@test.com" }
        };

        _proveedorService.ListarProveedoresAsync(Arg.Any<CancellationToken>())
            .Returns(proveedores);

        await _sut.CargarProveedoresCommand.ExecuteAsync(null);

        // Act
        _sut.TextoBusqueda = "Sur";

        // Assert
        _sut.Proveedores.Should().ContainSingle();
        _sut.Proveedores[0].RazonSocial.Should().Be("Papelera Sur");
        _sut.TotalRegistrosFiltrados.Should().Be(1);
    }

    [Fact]
    public async Task NuevoProveedorCommand_CuandoDialogoRetornaTrue_RecargaProveedores()
    {
        // Arrange
        _dialogService.AbrirFormularioNuevoProveedorAsync().Returns(true);
        _proveedorService.ListarProveedoresAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProveedorDto>());

        // Act
        await _sut.NuevoProveedorCommand.ExecuteAsync(null);

        // Assert
        await _dialogService.Received(1).AbrirFormularioNuevoProveedorAsync();
        await _proveedorService.Received(1).ListarProveedoresAsync(Arg.Any<CancellationToken>());
        _sut.MensajeEstado.Should().Be("Proveedor registrado exitosamente.");
    }

    [Fact]
    public async Task Paginacion_NavegacionEntrePaginas_ActualizaPropiedades()
    {
        // Arrange
        var proveedores = Enumerable.Range(1, 45)
            .Select(i => new ProveedorDto
            {
                IdProveedor = i,
                RazonSocial = $"Proveedor {i:D2}",
                Cuit = $"30-{i:D8}-0"
            })
            .ToList();

        _proveedorService.ListarProveedoresAsync(Arg.Any<CancellationToken>())
            .Returns(proveedores);

        await _sut.CargarProveedoresCommand.ExecuteAsync(null);

        // Assert estado inicial (TamanoPagina = 20)
        _sut.TotalPaginas.Should().Be(3);
        _sut.PaginaActual.Should().Be(1);
        _sut.PuedeRetrocederPagina.Should().BeFalse();
        _sut.PuedeAvanzarPagina.Should().BeTrue();
        _sut.Proveedores.Should().HaveCount(20);

        // Act 1: Avanzar página
        _sut.PaginaSiguienteCommand.Execute(null);

        // Assert 1
        _sut.PaginaActual.Should().Be(2);
        _sut.PuedeRetrocederPagina.Should().BeTrue();
        _sut.PuedeAvanzarPagina.Should().BeTrue();

        // Act 2: Última página
        _sut.UltimaPaginaCommand.Execute(null);

        // Assert 2
        _sut.PaginaActual.Should().Be(3);
        _sut.PuedeAvanzarPagina.Should().BeFalse();
        _sut.Proveedores.Should().HaveCount(5);

        // Act 3: Primera página
        _sut.PrimeraPaginaCommand.Execute(null);

        // Assert 3
        _sut.PaginaActual.Should().Be(1);
        _sut.PuedeRetrocederPagina.Should().BeFalse();
    }

    [Fact]
    public async Task AbrirImportadorCommand_SinSeleccionPrevia_DebeInvocarAbrirImportadorCatalogosConNull()
    {
        // Arrange: sin proveedor seleccionado
        _sut.ProveedorSeleccionado = null;

        // Act
        await _sut.AbrirImportadorCommand.ExecuteAsync(null);

        // Assert
        await _dialogService.Received(1).AbrirImportadorCatalogosAsync(null);
        _sut.MensajeError.Should().BeNull();
    }

    [Fact]
    public async Task AbrirImportadorCommand_ConProveedorSeleccionado_DebeInvocarDialogService()
    {
        // Arrange
        var proveedor = new ProveedorDto
        {
            IdProveedor = 10,
            RazonSocial = "Distribuidora Mayorista",
            Cuit = "30-10101010-1"
        };
        _sut.ProveedorSeleccionado = proveedor;

        // Act
        await _sut.AbrirImportadorCommand.ExecuteAsync(null);

        // Assert
        await _dialogService.Received(1).AbrirImportadorCatalogosAsync(proveedor);
    }

    [Fact]
    public async Task AbrirImportadorCommand_ConParametroProveedor_DebeInvocarDialogServiceDirectamente()
    {
        // Arrange: no hay selección previa en la grilla
        _sut.ProveedorSeleccionado = null;
        var proveedorFila = new ProveedorDto
        {
            IdProveedor = 20,
            RazonSocial = "Papelera Central",
            Cuit = "30-20202020-2"
        };

        // Act
        await _sut.AbrirImportadorCommand.ExecuteAsync(proveedorFila);

        // Assert
        await _dialogService.Received(1).AbrirImportadorCatalogosAsync(proveedorFila);
    }

    [Fact]
    public async Task EditarProveedorCommand_SinProveedor_DebeMostrarMensajeError()
    {
        // Arrange
        _sut.ProveedorSeleccionado = null;

        // Act
        await _sut.EditarProveedorCommand.ExecuteAsync(null);

        // Assert
        _sut.MensajeError.Should().Be("Seleccione un proveedor de la grilla para editar [F4].");
        await _dialogService.DidNotReceive().AbrirFormularioEditarProveedorAsync(Arg.Any<ProveedorDto>());
    }

    [Fact]
    public async Task EditarProveedorCommand_ConParametro_DebeAbrirDialogoYRecargar()
    {
        // Arrange
        var proveedorFila = new ProveedorDto
        {
            IdProveedor = 15,
            RazonSocial = "Librería Central",
            Cuit = "30-15151515-5"
        };
        _dialogService.AbrirFormularioEditarProveedorAsync(proveedorFila).Returns(true);
        _proveedorService.ListarProveedoresAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProveedorDto> { proveedorFila });

        // Act
        await _sut.EditarProveedorCommand.ExecuteAsync(proveedorFila);

        // Assert
        await _dialogService.Received(1).AbrirFormularioEditarProveedorAsync(proveedorFila);
        await _proveedorService.Received(1).ListarProveedoresAsync(Arg.Any<CancellationToken>());
        _sut.MensajeEstado.Should().Be("Proveedor actualizado exitosamente.");
    }

    [Fact]
    public async Task EliminarProveedorCommand_SinProveedor_DebeMostrarMensajeError()
    {
        // Arrange
        _sut.ProveedorSeleccionado = null;

        // Act
        await _sut.EliminarProveedorCommand.ExecuteAsync(null);

        // Assert
        _sut.MensajeError.Should().Be("Seleccione un proveedor de la grilla para dar de baja.");
        await _dialogService.DidNotReceive().ConfirmarEliminacionAsync(Arg.Any<string>());
    }
}

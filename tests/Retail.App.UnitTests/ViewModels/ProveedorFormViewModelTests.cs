using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.ViewModels.Proveedores;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class ProveedorFormViewModelTests
{
    private readonly IProveedorService _proveedorService;
    private readonly ProveedorFormViewModel _sut;

    public ProveedorFormViewModelTests()
    {
        _proveedorService = Substitute.For<IProveedorService>();
        _sut = new ProveedorFormViewModel(_proveedorService);
    }

    [Fact]
    public void ConfigurarAlta_ReiniciaPropiedadesYModo()
    {
        // Act
        _sut.ConfigurarAlta();

        // Assert
        _sut.EsModoEdicion.Should().BeFalse();
        _sut.TituloVentana.Should().Be("Alta de Proveedor Mayorista");
        _sut.IdProveedor.Should().Be(0);
        _sut.RazonSocial.Should().BeEmpty();
        _sut.Cuit.Should().BeEmpty();
        _sut.Telefono.Should().BeNull();
        _sut.Email.Should().BeNull();
        _sut.MensajeError.Should().BeNull();
    }

    [Fact]
    public void ConfigurarEdicion_CargaDatosDelProveedor()
    {
        // Arrange
        var dto = new ProveedorDto
        {
            IdProveedor = 42,
            RazonSocial = "Distribuidora Mayor",
            Cuit = "30-55555555-5",
            Telefono = "011-4321-8765",
            Email = "contacto@mayor.com"
        };

        // Act
        _sut.ConfigurarEdicion(dto);

        // Assert
        _sut.EsModoEdicion.Should().BeTrue();
        _sut.TituloVentana.Should().Be("Modificar Proveedor Mayorista");
        _sut.IdProveedor.Should().Be(42);
        _sut.RazonSocial.Should().Be("Distribuidora Mayor");
        _sut.Cuit.Should().Be("30-55555555-5");
        _sut.Telefono.Should().Be("011-4321-8765");
        _sut.Email.Should().Be("contacto@mayor.com");
    }

    [Fact]
    public async Task GuardarCommand_ModoAlta_LlamaCrearProveedorAsync()
    {
        // Arrange
        _sut.ConfigurarAlta();
        _sut.RazonSocial = "Librería Santa Fe";
        _sut.Cuit = "30-77777777-7";
        _sut.Telefono = "342-123456";
        _sut.Email = "ventas@santafe.com";

        // Act
        await _sut.GuardarCommand.ExecuteAsync(null);

        // Assert
        await _proveedorService.Received(1).CrearProveedorAsync(Arg.Is<CrearProveedorDto>(d =>
            d.RazonSocial == "Librería Santa Fe" &&
            d.Cuit == "30-77777777-7" &&
            d.Telefono == "342-123456" &&
            d.Email == "ventas@santafe.com"
        ), Arg.Any<CancellationToken>());

        _sut.DialogResult.Should().BeTrue();
        _sut.TieneError.Should().BeFalse();
    }

    [Fact]
    public async Task GuardarCommand_ModoEdicion_LlamaActualizarProveedorAsync()
    {
        // Arrange
        _sut.ConfigurarEdicion(new ProveedorDto
        {
            IdProveedor = 10,
            RazonSocial = "Distribuidora Original",
            Cuit = "30-99999999-9"
        });

        _sut.RazonSocial = "Distribuidora Modificada";
        _sut.Telefono = "123456";

        // Act
        await _sut.GuardarCommand.ExecuteAsync(null);

        // Assert
        await _proveedorService.Received(1).ActualizarProveedorAsync(Arg.Is<ProveedorDto>(d =>
            d.IdProveedor == 10 &&
            d.RazonSocial == "Distribuidora Modificada" &&
            d.Telefono == "123456"
        ), Arg.Any<CancellationToken>());

        _sut.DialogResult.Should().BeTrue();
    }

    [Fact]
    public async Task GuardarCommand_CuandoDomainExceptionOcurre_CapturaMensajeEnMensajeError()
    {
        // Arrange
        _sut.ConfigurarAlta();
        _sut.RazonSocial = "Proveedor Repetido";
        _sut.Cuit = "30-11111111-1";

        _proveedorService.CrearProveedorAsync(Arg.Any<CrearProveedorDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("El CUIT ya está en uso."));

        // Act
        await _sut.GuardarCommand.ExecuteAsync(null);

        // Assert
        _sut.DialogResult.Should().BeFalse();
        _sut.TieneError.Should().BeTrue();
        _sut.MensajeError.Should().Be("El CUIT ya está en uso.");
    }

    [Fact]
    public void CancelarCommand_EstableceDialogResultFalse()
    {
        // Act
        _sut.CancelarCommand.Execute(null);

        // Assert
        _sut.DialogResult.Should().BeFalse();
    }
}

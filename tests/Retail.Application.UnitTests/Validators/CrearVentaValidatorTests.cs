using FluentAssertions;
using Retail.Application.DTOs.Ventas;
using Retail.Application.Validators.Ventas;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.Application.UnitTests.Validators;

public class CrearVentaValidatorTests
{
    private readonly CrearVentaValidator _validator = new();

    [Fact]
    public void Validate_VentaConsumidorFinalValida_PasaLaValidacion()
    {
        // Act
        var result = _validator.Validate(CrearDtoValido());

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_SinItems_FallaConMensajeDeVentaVacia()
    {
        // Arrange
        var dto = CrearDtoValido() with { Items = [] };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("sin artículos"));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void Validate_TurnoOUsuarioInvalido_Falla(int idTurno, int idUsuario)
    {
        // Arrange
        var dto = CrearDtoValido() with { IdTurno = idTurno, IdUsuario = idUsuario };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ItemConCantidadCero_Falla()
    {
        // Arrange
        var dto = CrearDtoValido() with { Items = [Item() with { Cantidad = 0 }] };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("cantidad"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Validate_PagoConMontoNoPositivo_Falla(decimal monto)
    {
        // Arrange
        var dto = CrearDtoValido() with { Pagos = [new PagoVentaDto { MedioPago = MedioPagoEnum.Efectivo, Monto = monto }] };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_PagoConMedioInexistente_Falla()
    {
        // Arrange
        var dto = CrearDtoValido() with { Pagos = [new PagoVentaDto { MedioPago = (MedioPagoEnum)99, Monto = 100m }] };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_DescuentoNegativo_Falla()
    {
        // Arrange
        var dto = CrearDtoValido() with { Descuento = -1m };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ConPresupuestoDeOrigen_FallaPorqueRf12NoEstaImplementado()
    {
        // Arrange
        var dto = CrearDtoValido() with { IdPresupuestoOrigen = 7 };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("RF-12"));
    }

    private static DetalleVentaDto Item()
    {
        return new DetalleVentaDto
        {
            IdArticulo = 10,
            Descripcion = "Cuaderno",
            Cantidad = 1,
            PrecioUnitario = 1500m
        };
    }

    private static CrearVentaDto CrearDtoValido()
    {
        return new CrearVentaDto
        {
            IdTurno = 1,
            IdUsuario = 2,
            Items = [Item()],
            Pagos = [new PagoVentaDto { MedioPago = MedioPagoEnum.Efectivo, Monto = 1500m }]
        };
    }
}

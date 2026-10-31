using FluentAssertions;
using Retail.Application.DTOs.Compras;
using Retail.Application.Validators.Compras;
using Xunit;

namespace Retail.Application.UnitTests.Validators;

public class CrearCompraValidatorTests
{
    private readonly CrearCompraValidator _validator = new();

    [Fact]
    public void Validate_DtoValido_PasaLaValidacion()
    {
        // Act
        var result = _validator.Validate(CrearDtoValido());

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void Validate_ProveedorOUsuarioInvalido_Falla(int idProveedor, int idUsuario)
    {
        // Arrange
        var dto = CrearDtoValido() with { IdProveedor = idProveedor, IdUsuario = idUsuario };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_TipoComprobanteVacio_Falla(string tipo)
    {
        // Arrange
        var dto = CrearDtoValido() with { TipoComprobante = tipo };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("tipo de comprobante"));
    }

    [Fact]
    public void Validate_SinItems_FallaConMensajeCorrespondiente()
    {
        // Arrange
        var dto = CrearDtoValido() with { Items = Array.Empty<DetalleCompraDto>() };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("al menos un renglón"));
    }

    [Fact]
    public void Validate_ItemConCantidadCero_Falla()
    {
        // Arrange
        var items = new[] { new DetalleCompraDto { IdArticulo = 1, Cantidad = 0, CostoUnitario = 100m } };
        var dto = CrearDtoValido() with { Items = items };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("al menos 1"));
    }

    [Fact]
    public void Validate_ItemConCostoCero_Falla()
    {
        // Arrange
        var items = new[] { new DetalleCompraDto { IdArticulo = 1, Cantidad = 1, CostoUnitario = 0m } };
        var dto = CrearDtoValido() with { Items = items };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("mayor a cero"));
    }

    [Fact]
    public void Validate_FechaEmisionMuyFutura_Falla()
    {
        // Arrange
        var dto = CrearDtoValido() with { FechaEmision = DateTime.UtcNow.AddDays(30) };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("futura"));
    }

    // ── C-2: Artículos repetidos ──────────────────────────────────────────────

    [Fact]
    public void Validar_ArticulosRepetidos_FallaConMensaje()
    {
        // Arrange — dos renglones con el mismo IdArticulo
        var items = new[]
        {
            new DetalleCompraDto { IdArticulo = 10, Cantidad = 2, CostoUnitario = 100m },
            new DetalleCompraDto { IdArticulo = 10, Cantidad = 1, CostoUnitario = 120m },
        };
        var dto = CrearDtoValido() with { Items = items };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("mismo artículo"));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CrearCompraDto CrearDtoValido() =>
        new()
        {
            IdProveedor = 1,
            IdUsuario = 2,
            TipoComprobante = "FACTURA",
            NumeroComprobante = "0001-00000001",
            FechaEmision = DateTime.UtcNow.AddDays(-1),
            Items =
            [
                new DetalleCompraDto { IdArticulo = 10, Cantidad = 2, CostoUnitario = 150m },
            ],
        };
}

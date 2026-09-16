using FluentAssertions;
using Retail.Application.DTOs.Clientes;
using Retail.Application.Validators.Clientes;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.Application.UnitTests.Validators;

public class RegistrarCobranzaValidatorTests
{
    private readonly RegistrarCobranzaValidator _validator = new();

    [Fact]
    public void Validate_ConParametrosValidos_DebeAprobar()
    {
        // Arrange
        var dto = new RegistrarCobranzaDto
        {
            IdCliente = 1,
            IdTurno = 1,
            IdUsuario = 1,
            Monto = 1500m,
            MedioPago = MedioPagoEnum.Efectivo,
            Referencia = "Cobro en mostrador"
        };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_IdClienteInvalido_DebeFallar(int idClienteInvalido)
    {
        // Arrange
        var dto = new RegistrarCobranzaDto
        {
            IdCliente = idClienteInvalido,
            IdTurno = 1,
            IdUsuario = 1,
            Monto = 1000m,
            MedioPago = MedioPagoEnum.Efectivo
        };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegistrarCobranzaDto.IdCliente));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_IdTurnoInvalido_DebeFallar(int idTurnoInvalido)
    {
        // Arrange
        var dto = new RegistrarCobranzaDto
        {
            IdCliente = 1,
            IdTurno = idTurnoInvalido,
            IdUsuario = 1,
            Monto = 1000m,
            MedioPago = MedioPagoEnum.Efectivo
        };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegistrarCobranzaDto.IdTurno));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Validate_IdUsuarioInvalido_DebeFallar(int idUsuarioInvalido)
    {
        // Arrange
        var dto = new RegistrarCobranzaDto
        {
            IdCliente = 1,
            IdTurno = 1,
            IdUsuario = idUsuarioInvalido,
            Monto = 1000m,
            MedioPago = MedioPagoEnum.Efectivo
        };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegistrarCobranzaDto.IdUsuario));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Validate_MontoCeroONegativo_DebeFallar(decimal montoInvalido)
    {
        // Arrange
        var dto = new RegistrarCobranzaDto
        {
            IdCliente = 1,
            IdTurno = 1,
            IdUsuario = 1,
            Monto = montoInvalido,
            MedioPago = MedioPagoEnum.Efectivo
        };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegistrarCobranzaDto.Monto));
    }

    [Fact]
    public void Validate_MedioPagoInvalido_DebeFallar()
    {
        // Arrange
        var dto = new RegistrarCobranzaDto
        {
            IdCliente = 1,
            IdTurno = 1,
            IdUsuario = 1,
            Monto = 500m,
            MedioPago = (MedioPagoEnum)999
        };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegistrarCobranzaDto.MedioPago));
    }

    [Fact]
    public void Validate_ReferenciaMuyLarga_DebeFallar()
    {
        // Arrange
        var dto = new RegistrarCobranzaDto
        {
            IdCliente = 1,
            IdTurno = 1,
            IdUsuario = 1,
            Monto = 500m,
            MedioPago = MedioPagoEnum.Efectivo,
            Referencia = new string('A', 101)
        };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegistrarCobranzaDto.Referencia));
    }
}

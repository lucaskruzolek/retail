using FluentAssertions;
using Retail.Application.DTOs.Pedidos;
using Retail.Application.Validators.Pedidos;
using Retail.Domain.Entities;
using Xunit;

namespace Retail.Application.UnitTests.Validators;

public class PedidoValidatorTests
{
    private readonly AgregarVariosAlPedidoValidator _agregarValidator = new();
    private readonly CambiarCantidadesPedidoValidator _cambiarValidator = new();
    private readonly CancelarPedidoValidator _cancelarValidator = new();

    private static AgregarVariosAlPedidoDto CrearLote(int idUsuario = 1, params RenglonAAgregarDto[] renglones)
    {
        return new AgregarVariosAlPedidoDto
        {
            IdUsuario = idUsuario,
            Renglones = renglones.Length == 0
                ? new[] { new RenglonAAgregarDto { IdCatalogoProveedor = 1, Cantidad = 1 } }
                : renglones
        };
    }

    [Fact]
    public void AgregarVariosAlPedido_ConDatosValidos_DebeAprobar()
    {
        var dto = CrearLote(
            1,
            new RenglonAAgregarDto { IdCatalogoProveedor = 1, Cantidad = 1 },
            new RenglonAAgregarDto { IdCatalogoProveedor = 2, Cantidad = 5 });

        _agregarValidator.Validate(dto).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AgregarVariosAlPedido_IdUsuarioInvalido_DebeFallar(int id)
    {
        _agregarValidator.Validate(CrearLote(id)).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(AgregarVariosAlPedidoDto.IdUsuario));
    }

    [Fact]
    public void AgregarVariosAlPedido_SinRenglones_DebeFallar()
    {
        var dto = new AgregarVariosAlPedidoDto { IdUsuario = 1 };

        _agregarValidator.Validate(dto).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(AgregarVariosAlPedidoDto.Renglones));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AgregarVariosAlPedido_IdCatalogoInvalido_DebeFallar(int id)
    {
        var dto = CrearLote(1, new RenglonAAgregarDto { IdCatalogoProveedor = id, Cantidad = 1 });

        _agregarValidator.Validate(dto).Errors
            .Should().ContainSingle(e => e.PropertyName.EndsWith(nameof(RenglonAAgregarDto.IdCatalogoProveedor)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void AgregarVariosAlPedido_CantidadInvalida_DebeFallar(int cantidad)
    {
        var dto = CrearLote(1, new RenglonAAgregarDto { IdCatalogoProveedor = 1, Cantidad = cantidad });

        _agregarValidator.Validate(dto).Errors
            .Should().ContainSingle(e => e.PropertyName.EndsWith(nameof(RenglonAAgregarDto.Cantidad)));
    }

    [Fact]
    public void AgregarVariosAlPedido_ItemRepetido_DebeFallar()
    {
        var dto = CrearLote(
            1,
            new RenglonAAgregarDto { IdCatalogoProveedor = 7, Cantidad = 1 },
            new RenglonAAgregarDto { IdCatalogoProveedor = 7, Cantidad = 2 });

        _agregarValidator.Validate(dto).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(AgregarVariosAlPedidoDto.Renglones)
                && e.ErrorMessage.Contains("repetirse"));
    }

    private static CambiarCantidadesPedidoDto CrearCambio(int idPedido = 1, params CantidadRenglonDto[] renglones)
    {
        return new CambiarCantidadesPedidoDto
        {
            IdPedido = idPedido,
            Renglones = renglones.Length == 0
                ? new[] { new CantidadRenglonDto { IdCatalogoProveedor = 1, Cantidad = 5 } }
                : renglones
        };
    }

    [Fact]
    public void CambiarCantidades_ConDatosValidos_DebeAprobar()
    {
        var dto = CrearCambio(
            1,
            new CantidadRenglonDto { IdCatalogoProveedor = 1, Cantidad = 5 },
            new CantidadRenglonDto { IdCatalogoProveedor = 2, Cantidad = 12 });

        _cambiarValidator.Validate(dto).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CambiarCantidades_IdPedidoInvalido_DebeFallar(int id)
    {
        _cambiarValidator.Validate(CrearCambio(id)).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(CambiarCantidadesPedidoDto.IdPedido));
    }

    [Fact]
    public void CambiarCantidades_SinRenglones_DebeFallar()
    {
        var dto = new CambiarCantidadesPedidoDto { IdPedido = 1 };

        _cambiarValidator.Validate(dto).Errors
            .Should().Contain(e => e.PropertyName == nameof(CambiarCantidadesPedidoDto.Renglones));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CambiarCantidades_IdCatalogoInvalido_DebeFallar(int id)
    {
        var dto = CrearCambio(1, new CantidadRenglonDto { IdCatalogoProveedor = id, Cantidad = 5 });

        _cambiarValidator.Validate(dto).Errors
            .Should().ContainSingle(e => e.PropertyName.EndsWith(nameof(CantidadRenglonDto.IdCatalogoProveedor)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void CambiarCantidades_CantidadInvalida_DebeFallar(int cantidad)
    {
        var dto = CrearCambio(1, new CantidadRenglonDto { IdCatalogoProveedor = 1, Cantidad = cantidad });

        _cambiarValidator.Validate(dto).Errors
            .Should().ContainSingle(e => e.PropertyName.EndsWith(nameof(CantidadRenglonDto.Cantidad)));
    }

    [Fact]
    public void CambiarCantidades_RenglonRepetido_DebeFallar()
    {
        var dto = CrearCambio(
            1,
            new CantidadRenglonDto { IdCatalogoProveedor = 3, Cantidad = 1 },
            new CantidadRenglonDto { IdCatalogoProveedor = 3, Cantidad = 2 });

        _cambiarValidator.Validate(dto).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(CambiarCantidadesPedidoDto.Renglones)
                && e.ErrorMessage.Contains("dos veces"));
    }

    [Fact]
    public void CancelarPedido_ConDatosValidos_DebeAprobar()
    {
        var dto = new CancelarPedidoDto { IdPedido = 1, Motivo = "El proveedor discontinuó el título" };

        _cancelarValidator.Validate(dto).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CancelarPedido_IdPedidoInvalido_DebeFallar(int id)
    {
        var dto = new CancelarPedidoDto { IdPedido = id, Motivo = "Motivo" };

        _cancelarValidator.Validate(dto).Errors.Should().ContainSingle(e => e.PropertyName == nameof(CancelarPedidoDto.IdPedido));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CancelarPedido_MotivoVacio_DebeFallar(string motivo)
    {
        var dto = new CancelarPedidoDto { IdPedido = 1, Motivo = motivo };

        _cancelarValidator.Validate(dto).Errors.Should().Contain(e => e.PropertyName == nameof(CancelarPedidoDto.Motivo));
    }

    [Fact]
    public void CancelarPedido_MotivoDeLargoMaximo_DebeAprobar()
    {
        var dto = new CancelarPedidoDto { IdPedido = 1, Motivo = new string('a', Pedido.LongitudMaximaMotivoCancelacion) };

        Pedido.LongitudMaximaMotivoCancelacion.Should().Be(250);
        _cancelarValidator.Validate(dto).IsValid.Should().BeTrue();
    }

    [Fact]
    public void CancelarPedido_MotivoExcedido_DebeFallar()
    {
        var dto = new CancelarPedidoDto { IdPedido = 1, Motivo = new string('a', Pedido.LongitudMaximaMotivoCancelacion + 1) };

        _cancelarValidator.Validate(dto).Errors.Should().ContainSingle(e => e.PropertyName == nameof(CancelarPedidoDto.Motivo));
    }
}

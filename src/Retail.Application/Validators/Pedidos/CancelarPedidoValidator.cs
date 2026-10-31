using FluentValidation;
using Retail.Application.DTOs.Pedidos;
using Retail.Domain.Entities;

namespace Retail.Application.Validators.Pedidos;

/// <summary>
/// Validador de frontera para cancelar un pedido (RF-28). El largo máximo reutiliza la constante del dominio para
/// que la frontera y la raíz coincidan.
/// </summary>
public class CancelarPedidoValidator : AbstractValidator<CancelarPedidoDto>
{
    public CancelarPedidoValidator()
    {
        RuleFor(x => x.IdPedido)
            .GreaterThan(0).WithMessage("El identificador del pedido debe ser válido.");

        RuleFor(x => x.Motivo)
            .NotEmpty().WithMessage("Debe indicarse el motivo de la cancelación del pedido.")
            .MaximumLength(Pedido.LongitudMaximaMotivoCancelacion)
            .WithMessage($"El motivo de la cancelación no puede superar los {Pedido.LongitudMaximaMotivoCancelacion} caracteres.");
    }
}

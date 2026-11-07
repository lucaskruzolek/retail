using FluentValidation;
using Retail.Application.DTOs.Pedidos;

namespace Retail.Application.Validators.Pedidos;

/// <summary>
/// Validador de frontera para cambiar las cantidades de los renglones de un pedido (RF-28).
/// </summary>
public class CambiarCantidadesPedidoValidator : AbstractValidator<CambiarCantidadesPedidoDto>
{
    public CambiarCantidadesPedidoValidator()
    {
        RuleFor(x => x.IdPedido)
            .GreaterThan(0).WithMessage("El identificador del pedido debe ser válido.");

        RuleFor(x => x.Renglones)
            .NotEmpty().WithMessage("Debe indicarse al menos un renglón a modificar.")
            .Must(renglones => renglones.Select(r => r.IdCatalogoProveedor).Distinct().Count() == renglones.Count)
            .WithMessage("Un renglón no puede figurar dos veces en el mismo cambio.");

        RuleForEach(x => x.Renglones).ChildRules(renglon =>
        {
            renglon.RuleFor(r => r.IdCatalogoProveedor)
                .GreaterThan(0).WithMessage("El identificador del ítem de catálogo debe ser válido.");

            renglon.RuleFor(r => r.Cantidad)
                .GreaterThanOrEqualTo(1).WithMessage("La cantidad pedida debe ser al menos 1.");
        });
    }
}

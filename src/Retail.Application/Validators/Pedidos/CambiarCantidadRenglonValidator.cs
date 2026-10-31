using FluentValidation;
using Retail.Application.DTOs.Pedidos;

namespace Retail.Application.Validators.Pedidos;

/// <summary>
/// Validador de frontera para cambiar la cantidad de un renglón del pedido (RF-28).
/// </summary>
public class CambiarCantidadRenglonValidator : AbstractValidator<CambiarCantidadRenglonDto>
{
    public CambiarCantidadRenglonValidator()
    {
        RuleFor(x => x.IdPedido)
            .GreaterThan(0).WithMessage("El identificador del pedido debe ser válido.");

        RuleFor(x => x.IdCatalogoProveedor)
            .GreaterThan(0).WithMessage("El identificador del ítem de catálogo debe ser válido.");

        RuleFor(x => x.Cantidad)
            .GreaterThanOrEqualTo(1).WithMessage("La cantidad pedida debe ser al menos 1.");
    }
}

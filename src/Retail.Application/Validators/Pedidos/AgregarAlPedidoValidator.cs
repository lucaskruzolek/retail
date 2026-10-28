using FluentValidation;
using Retail.Application.DTOs.Pedidos;

namespace Retail.Application.Validators.Pedidos;

/// <summary>
/// Validador de frontera para agregar un ítem de catálogo al pedido (RF-28).
/// </summary>
public class AgregarAlPedidoValidator : AbstractValidator<AgregarAlPedidoDto>
{
    public AgregarAlPedidoValidator()
    {
        RuleFor(x => x.IdCatalogoProveedor)
            .GreaterThan(0).WithMessage("El identificador del ítem de catálogo debe ser válido.");

        RuleFor(x => x.Cantidad)
            .GreaterThanOrEqualTo(1).WithMessage("La cantidad pedida debe ser al menos 1.");

        RuleFor(x => x.IdUsuario)
            .GreaterThan(0).WithMessage("El identificador de usuario debe ser válido.");
    }
}

using FluentValidation;
using Retail.Application.DTOs.Pedidos;

namespace Retail.Application.Validators.Pedidos;

/// <summary>
/// Validador de frontera para agregar un lote de ítems de catálogo a los pedidos (RF-29, A-17).
/// </summary>
public class AgregarVariosAlPedidoValidator : AbstractValidator<AgregarVariosAlPedidoDto>
{
    public AgregarVariosAlPedidoValidator()
    {
        RuleFor(x => x.IdUsuario)
            .GreaterThan(0).WithMessage("El identificador de usuario debe ser válido.");

        // Un ítem repetido en el lote es un error de la pantalla que lo armó: el dominio sumaría las cantidades en
        // silencio y lo escondería.
        RuleFor(x => x.Renglones)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Debe indicar al menos un ítem para pedir.")
            .Must(renglones => renglones.Select(r => r.IdCatalogoProveedor).Distinct().Count() == renglones.Count)
            .WithMessage("Un mismo ítem de catálogo no puede repetirse en el lote.");

        RuleForEach(x => x.Renglones).ChildRules(renglon =>
        {
            renglon.RuleFor(r => r.IdCatalogoProveedor)
                .GreaterThan(0).WithMessage("El identificador del ítem de catálogo debe ser válido.");

            renglon.RuleFor(r => r.Cantidad)
                .GreaterThanOrEqualTo(1).WithMessage("La cantidad pedida debe ser al menos 1.");
        });
    }
}

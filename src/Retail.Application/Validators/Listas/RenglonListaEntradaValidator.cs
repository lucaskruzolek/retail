using FluentValidation;
using Retail.Application.DTOs.Listas;

namespace Retail.Application.Validators.Listas;

/// <summary>
/// Reglas de frontera de un renglón de lista, compartidas por la creación y la actualización.
/// </summary>
public class RenglonListaEntradaValidator : AbstractValidator<RenglonListaEntradaDto>
{
    public RenglonListaEntradaValidator()
    {
        RuleFor(x => x.IdArticulo)
            .GreaterThan(0).WithMessage("Hay un artículo inválido en la lista.");

        RuleFor(x => x.Cantidad)
            .GreaterThanOrEqualTo(1).WithMessage("La cantidad de cada artículo debe ser al menos 1.");
    }
}

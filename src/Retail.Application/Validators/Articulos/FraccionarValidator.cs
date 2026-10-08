using FluentValidation;
using Retail.Application.DTOs.Articulos;

namespace Retail.Application.Validators.Articulos;

/// <summary>
/// Validador declarativo para el fraccionamiento de un artículo de origen en una presentación (RF-21).
/// </summary>
public class FraccionarValidator : AbstractValidator<FraccionarDto>
{
    public FraccionarValidator()
    {
        RuleFor(x => x.IdArticuloDerivado)
            .GreaterThan(0).WithMessage("Debe seleccionar una presentación válida.");

        RuleFor(x => x.CantidadOrigen)
            .GreaterThanOrEqualTo(1).WithMessage("La cantidad a fraccionar debe ser al menos 1.");
    }
}

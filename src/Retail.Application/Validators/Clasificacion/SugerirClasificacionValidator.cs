using FluentValidation;
using Retail.Application.DTOs.Clasificacion;

namespace Retail.Application.Validators.Clasificacion;

/// <summary>
/// Validador de la solicitud de sugerencias de clasificación (RF-27).
/// </summary>
public class SugerirClasificacionValidator : AbstractValidator<SugerirClasificacionDto>
{
    public SugerirClasificacionValidator()
    {
        RuleFor(x => x.IdProveedor)
            .GreaterThan(0).WithMessage("Debe indicar un proveedor válido.");

        RuleFor(x => x.IdsCatalogo)
            .NotEmpty().WithMessage("Debe seleccionar al menos un ítem del catálogo.");

        RuleForEach(x => x.IdsCatalogo)
            .GreaterThan(0).WithMessage("El ID del ítem de catálogo debe ser mayor a cero.");
    }
}

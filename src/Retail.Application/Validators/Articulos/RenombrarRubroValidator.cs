using FluentValidation;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Validators.Common;

namespace Retail.Application.Validators.Articulos;

/// <summary>
/// Validador declarativo para el cambio de nombre del rubro. Usa las mismas reglas que el alta.
/// </summary>
public class RenombrarRubroValidator : AbstractValidator<RenombrarRubroDto>
{
    public RenombrarRubroValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.IdRubro)
            .GreaterThan(0).WithMessage("El identificador del rubro debe ser válido.");

        RuleFor(x => x.Nombre)
            .NombreClasificacion("el nombre del rubro");
    }
}

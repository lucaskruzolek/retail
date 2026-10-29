using FluentValidation;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Validators.Common;

namespace Retail.Application.Validators.Articulos;

/// <summary>
/// Validador declarativo para el cambio de nombre de la marca. Usa las mismas reglas que el alta.
/// </summary>
public class RenombrarMarcaValidator : AbstractValidator<RenombrarMarcaDto>
{
    public RenombrarMarcaValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.IdMarca)
            .GreaterThan(0).WithMessage("El identificador de la marca debe ser válido.");

        RuleFor(x => x.Nombre)
            .NombreClasificacion("el nombre de la marca");
    }
}

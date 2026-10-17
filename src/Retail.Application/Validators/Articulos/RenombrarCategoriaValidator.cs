using FluentValidation;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Validators.Common;

namespace Retail.Application.Validators.Articulos;

/// <summary>
/// Validador declarativo para el cambio de nombre de la categoría. Usa las mismas reglas que el alta.
/// </summary>
public class RenombrarCategoriaValidator : AbstractValidator<RenombrarCategoriaDto>
{
    public RenombrarCategoriaValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.IdCategoria)
            .GreaterThan(0).WithMessage("El identificador de la categoría debe ser válido.");

        RuleFor(x => x.Nombre)
            .NombreClasificacion("el nombre de la categoría");
    }
}

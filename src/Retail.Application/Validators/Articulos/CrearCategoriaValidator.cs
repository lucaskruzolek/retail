using FluentValidation;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Validators.Common;

namespace Retail.Application.Validators.Articulos;

/// <summary>
/// Validador declarativo para el alta de la categoría.
/// </summary>
public class CrearCategoriaValidator : AbstractValidator<CrearCategoriaDto>
{
    public CrearCategoriaValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Nombre)
            .NombreClasificacion("el nombre de la categoría");

        RuleFor(x => x.IdRubro)
            .GreaterThan(0).WithMessage("Seleccione el rubro de la categoría.");
    }
}

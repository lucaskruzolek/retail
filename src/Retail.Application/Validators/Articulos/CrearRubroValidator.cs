using FluentValidation;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Validators.Common;

namespace Retail.Application.Validators.Articulos;

/// <summary>
/// Validador declarativo para el alta del rubro.
/// </summary>
public class CrearRubroValidator : AbstractValidator<CrearRubroDto>
{
    public CrearRubroValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Nombre)
            .NombreClasificacion("el nombre del rubro");
    }
}

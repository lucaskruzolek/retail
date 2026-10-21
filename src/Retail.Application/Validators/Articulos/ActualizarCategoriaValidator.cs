using FluentValidation;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Validators.Common;

namespace Retail.Application.Validators.Articulos;

/// <summary>
/// Validador declarativo para la actualización de la categoría (nombre y rubro). Usa las mismas reglas que el alta.
/// </summary>
public class ActualizarCategoriaValidator : AbstractValidator<ActualizarCategoriaDto>
{
    public ActualizarCategoriaValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.IdCategoria)
            .GreaterThan(0).WithMessage("El identificador de la categoría debe ser válido.");

        RuleFor(x => x.Nombre)
            .NombreClasificacion("el nombre de la categoría");

        RuleFor(x => x.IdRubro)
            .GreaterThan(0).WithMessage("Seleccione el rubro de la categoría.");
    }
}

using FluentValidation;
using Retail.Application.DTOs.Proveedores;

namespace Retail.Application.Validators.Proveedores;

/// <summary>
/// Validador para la incorporación masiva de artículos a tienda desde catálogos (RF-05).
/// </summary>
public class IncorporarCatalogoArticulosValidator : AbstractValidator<IncorporarCatalogoArticulosDto>
{
    public IncorporarCatalogoArticulosValidator()
    {
        RuleFor(x => x)
            .Must(x => (x.Items != null && x.Items.Count > 0) || (x.IdsCatalogo != null && x.IdsCatalogo.Count > 0))
            .WithMessage("Debe seleccionar al menos un ítem del catálogo para incorporar.");

        RuleFor(x => x.PorcentajeGananciaSugerido)
            .GreaterThanOrEqualTo(0m).WithMessage("El porcentaje de ganancia sugerido no puede ser negativo.");

        RuleForEach(x => x.Items)
            .ChildRules(item =>
            {
                item.RuleFor(i => i.IdCatalogo)
                    .GreaterThan(0).WithMessage("El ID del ítem de catálogo debe ser mayor a cero.");

                item.RuleFor(i => i.PorcentajeGanancia)
                    .GreaterThanOrEqualTo(0m).WithMessage("El porcentaje de ganancia no puede ser negativo.");
            });

        When(x => x.IdCategoria.HasValue, () =>
        {
            RuleFor(x => x.IdCategoria!.Value)
                .GreaterThan(0).WithMessage("La categoría seleccionada no es válida.");
        });

        When(x => x.IdMarca.HasValue, () =>
        {
            RuleFor(x => x.IdMarca!.Value)
                .GreaterThan(0).WithMessage("La marca seleccionada no es válida.");
        });
    }
}

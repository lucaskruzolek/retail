using FluentValidation;
using Retail.Application.DTOs.Proveedores;

namespace Retail.Application.Validators.Proveedores;

/// <summary>
/// Validador para la incorporación de artículos a tienda desde catálogos (RF-05), con la clasificación por ítem
/// (RF-27, Track 6.6).
/// </summary>
public class IncorporarCatalogoArticulosValidator : AbstractValidator<IncorporarCatalogoArticulosDto>
{
    public IncorporarCatalogoArticulosValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Debe seleccionar al menos un ítem del catálogo para incorporar.");

        RuleForEach(x => x.Items)
            .ChildRules(item =>
            {
                item.RuleFor(i => i.IdCatalogo)
                    .GreaterThan(0).WithMessage("El ID del ítem de catálogo debe ser mayor a cero.");

                item.RuleFor(i => i.PorcentajeGanancia)
                    .GreaterThanOrEqualTo(0m).WithMessage("El porcentaje de ganancia no puede ser negativo.");

                item.RuleFor(i => i.IdCategoria)
                    .GreaterThan(0).When(i => i.IdCategoria.HasValue).WithMessage("La categoría seleccionada no es válida.");

                item.RuleFor(i => i.IdCategoriaSugerida)
                    .GreaterThan(0).When(i => i.IdCategoriaSugerida.HasValue).WithMessage("La categoría sugerida no es válida.");

                item.RuleFor(i => i.IdMarca)
                    .GreaterThan(0).When(i => i.IdMarca.HasValue).WithMessage("La marca seleccionada no es válida.");

                item.RuleFor(i => i.IdMarcaSugerida)
                    .GreaterThan(0).When(i => i.IdMarcaSugerida.HasValue).WithMessage("La marca sugerida no es válida.");

                item.RuleFor(i => i.SinMarcaConfirmada)
                    .Equal(false).When(i => i.IdMarca.HasValue)
                    .WithMessage("Un artículo con marca no puede confirmarse a la vez como \"Sin marca\".");
            });

        RuleFor(x => x.Items)
            .Must(items => items.Select(i => i.IdCatalogo).Distinct().Count() == items.Count)
            .When(x => x.Items.Count > 0)
            .WithMessage("Un ítem del catálogo no puede figurar dos veces en la misma incorporación.");
    }
}

using FluentValidation;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Validators.Common;

namespace Retail.Application.Validators.Clasificacion;

/// <summary>
/// Validador de la asignación de un par "término + segunda palabra" (Track 6.6, Fase 5). Sin categoría, el par queda
/// ignorado (P-4).
/// </summary>
public class AsignarParValidator : AbstractValidator<AsignarParDto>
{
    public AsignarParValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.IdTermino)
            .GreaterThan(0).WithMessage("Debe indicar un término válido.");

        RuleFor(x => x.SegundaPalabra)
            .SegundaPalabraDePar();

        RuleFor(x => x.IdCategoria)
            .GreaterThan(0).When(x => x.IdCategoria.HasValue)
            .WithMessage("La categoría del par debe ser válida.");
    }
}

using FluentValidation;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Validators.Common;

namespace Retail.Application.Validators.Clasificacion;

/// <summary>
/// Validador del registro o la reasignación de un término (Track 6.6, M-27). Sin categoría, el término es ambiguo.
/// </summary>
public class RegistrarTerminoValidator : AbstractValidator<RegistrarTerminoDto>
{
    public RegistrarTerminoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Termino)
            .TerminoDeClasificacion();

        RuleFor(x => x.IdCategoria)
            .GreaterThan(0).When(x => x.IdCategoria.HasValue)
            .WithMessage("La categoría del término debe ser válida.");
    }
}

using FluentValidation;
using Retail.Application.DTOs.Clasificacion;
using Retail.Domain.Common;

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
            .NotEmpty().WithMessage("Complete el término.")
            .Must(valor => ReglasClasificacion.EsTerminoValido(valor))
            .WithMessage(
                $"Revise el término: debe ser una sola palabra, sin espacios, de hasta " +
                $"{ReglasClasificacion.LongitudMaximaTermino} caracteres.");

        RuleFor(x => x.IdCategoria)
            .GreaterThan(0).When(x => x.IdCategoria.HasValue)
            .WithMessage("La categoría del término debe ser válida.");
    }
}

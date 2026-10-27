using FluentValidation;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Validators.Common;

namespace Retail.Application.Validators.Clasificacion;

/// <summary>
/// Validador de la baja de un par: la misma clave que la asignación, sin categoría.
/// </summary>
public class QuitarParValidator : AbstractValidator<QuitarParDto>
{
    public QuitarParValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.IdTermino)
            .GreaterThan(0).WithMessage("Debe indicar un término válido.");

        RuleFor(x => x.SegundaPalabra)
            .SegundaPalabraDePar();
    }
}

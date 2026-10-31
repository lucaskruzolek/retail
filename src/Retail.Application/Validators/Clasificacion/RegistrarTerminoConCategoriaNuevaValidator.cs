using FluentValidation;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Validators.Common;

namespace Retail.Application.Validators.Clasificacion;

/// <summary>
/// Validador de "crear categoría y registrar término" (Track 6.6, paso 6.2): las mismas reglas que el registro de un
/// término y que el alta de una categoría.
/// </summary>
public class RegistrarTerminoConCategoriaNuevaValidator : AbstractValidator<RegistrarTerminoConCategoriaNuevaDto>
{
    public RegistrarTerminoConCategoriaNuevaValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Termino)
            .TerminoDeClasificacion();

        RuleFor(x => x.NombreCategoria)
            .NombreClasificacion("el nombre de la categoría");

        RuleFor(x => x.IdRubro)
            .GreaterThan(0).WithMessage("Seleccione el rubro de la categoría.");
    }
}

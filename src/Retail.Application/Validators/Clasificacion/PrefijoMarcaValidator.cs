using FluentValidation;
using Retail.Application.DTOs.Clasificacion;
using Retail.Domain.Common;

namespace Retail.Application.Validators.Clasificacion;

/// <summary>
/// Validador del alta y la baja de un prefijo EAN de marca (Track 6.6, M-28).
/// </summary>
public class PrefijoMarcaValidator : AbstractValidator<PrefijoMarcaDto>
{
    public PrefijoMarcaValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.IdMarca)
            .GreaterThan(0).WithMessage("El identificador de la marca debe ser válido.");

        RuleFor(x => x.Prefijo)
            .NotEmpty().WithMessage("Complete el prefijo.")
            .Must(valor => ReglasCodigoBarras.EsPrefijoTitularValido(valor))
            .WithMessage(
                $"Revise el prefijo: debe tener exactamente {ReglasCodigoBarras.LongitudPrefijoTitular} dígitos y no " +
                "puede empezar con 2 (códigos de uso interno).");
    }
}

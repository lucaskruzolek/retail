using FluentValidation;
using Retail.Application.DTOs.Clasificacion;
using Retail.Domain.Common;

namespace Retail.Application.Validators.Clasificacion;

/// <summary>
/// Validador del alta y la baja de un alias de marca (Track 6.6, sección 8.1).
/// </summary>
public class AliasMarcaValidator : AbstractValidator<AliasMarcaDto>
{
    public AliasMarcaValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.IdMarca)
            .GreaterThan(0).WithMessage("El identificador de la marca debe ser válido.");

        RuleFor(x => x.Alias)
            .NotEmpty().WithMessage("Complete el alias.")
            .Must(valor => ReglasClasificacion.EsAliasValido(valor))
            .WithMessage($"Revise el alias: no puede superar los {ReglasClasificacion.LongitudMaximaAlias} caracteres.");
    }
}

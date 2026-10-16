using FluentValidation;
using Retail.Application.DTOs.Reportes;

namespace Retail.Application.Validators.Reportes;

/// <summary>Valida los filtros del ranking de artículos.</summary>
public class FiltroRankingValidator : FiltroPeriodoValidatorBase<FiltroRankingDto>
{
    public FiltroRankingValidator(TimeProvider reloj)
        : base(reloj)
    {
        RuleFor(x => x.Criterio).IsInEnum().WithMessage("El criterio del ranking no es válido.");
        RuleFor(x => x.Cantidad).InclusiveBetween(1, 100).WithMessage("La cantidad debe estar entre 1 y 100.");
        RuleFor(x => x.IdCategoria).GreaterThan(0).When(x => x.IdCategoria.HasValue).WithMessage("La categoría no es válida.");
        RuleFor(x => x.IdMarca).GreaterThan(0).When(x => x.IdMarca.HasValue).WithMessage("La marca no es válida.");
    }
}

using FluentValidation;
using Retail.Application.DTOs.Reportes;

namespace Retail.Application.Validators.Reportes;

/// <summary>Valida los filtros del reporte de rentabilidad.</summary>
public class FiltroRentabilidadValidator : FiltroPeriodoValidatorBase<FiltroRentabilidadDto>
{
    public FiltroRentabilidadValidator(TimeProvider reloj)
        : base(reloj)
    {
        RuleFor(x => x.AgruparPor).IsInEnum().WithMessage("La agrupación no es válida.");
        RuleFor(x => x.IdCategoria).GreaterThan(0).When(x => x.IdCategoria.HasValue).WithMessage("La categoría no es válida.");
        RuleFor(x => x.IdMarca).GreaterThan(0).When(x => x.IdMarca.HasValue).WithMessage("La marca no es válida.");
    }
}

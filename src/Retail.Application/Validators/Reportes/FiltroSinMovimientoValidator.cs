using FluentValidation;
using Retail.Application.DTOs.Reportes;

namespace Retail.Application.Validators.Reportes;

/// <summary>Valida los filtros del reporte de artículos sin movimiento.</summary>
public class FiltroSinMovimientoValidator : AbstractValidator<FiltroSinMovimientoDto>
{
    public FiltroSinMovimientoValidator()
    {
        Include(new FiltroInventarioValidator());
        RuleFor(x => x.Dias).InclusiveBetween(1, 3650).WithMessage("Los días sin movimiento deben estar entre 1 y 3650.");
    }
}

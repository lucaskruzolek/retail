using FluentValidation;
using Retail.Application.DTOs.Reportes;

namespace Retail.Application.Validators.Reportes;

/// <summary>Valida los filtros del reporte de ventas por período.</summary>
public class FiltroVentasPorPeriodoValidator : FiltroPeriodoValidatorBase<FiltroVentasPorPeriodoDto>
{
    public FiltroVentasPorPeriodoValidator(TimeProvider reloj)
        : base(reloj)
    {
        RuleFor(x => x.Agrupacion).IsInEnum().WithMessage("La agrupación no es válida.");
        RuleFor(x => x.IdUsuario).GreaterThan(0).When(x => x.IdUsuario.HasValue).WithMessage("El cajero no es válido.");
    }
}

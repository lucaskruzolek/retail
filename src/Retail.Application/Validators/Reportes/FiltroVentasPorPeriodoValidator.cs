using FluentValidation;
using Retail.Application.DTOs.Reportes;

namespace Retail.Application.Validators.Reportes;

/// <summary>Valida los filtros del reporte de ventas por período.</summary>
public class FiltroVentasPorPeriodoValidator : FiltroPeriodoValidatorBase<FiltroVentasPorPeriodoDto>
{
    /// <summary>Máximo de días con agrupación diaria: un año bisiesto completo (D-32: acotar en lugar de paginar).</summary>
    public const int MaximoDiasConAgrupacionDiaria = 366;

    /// <summary>Máximo de años con agrupación semanal (D-32).</summary>
    public const int MaximoAniosConAgrupacionSemanal = 5;

    public FiltroVentasPorPeriodoValidator(TimeProvider reloj)
        : base(reloj)
    {
        RuleFor(x => x.Agrupacion).IsInEnum().WithMessage("La agrupación no es válida.");
        RuleFor(x => x.Hasta)
            .Must((filtro, hasta) => hasta.DayNumber - filtro.Desde.DayNumber + 1 <= MaximoDiasConAgrupacionDiaria)
            .When(x => x.Agrupacion == AgrupacionTemporalEnum.Dia)
            .WithMessage($"Agrupado por día, el período no puede superar los {MaximoDiasConAgrupacionDiaria} días.");

        RuleFor(x => x.Hasta)
            .Must((filtro, hasta) => EntraEnAnios(filtro.Desde, hasta, MaximoAniosConAgrupacionSemanal))
            .When(x => x.Agrupacion == AgrupacionTemporalEnum.Semana)
            .WithMessage($"Agrupado por semana, el período no puede superar los {MaximoAniosConAgrupacionSemanal} años.");

        RuleFor(x => x.IdUsuario).GreaterThan(0).When(x => x.IdUsuario.HasValue).WithMessage("El cajero no es válido.");
    }
}

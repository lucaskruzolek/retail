using FluentValidation;
using Retail.Application.DTOs.Reportes;
using Retail.Application.Services.Reportes;

namespace Retail.Application.Validators.Reportes;

/// <summary>
/// Reglas comunes de todo filtro con período: orden de las fechas y que no sean futuras. "Hoy" es el día local del
/// comercio (D-9).
/// </summary>
/// <typeparam name="T">Tipo concreto del filtro; cada validador valida su tipo exacto.</typeparam>
public abstract class FiltroPeriodoValidatorBase<T> : AbstractValidator<T>
    where T : FiltroPeriodoDto
{
    protected FiltroPeriodoValidatorBase(TimeProvider reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        RuleFor(x => x.Desde)
            .LessThanOrEqualTo(x => x.Hasta)
            .WithMessage("La fecha desde no puede ser posterior a la fecha hasta.");

        RuleFor(x => x.Hasta)
            .Must(hasta => hasta <= reloj.HoyLocal())
            .WithMessage("La fecha hasta no puede ser posterior a hoy.");
    }
}

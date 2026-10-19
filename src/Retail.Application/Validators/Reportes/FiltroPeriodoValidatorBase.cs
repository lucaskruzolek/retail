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
    /// <summary>
    /// Largo máximo de cualquier período, en años (D-32: se acota el rango en lugar de paginar los resultados).
    /// </summary>
    public const int MaximoAniosDelPeriodo = 10;

    protected FiltroPeriodoValidatorBase(TimeProvider reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        RuleFor(x => x.Desde)
            .LessThanOrEqualTo(x => x.Hasta)
            .WithMessage("La fecha desde no puede ser posterior a la fecha hasta.");

        RuleFor(x => x.Hasta)
            .Must((filtro, hasta) => EntraEnAnios(filtro.Desde, hasta, MaximoAniosDelPeriodo))
            .WithMessage($"El período no puede superar los {MaximoAniosDelPeriodo} años.");

        RuleFor(x => x.Hasta)
            .Must(hasta => hasta <= reloj.HoyLocal())
            .WithMessage("La fecha hasta no puede ser posterior a hoy.");
    }

    /// <summary>
    /// Indica si <paramref name="hasta"/> cae dentro de los <paramref name="anios"/> años calendario que empiezan en
    /// <paramref name="desde"/> (01/01/2026 a 31/12/2026 es un año completo).
    /// </summary>
    protected static bool EntraEnAnios(DateOnly desde, DateOnly hasta, int anios)
    {
        // Evita desbordar DateOnly con fechas absurdas: otras reglas ya las rechazan.
        return desde.Year + anios > 9999 || hasta <= desde.AddYears(anios).AddDays(-1);
    }
}

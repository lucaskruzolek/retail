using FluentValidation;
using Retail.Application.DTOs.Sesiones;

namespace Retail.Application.Validators.Sesiones;

/// <summary>
/// Validador de frontera del aviso de mantenimiento: entre 0 y 60 minutos desde ahora y un motivo corto.
/// </summary>
public class ProgramarMantenimientoValidator : AbstractValidator<ProgramarMantenimientoDto>
{
    public const int MinutosMaximos = 60;
    public const int LongitudMaximaMotivo = 200;

    public ProgramarMantenimientoValidator()
    {
        RuleFor(x => x.MinutosDesdeAhora)
            .InclusiveBetween(0, MinutosMaximos)
            .WithMessage($"El mantenimiento debe programarse entre 0 y {MinutosMaximos} minutos desde ahora.");

        RuleFor(x => x.Motivo)
            .Must(motivo => motivo is null || motivo.Trim().Length <= LongitudMaximaMotivo)
            .WithMessage($"El motivo no puede superar los {LongitudMaximaMotivo} caracteres.");
    }
}

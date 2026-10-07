using FluentValidation;
using Retail.Application.DTOs.Caja;

namespace Retail.Application.Validators.Caja;

public class MovimientoCajaValidator : AbstractValidator<MovimientoCajaDto>
{
    public MovimientoCajaValidator()
    {
        RuleFor(x => x.Monto)
            .GreaterThan(0)
            .WithMessage("El monto del movimiento debe ser mayor a cero.");

        RuleFor(x => x.Concepto)
            .NotEmpty()
            .WithMessage("El concepto u observación del movimiento es obligatorio.")
            .MaximumLength(200)
            .WithMessage("El concepto no puede superar los 200 caracteres.");
    }
}

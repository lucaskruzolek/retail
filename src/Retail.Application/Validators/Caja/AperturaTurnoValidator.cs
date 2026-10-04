using FluentValidation;
using Retail.Application.DTOs.Caja; // Ajusta el namespace según la ubicación de tus DTOs de caja

namespace Retail.Application.Validators.Caja;

public class AperturaTurnoValidator : AbstractValidator<AperturaTurnoDto>
{
    public AperturaTurnoValidator()
    {
        RuleFor(x => x.IdUsuario)
            .GreaterThan(0)
            .WithMessage("Debe especificarse un usuario válido para la apertura del turno.");

        RuleFor(x => x.SaldoInicial)
            .GreaterThanOrEqualTo(0)
            .WithMessage("El saldo inicial de caja no puede ser negativo.");
    }
}
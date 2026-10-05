using FluentValidation;
using Retail.Application.DTOs.Caja;

namespace Retail.Application.Validators.Caja;

public class ArqueoCiegoValidator : AbstractValidator<ArqueoCiegoDto>
{
    public ArqueoCiegoValidator()
    {
        RuleFor(x => x.IdTurno)
            .GreaterThan(0)
            .WithMessage("El identificador del turno a cerrar es inválido.");

        RuleFor(x => x.SaldoDeclaradoEfectivo)
            .GreaterThanOrEqualTo(0)
            .WithMessage("El saldo declarado en efectivo no puede ser negativo.");

        RuleFor(x => x.MontoRetenidoEnCaja)
            .GreaterThanOrEqualTo(0)
            .WithMessage("El monto retenido para el próximo turno no puede ser negativo.");
    }
}

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

        RuleFor(x => x.IdUsuarioActor)
            .GreaterThan(0)
            .WithMessage("Debe especificarse quién cierra el turno.");

        RuleFor(x => x.SaldoTeoricoLeido)
            .GreaterThanOrEqualTo(0)
            .WithMessage("El saldo teórico leído no puede ser negativo.");

        RuleFor(x => x.TotalElectronicoLeido)
            .GreaterThanOrEqualTo(0)
            .WithMessage("El total electrónico leído no puede ser negativo.");

        RuleFor(x => x.CantidadVentasLeida)
            .GreaterThanOrEqualTo(0)
            .WithMessage("La cantidad de ventas leída no puede ser negativa.");

        RuleFor(x => x.MontoRetenidoEnCaja)
            .GreaterThanOrEqualTo(0)
            .WithMessage("El monto retenido para el próximo turno no puede ser negativo.");
    }
}

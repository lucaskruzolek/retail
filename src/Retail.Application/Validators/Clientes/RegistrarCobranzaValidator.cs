using FluentValidation;
using Retail.Application.DTOs.Clientes;

namespace Retail.Application.Validators.Clientes;

/// <summary>
/// Validador declarativo para la registración de cobranzas de cuentas corrientes (RF-20).
/// </summary>
public class RegistrarCobranzaValidator : AbstractValidator<RegistrarCobranzaDto>
{
    public RegistrarCobranzaValidator()
    {
        RuleFor(x => x.IdCliente)
            .GreaterThan(0).WithMessage("El identificador de cliente debe ser válido.");

        RuleFor(x => x.IdTurno)
            .GreaterThan(0).WithMessage("El identificador de turno de caja debe ser válido.");

        RuleFor(x => x.IdUsuario)
            .GreaterThan(0).WithMessage("El identificador de operador debe ser válido.");

        RuleFor(x => x.Monto)
            .GreaterThan(0m).WithMessage("El importe de la cobranza debe ser mayor a cero.");

        RuleFor(x => x.MedioPago)
            .IsInEnum().WithMessage("Debe especificar un medio de pago válido.");

        When(x => !string.IsNullOrWhiteSpace(x.Referencia), () =>
        {
            RuleFor(x => x.Referencia)
                .MaximumLength(100).WithMessage("La referencia del cobro no puede exceder 100 caracteres.");
        });
    }
}

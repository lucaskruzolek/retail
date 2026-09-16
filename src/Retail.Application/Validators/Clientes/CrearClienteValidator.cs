using FluentValidation;
using Retail.Application.DTOs.Clientes;

namespace Retail.Application.Validators.Clientes;

/// <summary>
/// Validador declarativo para el alta de nuevos clientes comerciales (RF-20).
/// </summary>
public class CrearClienteValidator : AbstractValidator<CrearClienteDto>
{
    public CrearClienteValidator()
    {
        RuleFor(x => x.RazonSocialONombre)
            .NotEmpty().WithMessage("La razón social o nombre del cliente es obligatorio.")
            .MaximumLength(150).WithMessage("La razón social o nombre no puede exceder 150 caracteres.");

        RuleFor(x => x.NumeroDocumento)
            .NotEmpty().WithMessage("El número de documento o CUIT es obligatorio.")
            .MinimumLength(7).WithMessage("El número de documento debe tener al menos 7 caracteres.")
            .MaximumLength(20).WithMessage("El número de documento no puede exceder 20 caracteres.");

        RuleFor(x => x.TipoDocumento)
            .IsInEnum().WithMessage("Debe especificar un tipo de documento válido.");

        RuleFor(x => x.CondicionIva)
            .IsInEnum().WithMessage("Debe especificar una condición de IVA válida.");

        RuleFor(x => x.LimiteCredito)
            .GreaterThanOrEqualTo(0m).WithMessage("El límite de crédito no puede ser negativo.");

        When(x => !string.IsNullOrWhiteSpace(x.Email), () =>
        {
            RuleFor(x => x.Email)
                .EmailAddress().WithMessage("El formato del correo electrónico no es válido.")
                .MaximumLength(100).WithMessage("El correo electrónico no puede exceder 100 caracteres.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.Telefono), () =>
        {
            RuleFor(x => x.Telefono)
                .MaximumLength(50).WithMessage("El teléfono no puede exceder 50 caracteres.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.DomicilioFiscal), () =>
        {
            RuleFor(x => x.DomicilioFiscal)
                .MaximumLength(200).WithMessage("El domicilio fiscal no puede exceder 200 caracteres.");
        });
    }
}

using FluentValidation;
using Retail.Application.DTOs.Proveedores;

namespace Retail.Application.Validators.Proveedores;

/// <summary>
/// Validador declarativo para la modificación de distribuidores mayoristas.
/// </summary>
public class ActualizarProveedorValidator : AbstractValidator<ProveedorDto>
{
    public ActualizarProveedorValidator()
    {
        RuleFor(x => x.IdProveedor)
            .GreaterThan(0).WithMessage("El identificador del proveedor debe ser válido.");

        RuleFor(x => x.RazonSocial)
            .NotEmpty().WithMessage("La razón social del proveedor es obligatoria.")
            .MaximumLength(150).WithMessage("La razón social no puede exceder 150 caracteres.");

        RuleFor(x => x.Cuit)
            .NotEmpty().WithMessage("El CUIT del proveedor es obligatorio.")
            .Must(CrearProveedorValidator.EsCuitValido).WithMessage("El formato del CUIT es inválido. Debe contener 11 dígitos numéricos.");

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
    }
}
